using UnityEngine;

// Opens a see-through window (SeeThroughRail shader) in surfaces between the camera and the player's
// piece (ball or car) - but only while the piece is actually hidden, not just close to a surface.
// Put it on the camera that renders the game. The window radius is set per piece on its
// XRaySilhouette component (See Through Radius).
//
// Hidden test: a few rays from the camera to points inside the piece's colliders; the piece counts as
// hidden when at least hiddenFraction of them hit something else first. Cost: 5 raycasts per frame.
[RequireComponent(typeof(Camera))]
public class OcclusionWindow : MonoBehaviour
{
    [SerializeField] XRaySilhouette target;                        // empty = the first active XRaySilhouette in the scene
    [SerializeField, Range(0f, 1f)] float hiddenFraction = 0.5f;   // share of the 5 sample rays that must be blocked
    [SerializeField] float fadeTime = 0.15f;                       // seconds for the window to open / close
    [SerializeField] LayerMask occluderLayers = ~0;                // what can hide the piece

    static readonly int BallScreenId = Shader.PropertyToID("_BallScreen");
    static readonly int AspectId = Shader.PropertyToID("_BallScreenAspect");

    readonly Vector3[] samples = new Vector3[5];
    Camera cam;
    float openAmount;   // 0 = closed, 1 = fully open

    // For the ball <-> car transform later: set this to whichever piece is active.
    public XRaySilhouette Target
    {
        get => target;
        set => target = value;
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    // OnPreCull runs after every LateUpdate (so after Cinemachine has moved the camera)
    // and right before this camera renders.
    void OnPreCull()
    {
        XRaySilhouette piece = (target != null && target.isActiveAndEnabled) ? target : XRaySilhouette.FirstActive;
        float goal = (piece != null && IsHidden(piece)) ? 1f : 0f;
        openAmount = Mathf.MoveTowards(openAmount, goal, fadeTime > 0f ? Time.deltaTime / fadeTime : 1f);

        if (piece == null || openAmount <= 0f)
        {
            Shader.SetGlobalVector(BallScreenId, Vector4.zero);
            return;
        }
        Vector3 centre = piece.WorldBounds.center;
        Vector3 viewport = cam.WorldToViewportPoint(centre);   // z = eye depth in metres
        if (viewport.z <= 0f)
        {
            Shader.SetGlobalVector(BallScreenId, Vector4.zero);
            return;
        }
        Vector3 edge = cam.WorldToViewportPoint(centre + cam.transform.right * piece.SeeThroughRadius);
        float radius = Mathf.Abs(edge.x - viewport.x) * cam.aspect * openAmount;   // viewport-height units, matches the shader
        Shader.SetGlobalVector(BallScreenId, new Vector4(viewport.x, viewport.y, viewport.z, radius));
        Shader.SetGlobalFloat(AspectId, cam.aspect);
    }

    // Clear after rendering so other cameras (e.g. the Scene view) don't show the window.
    void OnPostRender()
    {
        Shader.SetGlobalVector(BallScreenId, Vector4.zero);
    }

    void OnDisable()
    {
        Shader.SetGlobalVector(BallScreenId, Vector4.zero);
    }

    // Centre plus four points spread across the piece as the camera sees it (60% of its extent along the
    // camera's right / up). A ray that reaches the piece's own collider first means that point is visible.
    bool IsHidden(XRaySilhouette piece)
    {
        Bounds b = piece.WorldBounds;
        Vector3 c = b.center;
        Vector3 right = cam.transform.right;
        Vector3 up = cam.transform.up;
        float alongRight = 0.6f * (Mathf.Abs(right.x) * b.extents.x + Mathf.Abs(right.y) * b.extents.y + Mathf.Abs(right.z) * b.extents.z);
        float alongUp = 0.6f * (Mathf.Abs(up.x) * b.extents.x + Mathf.Abs(up.y) * b.extents.y + Mathf.Abs(up.z) * b.extents.z);
        samples[0] = c;
        samples[1] = c + right * alongRight;
        samples[2] = c - right * alongRight;
        samples[3] = c + up * alongUp;
        samples[4] = c - up * alongUp;

        Vector3 origin = cam.transform.position;
        int blocked = 0;
        foreach (Vector3 p in samples)
        {
            Vector3 toPoint = p - origin;
            float dist = toPoint.magnitude;
            if (dist < 1e-4f)
                continue;
            if (Physics.Raycast(origin, toPoint / dist, out RaycastHit hit, dist, occluderLayers, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(piece.transform))
                blocked++;
        }
        return blocked >= Mathf.Max(1, Mathf.CeilToInt(hiddenFraction * samples.Length));
    }
}
