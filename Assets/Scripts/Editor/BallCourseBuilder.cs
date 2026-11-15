using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds a basic mini-golf hole for testing ball control, in Assets/Scenes/TestScene5_BallCourse.unity.
// Run:  Tools > MiniCarGolf > Build Ball Course Scene
//
// Layout (loosely after the "roller coaster" reference): a raised tee lane, a smooth ramp down,
// a flat stretch reserved for a future loop, a 90 degree right turn, a back straight, a 180 degree
// U-turn, a return straight and a cup section with a real 108 mm hole, closed by an end wall.
//
// The lane floor and both rails are swept along ONE sampled centre line, so there are no seams
// between pieces for a 4 cm ball to catch on. Re-running replaces the "BallCourse" object and
// re-applies the golf-ball setup; everything else in the scene is left alone. All sizes are
// constants below. Generated meshes, materials and physic materials go to Assets/BallCourse.
public static class BallCourseBuilder
{
    const string ScenePath = "Assets/Scenes/TestScene5_BallCourse.unity";
    const string SourceScenePath = "Assets/Scenes/TestScene1_Ball_Camera.unity";   // copied once if ScenePath doesn't exist
    const string AssetFolder = "Assets/BallCourse";
    const string RootName = "BallCourse";
    const string BallPath = "GameZone/PlayerBall";
    const string CameraPath = "CameraPivot/Main Camera";
    const string CubesPath = "GameZone/Cubes";
    const string CourseLayerName = "Background";

    // --- Golf ball (regulation: 42.67 mm, 45.93 g) ---
    const float BallRadius = 0.02135f;
    const float BallMass = 0.0459f;
    const float BallShotStrength = 0.6f;   // Ball.ballSpeed. Max impulse = 0.3 (arrow length) * this -> ~3.9 m/s top shot at this mass
    const float ClickRadius = 0.15f;       // world radius of the invisible trigger that makes the tiny ball easy to click
    const float CameraDistance = 2.5f;     // Main Camera distance behind the follow pivot (was 10 for the 1 m ball)

    // --- Physics feel (TUNE) ---
    const float BallFriction = 0.4f;
    const float BallBounce = 0.6f;
    const float GreenFriction = 0.4f;      // lane surface ("carpet")
    const float RailFriction = 0.2f;
    const float RailBounce = 0.6f;         // rubber-ish bumpers

    // --- Course cross-section ---
    const float LaneWidth = 1.0f;
    const float BaseHeight = 0.1f;         // lane surface above the ground; the cup sinks into this
    const float TeeHeight = 0.6f;          // raised start lane
    const float RailHeight = 0.08f;        // rail top above the lane surface
    const float RailThickness = 0.06f;
    const float SampleStep = 0.05f;        // centre-line sampling distance

    // --- Path. Starts at the origin heading +Z; all turns are to the right ---
    const float TeeLength = 3.0f;
    const float RampLength = 2.5f;         // smooth (cosine) drop from TeeHeight to BaseHeight, max slope ~17 degrees
    const float LoopSpotLength = 2.0f;     // flat stretch reserved for the loop (step 2)
    const float Turn1Radius = 1.5f;        // 90 degrees
    const float BackStraightLength = 4.0f;
    const float UTurnRadius = 1.2f;        // 180 degrees
    const float ReturnLength = 2.5f;
    const float CupSectionLength = 1.5f;   // flat plate with the hole, then the end wall

    // --- Cup (regulation: 108 mm diameter) ---
    const float CupRadius = 0.054f;
    const float CupDepth = 0.1f;           // = BaseHeight, so the cup reaches the ground
    const float CupFromEnd = 0.5f;         // hole centre distance from the end wall
    const int CupSegments = 32;

    struct PathSample
    {
        public Vector3 pos;   // centre line; pos.y = lane surface height
        public Vector3 dir;   // horizontal heading (unit)
    }

