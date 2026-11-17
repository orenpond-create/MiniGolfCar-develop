using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Draws a constant-width outline (in screen pixels) around this piece wherever something else hides it,
// like the hidden-ball outline in Golf With Your Friends. Same look for the ball and the car.
//
// Each frame:
//   1. maskMaterial (XRayMask) marks the piece's whole silhouette in the stencil buffer.
//   2. outlineMaterial (XRayOutline) draws a slightly inflated copy, only OUTSIDE that silhouette and only
//      where something is in front of it - a ring around the hidden piece, nothing when it's visible.
// Colour, width (pixels) and the hidden threshold are set on the outline material.
//
// Multi-material models (the car: 65 parts / 85 sub-meshes) use a baked mesh - all parts merged once in the
// editor, with smoothed normals in UV4 so the outline doesn't break at sharp corners - so the whole piece
// costs 2 extra draw calls. Baking is done in the editor because merging at runtime needs Read/Write on
// the model. Re-bake (right-click this component > Bake X-ray mesh) if the model changes. A baked mesh is
// only valid while no part moves relative to this object.
// Leave Baked Mesh empty for single smooth meshes (the ball): then each sub-mesh is drawn directly.
public class XRaySilhouette : MonoBehaviour
{
    [FormerlySerializedAs("xrayMaterial")]
    [SerializeField] Material outlineMaterial;   // MiniCarGolf/XRayOutline
    [SerializeField] Material maskMaterial;      // MiniCarGolf/XRayMask, same stencil ref as the outline
    [SerializeField] Mesh bakedMesh;             // optional: all parts merged (see above)

    struct Part
    {
        public MeshFilter filter;
        public Renderer renderer;
        public int subMeshes;
    }

    readonly List<Part> parts = new List<Part>();

    void OnEnable()
    {
        parts.Clear();
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter f = r.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null)
                parts.Add(new Part { filter = f, renderer = r, subMeshes = f.sharedMesh.subMeshCount });
        }
    }

    // LateUpdate so transforms are final for this frame.
    void LateUpdate()
    {
        if (outlineMaterial == null || maskMaterial == null)
            return;

        if (bakedMesh != null)
        {
            Matrix4x4 m = transform.localToWorldMatrix;
            Graphics.DrawMesh(bakedMesh, m, maskMaterial, gameObject.layer, null, 0, null, false, false);
            Graphics.DrawMesh(bakedMesh, m, outlineMaterial, gameObject.layer, null, 0, null, false, false);
            return;
        }

        foreach (Part p in parts)
        {
            if (p.renderer == null || !p.renderer.enabled || !p.renderer.gameObject.activeInHierarchy)
                continue;
            Matrix4x4 matrix = p.filter.transform.localToWorldMatrix;
            for (int s = 0; s < p.subMeshes; s++)
            {
                Graphics.DrawMesh(p.filter.sharedMesh, matrix, maskMaterial, gameObject.layer, null, s, null, false, false);
                Graphics.DrawMesh(p.filter.sharedMesh, matrix, outlineMaterial, gameObject.layer, null, s, null, false, false);
            }
        }
    }

    // Merges every enabled MeshRenderer part into one single-sub-mesh mesh in this object's local space,
    // with smoothed normals in UV4 for the outline. Editor use: meshes without Read/Write can still be read there.
    public Mesh BuildCombinedMesh()
    {
        var instances = new List<CombineInstance>();
        Matrix4x4 toLocal = transform.worldToLocalMatrix;
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(false))
        {
            MeshFilter f = r.GetComponent<MeshFilter>();
            if (!r.enabled || f == null || f.sharedMesh == null)
                continue;
            Matrix4x4 m = toLocal * f.transform.localToWorldMatrix;
            for (int s = 0; s < f.sharedMesh.subMeshCount; s++)
                instances.Add(new CombineInstance { mesh = f.sharedMesh, subMeshIndex = s, transform = m });
        }
        var mesh = new Mesh { name = name + " x-ray", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.CombineMeshes(instances.ToArray(), true, true);
        mesh.RecalculateBounds();
        mesh.SetUVs(3, SmoothNormals(mesh));
        return mesh;
    }

    // Averages the normals of all vertices sharing a position (hard edges split them into several vertices),
    // so the inflated outline stays closed at the model's corners.
    static List<Vector3> SmoothNormals(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        var sums = new Dictionary<Vector3Int, Vector3>();
        var keys = new Vector3Int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = Vector3Int.RoundToInt(vertices[i] * 10000f);   // 0.1 mm buckets
            keys[i] = key;
            sums.TryGetValue(key, out Vector3 sum);
            sums[key] = sum + normals[i];
        }
        var result = new List<Vector3>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 n = sums[keys[i]];
            result.Add(n.sqrMagnitude > 1e-12f ? n.normalized : normals[i]);
        }
        return result;
    }

#if UNITY_EDITOR
    [ContextMenu("Bake X-ray mesh")]
    void BakeFromMenu()
    {
        string path = UnityEditor.EditorUtility.SaveFilePanelInProject("Save x-ray mesh", name + "_XRay", "asset", "Where to save the merged x-ray mesh");
        if (string.IsNullOrEmpty(path))
            return;
        Mesh mesh = BuildCombinedMesh();
        UnityEditor.AssetDatabase.CreateAsset(mesh, path);
        UnityEditor.Undo.RecordObject(this, "Bake X-ray mesh");
        bakedMesh = mesh;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
