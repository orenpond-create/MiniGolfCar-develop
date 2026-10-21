using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the car-tuning test course in Assets/Scenes/TestScene4_TerrianClaude.unity.
// Run:  Tools > MiniCarGolf > Build Test Course Scene
//
// What it does (safe to re-run - it replaces its own output and leaves everything else alone):
//   1. Opens TestScene4_TerrianClaude (a copy of TestScene3_Terrian) and removes the old Terrain.
//   2. Builds a new terrain: flat in the middle (the course lane) with gentle hills at the sides.
//   3. Builds the "TestCourse" object with five stations along +Z, each with exact, named sizes:
//        Curbs, Ramps, JumpGap, Washboard, DeadEnd
//   4. Puts everything on the ground layer the car's ground check uses (see GroundLayerName).
//   5. Moves "Hummer (1)" to the start position and saves the scene.
// Every size is a constant below, so changing a number and re-running rebuilds the course.
// Ramps are generated wedge meshes (no ProBuilder needed). Meshes/materials/terrain data are saved
// under Assets/TestCourse so the scene keeps working.
public static class TestCourseBuilder
{
    const string ScenePath = "Assets/Scenes/TestScene4_TerrianClaude.unity";
    const string AssetFolder = "Assets/TestCourse";
    const string MeshFolder = AssetFolder + "/Meshes";
    const string MaterialFolder = AssetFolder + "/Materials";
    const string TerrainDataPath = AssetFolder + "/CourseTerrain.asset";
    const string RootName = "TestCourse";
    const string HummerName = "Hummer (1)";

    // Must match the layer in the car's groundMask, or the car will think it is airborne on obstacles.
    const string GroundLayerName = "Background";

    // --- Terrain: flat in the middle, hills at the sides (|x| > HillStartX) ---
    static readonly Vector3 TerrainSize = new Vector3(240f, 20f, 300f);
    static readonly Vector3 TerrainOrigin = new Vector3(-120f, 0f, -60f);   // so the course lane (x ~ 0) is centred
    const int HeightmapResolution = 513;                                      // must be 2^n + 1
    const float HillStartX = 60f;
    const float HillFullX = 90f;
    const float HillHeight = 8f;
    const float HillNoiseScale = 0.025f;

    // --- Start position of the Hummer (faces +Z) ---
    static readonly Vector3 StartPosition = new Vector3(0f, 1.2f, -45f);

    // --- Station 1: curbs (four lanes, different heights) ---
    const float CurbZ = 0f;
    const float CurbWidth = 5f;
    const float CurbDepth = 2f;
    static readonly float[] CurbHeights = { 0.1f, 0.2f, 0.4f, 0.8f };
    static readonly float[] CurbLaneX = { -9f, -3f, 3f, 9f };

    // --- Station 2: ramps (three lanes, different angles) ---
    const float RampZ = 30f;
    const float RampWidth = 5f;
    const float RampRun = 6f;                      // horizontal length of each ramp
    static readonly float[] RampAngles = { 10f, 20f, 35f };
    static readonly float[] RampLaneX = { -6f, 0f, 6f };

    // --- Station 3: jump (launch ramp, gap, landing ramp) ---
    const float JumpZ = 70f;
    const float JumpWidth = 6f;
    const float JumpAngle = 20f;
    const float JumpRun = 6f;
    const float JumpGap = 6f;                      // TUNE: with maxSpeed 54 (~7 m/s) the car clears only ~2 m; with 400 (~20 m/s) about 12 m

    // --- Station 4: washboard bumps (two lanes: small/fast and big/slow) ---
    const float WashboardZ = 110f;
    const float WashboardWidth = 6f;
    static readonly float[] BumpDiameter = { 0.3f, 0.6f };      // bump height = diameter / 2
    static readonly float[] BumpSpacing = { 1.2f, 2.0f };
    static readonly int[] BumpCount = { 12, 8 };
    static readonly float[] BumpLaneX = { -4f, 4f };

    // --- Station 5: dead end (U shape, open toward -Z) ---
    const float DeadEndZ = 150f;
    const float DeadEndInnerWidth = 8f;            // TUNE: the car needs about 6.5 m to U-turn at ~7 m/s
    const float DeadEndDepth = 10f;
    const float DeadEndWallHeight = 2.5f;
    const float DeadEndWallThickness = 0.6f;

    [MenuItem("Tools/MiniCarGolf/Build Test Course Scene")]
    public static void Build()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            Debug.LogError("Scene not found: " + ScenePath);
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureFolder("Assets", "TestCourse");
        EnsureFolder(AssetFolder, "Meshes");
        EnsureFolder(AssetFolder, "Materials");

        int groundLayer = LayerMask.NameToLayer(GroundLayerName);
        if (groundLayer < 0)
        {
            Debug.LogWarning("Layer '" + GroundLayerName + "' not found - using Default. Check the car's groundMask.");
            groundLayer = 0;
        }