    [MenuItem("Tools/MiniCarGolf/Build Ball Course Scene")]
    public static void Build()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
            {
                Debug.LogError("Could not create " + ScenePath + " from " + SourceScenePath);
                return;
            }
        }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        var scene = EditorSceneManager.GetActiveScene();

        EnsureFolder("Assets", "BallCourse");
        EnsureFolder(AssetFolder, "Meshes");
        EnsureFolder(AssetFolder, "Materials");
        EnsureFolder(AssetFolder, "Physics");

        int layer = LayerMask.NameToLayer(CourseLayerName);
        if (layer < 0) layer = 0;

        GameObject old = GameObject.Find(RootName);
        if (old != null)
            Object.DestroyImmediate(old);

        Material greenMat = GetMaterial("Green", new Color(0.25f, 0.6f, 0.25f));
        Material railMat = GetMaterial("Rail", new Color(0.6f, 0.15f, 0.15f));
        Material cupMat = GetMaterial("Cup", new Color(0.08f, 0.08f, 0.08f));
        PhysicMaterial greenPhys = GetPhysicMaterial("Green", GreenFriction, 0f);
        PhysicMaterial railPhys = GetPhysicMaterial("Rail", RailFriction, RailBounce);
        PhysicMaterial ballPhys = GetPhysicMaterial("GolfBall", BallFriction, BallBounce);

        List<PathSample> path = BuildPath(out int cupStartIndex);

        GameObject root = new GameObject(RootName);

        // Lane floor up to the cup section (the cup section gets its own plate with a hole).
        Mesh floor = BuildFloorMesh(path, cupStartIndex);
        CreateMeshObject("LaneFloor", root.transform, SaveMesh(floor, "LaneFloor"), greenMat, greenPhys);

        // Both rails along the whole path, including the cup section.
        Mesh rails = BuildRailsMesh(path);
        CreateMeshObject("Rails", root.transform, SaveMesh(rails, "Rails"), railMat, railPhys);

        // End caps.
        PathSample first = path[0];
        PathSample last = path[path.Count - 1];
        CreateWall("StartWall", root.transform, first.pos - first.dir * (RailThickness * 0.5f), first.dir, first.pos.y + RailHeight, railMat, railPhys);
        CreateWall("EndWall", root.transform, last.pos + last.dir * (RailThickness * 0.5f), last.dir, last.pos.y + RailHeight, railMat, railPhys);

        BuildCupSection(root.transform, path[cupStartIndex], last, greenMat, greenPhys, cupMat);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = layer;
            t.gameObject.isStatic = true;
        }

        // Obstacles from the ball test scene would sit on top of the course.
        GameObject cubes = GameObject.Find(CubesPath);
        if (cubes != null)
            cubes.SetActive(false);

        SetupBall(ballPhys, first);
        SetupCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Ball course built in " + ScenePath + " (path length " + path.Count + " samples).");
    }

    // ---------------------------------------------------------------- path
    static List<PathSample> BuildPath(out int cupStartIndex)
    {
        var samples = new List<PathSample>();
        Vector3 p = Vector3.zero;
        Vector3 dir = Vector3.forward;
        float h = TeeHeight;

        void Add() => samples.Add(new PathSample { pos = new Vector3(p.x, h, p.z), dir = dir });

        void Straight(float length, float heightTo)
        {
            float heightFrom = h;
            int n = Mathf.Max(1, Mathf.CeilToInt(length / SampleStep));
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                p += dir * (length / n);
                h = heightFrom + (heightTo - heightFrom) * (1f - Mathf.Cos(Mathf.PI * t)) * 0.5f;   // zero slope at both ends
                Add();
            }
        }

        void TurnRight(float radius, float degrees)
        {
            Vector3 right = RightOf(dir);
            Vector3 centre = p + right * radius;
            int n = Mathf.Max(1, Mathf.CeilToInt(radius * degrees * Mathf.Deg2Rad / SampleStep));
            Quaternion step = Quaternion.Euler(0f, degrees / n, 0f);   // positive Y = clockwise from above = right turn
            for (int i = 1; i <= n; i++)
            {
                p = centre + step * (p - centre);
                dir = (step * dir).normalized;
                Add();
            }
        }

        Add();
        Straight(TeeLength, TeeHeight);
        Straight(RampLength, BaseHeight);
        Straight(LoopSpotLength, BaseHeight);
        TurnRight(Turn1Radius, 90f);
        Straight(BackStraightLength, BaseHeight);
        TurnRight(UTurnRadius, 180f);
        Straight(ReturnLength, BaseHeight);
        cupStartIndex = samples.Count - 1;
        Straight(CupSectionLength, BaseHeight);
        return samples;
    }

    static Vector3 RightOf(Vector3 dir) => new Vector3(dir.z, 0f, -dir.x);

    // ---------------------------------------------------------------- meshes
    static Mesh BuildFloorMesh(List<PathSample> path, int endIndex)
    {
        var left = new List<Vector3>();
        var right = new List<Vector3>();
        var facing = new List<Vector3>();
        float hw = LaneWidth * 0.5f;
        for (int i = 0; i <= endIndex; i++)
        {
            Vector3 r = RightOf(path[i].dir);
            left.Add(path[i].pos - r * hw);
            right.Add(path[i].pos + r * hw);
            facing.Add(Vector3.up);
        }
        var v = new List<Vector3>();
        var t = new List<int>();
        AddStrip(v, t, left, right, facing);
        return MakeMesh("LaneFloor", v, t);
    }

    // Each rail: inner face (toward the lane), top, and outer face down to the ground.
    static Mesh BuildRailsMesh(List<PathSample> path)
    {
        var v = new List<Vector3>();
        var t = new List<int>();
        float hw = LaneWidth * 0.5f;
        foreach (float side in new[] { -1f, 1f })
        {
            var innerBottom = new List<Vector3>();
            var innerTop = new List<Vector3>();
            var outerTop = new List<Vector3>();
            var outerBottom = new List<Vector3>();
            var toLane = new List<Vector3>();
            var up = new List<Vector3>();
            var outward = new List<Vector3>();
            foreach (PathSample s in path)
            {
                Vector3 r = RightOf(s.dir) * side;
                Vector3 inner = s.pos + r * hw;
                Vector3 outer = s.pos + r * (hw + RailThickness);
                innerBottom.Add(inner);
                innerTop.Add(inner + Vector3.up * RailHeight);
                outerTop.Add(outer + Vector3.up * RailHeight);
                outerBottom.Add(new Vector3(outer.x, 0f, outer.z));
                toLane.Add(-r);
                up.Add(Vector3.up);
                outward.Add(r);
            }
            AddStrip(v, t, innerBottom, innerTop, toLane);
            AddStrip(v, t, innerTop, outerTop, up);
            AddStrip(v, t, outerTop, outerBottom, outward);
        }
        return MakeMesh("Rails", v, t);
    }

    // Flat rectangle (local X = width, local Z = length) with a round hole, at local y = 0.
    // Rays from the hole centre at sorted angles (including the four corners) pair a point on the
    // hole edge with a point on the rectangle edge, so the ring of quads covers the plate exactly.
    static Mesh BuildPlateWithHole(float width, float length, Vector2 hole, float radius, int segments)
    {
        float xMin = -width * 0.5f, xMax = width * 0.5f, zMin = -length * 0.5f, zMax = length * 0.5f;
        var angles = new List<float>();
        for (int k = 0; k < segments; k++)
            angles.Add(k * Mathf.PI * 2f / segments);
        Vector2[] corners = { new Vector2(xMin, zMin), new Vector2(xMax, zMin), new Vector2(xMax, zMax), new Vector2(xMin, zMax) };
        foreach (Vector2 c in corners)
        {
            float a = Mathf.Atan2(c.y - hole.y, c.x - hole.x);
            if (a < 0f) a += Mathf.PI * 2f;
            angles.Add(a);
        }
        angles.Sort();
        var unique = new List<float>();
        foreach (float a in angles)
            if (unique.Count == 0 || a - unique[unique.Count - 1] > 1e-4f)
                unique.Add(a);

        var v = new List<Vector3>();
        var t = new List<int>();
        foreach (float a in unique)
        {
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float hit = float.MaxValue;
            if (d.x > 1e-6f) hit = Mathf.Min(hit, (xMax - hole.x) / d.x);
            if (d.x < -1e-6f) hit = Mathf.Min(hit, (xMin - hole.x) / d.x);
            if (d.y > 1e-6f) hit = Mathf.Min(hit, (zMax - hole.y) / d.y);
            if (d.y < -1e-6f) hit = Mathf.Min(hit, (zMin - hole.y) / d.y);
            Vector2 inner = hole + d * radius;
            Vector2 edge = hole + d * hit;
            v.Add(new Vector3(inner.x, 0f, inner.y));
            v.Add(new Vector3(edge.x, 0f, edge.y));
        }
        int n = unique.Count;
        for (int k = 0; k < n; k++)
        {
            int k1 = (k + 1) % n;
            AddQuad(v, t, 2 * k, 2 * k1, 2 * k1 + 1, 2 * k + 1, Vector3.up);
        }
        return MakeMesh("CupPlate", v, t);
    }

    // Open cylinder (walls facing inward) plus a floor slightly above the ground to avoid z-fighting.
    static Mesh BuildCupMesh(float radius, float depth, int segments)
    {
        var v = new List<Vector3>();
        var t = new List<int>();
        for (int k = 0; k < segments; k++)
        {
            float a0 = k * Mathf.PI * 2f / segments;
            float a1 = (k + 1) * Mathf.PI * 2f / segments;
            Vector3 r0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
            Vector3 r1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            int i = v.Count;
            v.Add(r0); v.Add(r1); v.Add(r1 + Vector3.down * depth); v.Add(r0 + Vector3.down * depth);
            Vector3 mid = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
            AddQuad(v, t, i, i + 1, i + 2, i + 3, -mid);
        }
        float floorY = -depth + 0.005f;
        int centre = v.Count;
        v.Add(new Vector3(0f, floorY, 0f));
        for (int k = 0; k < segments; k++)
        {
            float a = k * Mathf.PI * 2f / segments;
            v.Add(new Vector3(Mathf.Cos(a) * radius, floorY, Mathf.Sin(a) * radius));
        }
        for (int k = 0; k < segments; k++)
            AddTri(v, t, centre, centre + 1 + k, centre + 1 + (k + 1) % segments, Vector3.up);
        return MakeMesh("Cup", v, t);
    }

    // ---------------------------------------------------------------- objects
    static void BuildCupSection(Transform root, PathSample start, PathSample end, Material greenMat, PhysicMaterial greenPhys, Material cupMat)
    {
        Vector3 centre = (start.pos + end.pos) * 0.5f;
        GameObject section = new GameObject("CupSection");
        section.transform.SetParent(root, false);
        section.transform.position = new Vector3(centre.x, BaseHeight, centre.z);
        section.transform.rotation = Quaternion.LookRotation(start.dir, Vector3.up);

        Vector2 holeLocal = new Vector2(0f, CupSectionLength * 0.5f - CupFromEnd);   // local Z points toward the end wall
        Mesh plate = BuildPlateWithHole(LaneWidth, CupSectionLength, holeLocal, CupRadius, CupSegments);
        CreateMeshObject("Plate", section.transform, SaveMesh(plate, "CupPlate"), greenMat, greenPhys);

        GameObject cup = CreateMeshObject("Cup", section.transform, SaveMesh(BuildCupMesh(CupRadius, CupDepth, CupSegments), "Cup"), cupMat, greenPhys);
        cup.transform.localPosition = new Vector3(holeLocal.x, 0f, holeLocal.y);

        GameObject trigger = new GameObject("CupTrigger");
        trigger.transform.SetParent(cup.transform, false);
        trigger.transform.localPosition = new Vector3(0f, -CupDepth + 0.03f, 0f);
        BoxCollider box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(CupRadius * 1.4f, 0.04f, CupRadius * 1.4f);
        trigger.AddComponent<BallCupTrigger>();
    }

    static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material mat, PhysicMaterial phys)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        MeshCollider col = go.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        col.sharedMaterial = phys;
        return go;
    }

    // A box across the lane from the ground up to 'topY', centred on 'centre' (x/z) and facing 'dir'.
    static void CreateWall(string name, Transform parent, Vector3 centre, Vector3 dir, float topY, Material mat, PhysicMaterial phys)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = new Vector3(centre.x, topY * 0.5f, centre.z);
        wall.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        wall.transform.localScale = new Vector3(LaneWidth + 2f * RailThickness, topY, RailThickness);
        wall.GetComponent<Renderer>().sharedMaterial = mat;
        wall.GetComponent<Collider>().sharedMaterial = phys;
    }

    // ---------------------------------------------------------------- ball & camera
    static void SetupBall(PhysicMaterial ballPhys, PathSample tee)
    {
        GameObject ball = GameObject.Find(BallPath);
        if (ball == null)
        {
            Debug.LogWarning("'" + BallPath + "' not found - ball not configured.");
            return;
        }
        float diameter = BallRadius * 2f;
        ball.transform.localScale = Vector3.one * diameter;   // the sphere mesh has radius 0.5
        ball.transform.position = tee.pos + tee.dir * 0.6f + Vector3.up * (BallRadius + 0.002f);

        // Solid collider = the real ball; trigger collider = a bigger, invisible click target.
        SphereCollider solid = null, click = null;
        foreach (SphereCollider c in ball.GetComponents<SphereCollider>())
        {
            if (c.isTrigger) click = c;
            else if (solid == null) solid = c;
        }
        if (solid == null) solid = ball.AddComponent<SphereCollider>();
        solid.radius = 0.5f;
        solid.sharedMaterial = ballPhys;
        if (click == null) click = ball.AddComponent<SphereCollider>();
        click.isTrigger = true;
        click.radius = ClickRadius / diameter;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = BallMass;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // 4 cm ball can move > its own size per step
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        if (ball.GetComponent<GolfBallPhysics>() == null)
            ball.AddComponent<GolfBallPhysics>();

        Ball ballScript = ball.GetComponent<Ball>();
        if (ballScript != null)
        {
            var so = new SerializedObject(ballScript);
            SerializedProperty speed = so.FindProperty("ballSpeed");
            if (speed != null)
            {
                speed.floatValue = BallShotStrength;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    static void SetupCamera()
    {
        GameObject cam = GameObject.Find(CameraPath);
        if (cam == null)
        {
            Debug.LogWarning("'" + CameraPath + "' not found - camera distance not changed.");
            return;
        }
        cam.transform.localPosition = new Vector3(0f, 0f, -CameraDistance);
    }

    // ---------------------------------------------------------------- helpers
    // Adds a band of quads between two rows of points; quad i faces facing[i].
    static void AddStrip(List<Vector3> v, List<int> t, List<Vector3> rowA, List<Vector3> rowB, List<Vector3> facing)
    {
        int start = v.Count;
        for (int i = 0; i < rowA.Count; i++)
        {
            v.Add(rowA[i]);
            v.Add(rowB[i]);
        }
        for (int i = 0; i < rowA.Count - 1; i++)
        {
            int a = start + 2 * i, b = start + 2 * (i + 1), c = start + 2 * (i + 1) + 1, d = start + 2 * i + 1;
            AddQuad(v, t, a, b, c, d, facing[i]);
        }
    }

    // a-b-c-d go around the quad. The winding is chosen so the face points toward 'facing'
    // (Unity's front face is where Cross(b - a, c - a) points).
    static void AddQuad(List<Vector3> v, List<int> t, int a, int b, int c, int d, Vector3 facing)
    {
        if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), facing) >= 0f)
        {
            t.Add(a); t.Add(b); t.Add(c);
            t.Add(a); t.Add(c); t.Add(d);
        }
        else
        {
            t.Add(a); t.Add(c); t.Add(b);
            t.Add(a); t.Add(d); t.Add(c);
        }
    }

    static void AddTri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 facing)
    {
        if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), facing) >= 0f) { t.Add(a); t.Add(b); t.Add(c); }
        else { t.Add(a); t.Add(c); t.Add(b); }
    }

    static Mesh MakeMesh(string name, List<Vector3> v, List<int> t)
    {
        Mesh mesh = new Mesh { name = name };
        if (v.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = AssetFolder + "/Meshes/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Material GetMaterial(string name, Color color)
    {
        string path = AssetFolder + "/Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static PhysicMaterial GetPhysicMaterial(string name, float friction, float bounce)
    {
        string path = AssetFolder + "/Physics/" + name + ".physicMaterial";
        PhysicMaterial mat = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
        if (mat == null)
        {
            mat = new PhysicMaterial(name);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.dynamicFriction = friction;
        mat.staticFriction = friction;
        mat.bounciness = bounce;
        mat.frictionCombine = PhysicMaterialCombine.Average;
        mat.bounceCombine = PhysicMaterialCombine.Average;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
