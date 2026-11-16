using System.Collections.Generic;
using UnityEngine;

// Draws this object (every child MeshRenderer, every sub-mesh) as a glowing silhouette wherever
// something else hides it. Needed for multi-material models like the car: adding a second material to
// a renderer only covers its last sub-mesh, so the parts are drawn again with Graphics.DrawMesh.
// A stencil mask pass first marks where the object is visible, so parts hidden only by the object
// itself (far wheels behind the body) don't glow.
//
// Cost: with a baked mesh (all parts merged once in the editor, in this object's local space) the whole
// object is 2 extra draw calls per frame. The Hummer has 65 parts / 85 sub-meshes, which would
// otherwise be 170. Baking happens in the editor because merging at runtime needs Read/Write enabled on
// the model, which costs memory. Re-bake (right-click this component > Bake X-ray mesh) if the model
// changes. A baked mesh is only valid while no part moves relative to this object; leave it empty if
// parts animate (e.g. spinning wheels) - then it draws each sub-mesh separately.
//
// Also used by OcclusionWindow (on the camera): See Through Radius sets the size of the see-through
// circle opened in surfaces that hide this piece, and the piece's colliders decide when it counts as hidden.
public class XRaySilhouette : MonoBehaviour
{
    [SerializeField] Material xrayMaterial;   // MiniCarGolf/BallXRay, stencil compare NotEqual
    [SerializeField] Material maskMaterial;   // MiniCarGolf/XRayMask, same stencil ref as xrayMaterial
    [SerializeField] Mesh bakedMesh;          // optional: all parts merged (see above)
    [Header("See-through window")]
    [SerializeField, Min(0f)] float seeThroughRadius = 0.12f;   // metres: radius of the see-through circle around this piece

    struct Part
    {
        public MeshFilter filter;
        public Renderer renderer;
        public int subMeshes;
    }

    static readonly List<XRaySilhouette> active = new List<XRaySilhouette>();
    readonly List<Part> parts = new List<Part>();
    Collider[] colliders;

    public static XRaySilhouette FirstActive => active.Count > 0 ? active[0] : null;
    public float SeeThroughRadius => seeThroughRadius;

    // World bounds of the piece's solid colliders (body, wheels / ball), falling back to its renderers.
    // Colliders are used because renderer bounds include things like the car's tall antennas.
    public Bounds WorldBounds
    {
        get
        {
            bool found = false;
            Bounds b = new Bounds(transform.position, Vector3.zero);
            foreach (Collider c in colliders)
            {
                if (c == null || !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy)
                    continue;
                if (found) b.Encapsulate(c.bounds);
                else { b = c.bounds; found = true; }
            }
            if (!found)
            {
                foreach (Part p in parts)
                {
                    if (p.renderer == null || !p.renderer.enabled)
                        continue;
                    if (found) b.Encapsulate(p.renderer.bounds);
                    else { b = p.renderer.bounds; found = true; }
                }
            }
            return b;
        }
    }

    void OnEnable()
    {
        parts.Clear();
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter f = r.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null)
                parts.Add(new Part { filter = f, renderer = r, subMeshes = f.sharedMesh.subMeshCount });
        }
        colliders = GetComponentsInChildren<Collider>(true);
        active.Add(this);
    }

    void OnDisable()
    {
        active.Remove(this);
    }

    // LateUpdate so transforms are final for this frame.
    void LateUpdate()
    {
        if (xrayMaterial == null || maskMaterial == null)
            return;

        if (bakedMesh != null)
        {
            Matrix4x4 m = transform.localToWorldMatrix;
            Graphics.DrawMesh(bakedMesh, m, maskMaterial, gameObject.layer, null, 0, null, false, false);
            Graphics.DrawMesh(bakedMesh, m, xrayMaterial, gameObject.layer, null, 0, null, false, false);
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
                Graphics.DrawMesh(p.filter.sharedMesh, matrix, xrayMaterial, gameObject.layer, null, s, null, false, false);
            }
        }
    }

    // Merges every enabled MeshRenderer part into one single-sub-mesh mesh in this object's local space.
    // Editor use: meshes without Read/Write can still be read in the editor.
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
        return mesh;
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