        // Clean the scene: old terrain(s) and any previous run of this builder.
        foreach (Terrain oldTerrain in Object.FindObjectsOfType<Terrain>())
            Object.DestroyImmediate(oldTerrain.gameObject);
        GameObject oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
            Object.DestroyImmediate(oldRoot);

        GameObject root = new GameObject(RootName);

        BuildTerrain(root.transform);
        BuildCurbs(root.transform);
        BuildRamps(root.transform);
        BuildJump(root.transform);
        BuildWashboard(root.transform);
        BuildDeadEnd(root.transform);

        // Everything on the car's ground layer, and static.
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = groundLayer;
            t.gameObject.isStatic = true;
        }

        PlaceHummer();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Test course built in " + ScenePath + ". Stations along +Z: Curbs z=" + CurbZ + ", Ramps z=" + RampZ +
                  ", Jump z=" + JumpZ + ", Washboard z=" + WashboardZ + ", DeadEnd z=" + DeadEndZ + ".");
    }

    // ---------------------------------------------------------------- terrain
    static void BuildTerrain(Transform parent)
    {
        TerrainData data = new TerrainData();
        data.heightmapResolution = HeightmapResolution;   // set resolution BEFORE size
        data.size = TerrainSize;

        float[,] heights = new float[HeightmapResolution, HeightmapResolution];
        for (int row = 0; row < HeightmapResolution; row++)          // row = Z
        {
            for (int col = 0; col < HeightmapResolution; col++)      // col = X
            {
                float worldX = TerrainOrigin.x + col / (float)(HeightmapResolution - 1) * TerrainSize.x;
                float worldZ = TerrainOrigin.z + row / (float)(HeightmapResolution - 1) * TerrainSize.z;
                float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HillStartX, HillFullX, Mathf.Abs(worldX)));
                float noise = Mathf.PerlinNoise(worldX * HillNoiseScale + 100f, worldZ * HillNoiseScale + 100f);   // 0..1
                heights[row, col] = mask * noise * HillHeight / TerrainSize.y;                                      // normalised 0..1
            }
        }
        data.SetHeights(0, 0, heights);

        if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath) != null)
            AssetDatabase.DeleteAsset(TerrainDataPath);
        AssetDatabase.CreateAsset(data, TerrainDataPath);

        GameObject terrain = Terrain.CreateTerrainGameObject(data);   // adds Terrain + TerrainCollider
        terrain.name = "CourseTerrain";
        terrain.transform.SetParent(parent, false);
        terrain.transform.position = TerrainOrigin;
    }

    // ---------------------------------------------------------------- stations
    // Station 1: four curbs side by side. Tests stuck detection, climbing, the old "jump off a cube" bug.
    static void BuildCurbs(Transform root)
    {
        Transform station = NewStation(root, "Station1_Curbs");
        Material mat = GetMaterial("Curb", new Color(0.95f, 0.55f, 0.1f));
        for (int i = 0; i < CurbHeights.Length; i++)
        {
            float h = CurbHeights[i];
            CreateBox("Curb_" + h.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "m", station,
                new Vector3(CurbLaneX[i], h * 0.5f, CurbZ), new Vector3(CurbWidth, h, CurbDepth), mat);
        }
    }

    // Station 2: three launch ramps of different angles. Tests the tilt clamp, launch, air control.
    static void BuildRamps(Transform root)
    {
        Transform station = NewStation(root, "Station2_Ramps");
        Material mat = GetMaterial("Ramp", new Color(0.2f, 0.6f, 0.9f));
        for (int i = 0; i < RampAngles.Length; i++)
        {
            float height = RampRun * Mathf.Tan(RampAngles[i] * Mathf.Deg2Rad);
            string name = "Ramp_" + RampAngles[i].ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "deg";
            CreateWedge(name, station, new Vector3(RampLaneX[i], 0f, RampZ), Quaternion.identity, RampWidth, RampRun, height, mat);
        }
    }

    // Station 3: launch ramp, a gap, then a landing ramp (the same wedge turned 180 degrees).
    static void BuildJump(Transform root)
    {
        Transform station = NewStation(root, "Station3_JumpGap");
        Material mat = GetMaterial("Jump", new Color(0.3f, 0.8f, 0.3f));
        float height = JumpRun * Mathf.Tan(JumpAngle * Mathf.Deg2Rad);

        CreateWedge("LaunchRamp", station, new Vector3(0f, 0f, JumpZ), Quaternion.identity, JumpWidth, JumpRun, height, mat);

        // Turned 180 degrees about Y the wedge spans z = pos.z - run .. pos.z, tall end toward the gap.
        float landingStartZ = JumpZ + JumpRun + JumpGap;
        CreateWedge("LandingRamp", station, new Vector3(0f, 0f, landingStartZ + JumpRun), Quaternion.Euler(0f, 180f, 0f),
            JumpWidth, JumpRun, height, mat);
    }

    // Station 4: rows of half-buried cylinders across two lanes. Tests ground-flicker handling and suspension feel.
    static void BuildWashboard(Transform root)
    {
        Transform station = NewStation(root, "Station4_Washboard");
        Material mat = GetMaterial("Washboard", new Color(0.8f, 0.2f, 0.8f));
        for (int lane = 0; lane < BumpDiameter.Length; lane++)
        {
            float d = BumpDiameter[lane];
            for (int i = 0; i < BumpCount[lane]; i++)
            {
                GameObject bump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bump.name = "Bump_L" + lane + "_" + i;
                bump.transform.SetParent(station, false);
                bump.transform.localPosition = new Vector3(BumpLaneX[lane], 0f, WashboardZ + i * BumpSpacing[lane]);
                bump.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);                 // lay the cylinder along X
                bump.transform.localScale = new Vector3(d, WashboardWidth * 0.5f, d);         // primitive cylinder is 2 units tall
                bump.GetComponent<Renderer>().sharedMaterial = mat;

                // The primitive comes with a capsule collider; use the real mesh shape instead.
                Object.DestroyImmediate(bump.GetComponent<Collider>());
                bump.AddComponent<MeshCollider>().sharedMesh = bump.GetComponent<MeshFilter>().sharedMesh;
            }
        }
    }

    // Station 5: three walls forming a U, open toward -Z. Tests the stuck / recovery system.
    static void BuildDeadEnd(Transform root)
    {
        Transform station = NewStation(root, "Station5_DeadEnd");
        Material mat = GetMaterial("DeadEnd", new Color(0.85f, 0.15f, 0.15f));
        float t = DeadEndWallThickness;
        float h = DeadEndWallHeight;

        CreateBox("BackWall", station,
            new Vector3(0f, h * 0.5f, DeadEndZ + DeadEndDepth + t * 0.5f),
            new Vector3(DeadEndInnerWidth + 2f * t, h, t), mat);
        CreateBox("LeftWall", station,
            new Vector3(-(DeadEndInnerWidth * 0.5f + t * 0.5f), h * 0.5f, DeadEndZ + DeadEndDepth * 0.5f),
            new Vector3(t, h, DeadEndDepth), mat);
        CreateBox("RightWall", station,
            new Vector3(DeadEndInnerWidth * 0.5f + t * 0.5f, h * 0.5f, DeadEndZ + DeadEndDepth * 0.5f),
            new Vector3(t, h, DeadEndDepth), mat);
    }

    // ---------------------------------------------------------------- the car
    static void PlaceHummer()
    {
        GameObject hummer = GameObject.Find(HummerName);
        if (hummer == null)
        {
            Debug.LogWarning("'" + HummerName + "' not found in the scene - place your car at " + StartPosition + " yourself.");
            return;
        }
        hummer.transform.SetPositionAndRotation(StartPosition, Quaternion.identity);
        Rigidbody rb = hummer.GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // ---------------------------------------------------------------- helpers
    static Transform NewStation(Transform root, string name)
    {
        GameObject station = new GameObject(name);
        station.transform.SetParent(root, false);
        return station.transform;
    }

    static GameObject CreateBox(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = center;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = mat;
        return box;
    }

    // A triangular wedge (a ramp): climbs toward +Z over 'run', reaching 'height'. Origin = centre of the low end.
    static GameObject CreateWedge(string name, Transform parent, Vector3 position, Quaternion rotation,
                                  float width, float run, float height, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;

        Mesh mesh = BuildWedgeMesh(name, width, run, height);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    // Six faces with their own vertices (hard edges). Each triangle is wound so that
    // Vector3.Cross(b - a, c - a) points OUTWARD, which is Unity's front-face rule.
    static Mesh BuildWedgeMesh(string name, float width, float run, float height)
    {
        float hw = width * 0.5f;
        Vector3 a = new Vector3(-hw, 0f, 0f);        // low end, left
        Vector3 b = new Vector3(hw, 0f, 0f);         // low end, right
        Vector3 c = new Vector3(-hw, 0f, run);       // far end bottom, left
        Vector3 d = new Vector3(hw, 0f, run);        // far end bottom, right
        Vector3 e = new Vector3(-hw, height, run);   // far end top, left
        Vector3 f = new Vector3(hw, height, run);    // far end top, right

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            int i = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        Tri(a, b, d); Tri(a, d, c);      // bottom   (normal down)
        Tri(a, e, f); Tri(a, f, b);      // slope    (normal up and back toward -Z)
        Tri(c, d, f); Tri(c, f, e);      // far wall (normal +Z)
        Tri(a, c, e);                    // left     (normal -X)
        Tri(b, f, d);                    // right    (normal +X)

        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        string path = MeshFolder + "/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            AssetDatabase.DeleteAsset(path);                  // rebuild so the file always matches the constants
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Material GetMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
