using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// One-click builder that generates a correctly-configured CarReal prefab.
// Run:  Tools > MiniCarGolf > Build CarReal Prefab
//
// Everything is built with clean, uniform scales and simple round numbers so the raycast
// suspension geometry is self-consistent (no nested non-uniform scales to reason about).
// Output: Assets/Prefabs/CarReal.prefab
public static class CarRealPrefabBuilder
{
    const string PrefabPath = "Assets/Prefabs/CarReal.prefab";

    // --- Geometry / tuning knobs (all in the car's local space, ground assumed below) ---
    //
    // Two rules make this arrangement fail-safe:
    //   1. RayHeight (0.5) is ABOVE the box collider's bottom (0.3). So even if the car somehow
    //      rests on its box, the ray origins stay above the ground, still hit, and push it back
    //      up. The old layout had the rays BELOW the box bottom, which let the ray origins slip
    //      under the floor -> isGrounded stuck false -> no drive force, forever.
    //   2. The rays sit at X = +/-0.7, outside the box's X half-width (0.5), so they can never
    //      self-hit the body collider regardless of height.
    const float WheelRadius = 0.25f;
    const float RayHeight = 0.5f;              // local Y of the ray origins
    const float SuspensionRestLength = 0.9f;   // ray reach downward from RayHeight
    const float SpringStrength = 120f;
    const float DamperStrength = 18f;
    const float MaxSpringAccel = 25f;          // per-wheel ceiling; stops drop-catapulting
    const float RotateSpeed = 90f;             // deg/sec. The old value of 3 steered at 3 deg/SEC (~30s per 90 turn).

    static readonly Vector3 BoxCenter = new Vector3(0f, 0.5f, 0f);
    static readonly Vector3 BoxSize = new Vector3(1.0f, 0.4f, 2.0f);   // bottom at local Y 0.3

    [MenuItem("Tools/MiniCarGolf/Build CarReal Prefab")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        // --- Root: Rigidbody + body Box Collider + the CarReal script ---
        GameObject root = new GameObject("CarReal");

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 5f;
        rb.drag = 0f;
        rb.angularDrag = 0.05f;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.None;   // tilt is clamped in code, not frozen

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = BoxCenter;                       // rides above the wheels, never touches ground
        box.size = BoxSize;

        // --- Body visual (Cube, collider stripped) ---
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "BodyMesh";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = BoxCenter;
        body.transform.localScale = BoxSize;

        // --- Four wheels + their suspension ray points ---
        // Ray points sit at RayHeight, above the box bottom and outside it in X (see notes above).
        // Wheels hang below and are repositioned onto the ground by ApplySuspension at runtime.
        Vector3[] corners =
        {
            new Vector3(-0.7f, RayHeight,  0.9f),   // front-left
            new Vector3( 0.7f, RayHeight,  0.9f),   // front-right
            new Vector3(-0.7f, RayHeight, -0.9f),   // rear-left
            new Vector3( 0.7f, RayHeight, -0.9f),   // rear-right
        };

        List<Transform> rayPoints = new List<Transform>();
        List<Transform> wheelMeshes = new List<Transform>();

        for (int i = 0; i < corners.Length; i++)
        {
            GameObject rayPoint = new GameObject("RayPoint" + (i + 1));
            rayPoint.transform.SetParent(root.transform, false);
            rayPoint.transform.localPosition = corners[i];
            rayPoints.Add(rayPoint.transform);

            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            wheel.name = "Wheel" + (i + 1);
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
            wheel.transform.SetParent(root.transform, false);
            wheel.transform.localPosition = corners[i] + Vector3.down * (SuspensionRestLength - WheelRadius);
            // Thin along X (the axle), round in Y/Z -> looks like a wheel and matches SpinWheels' X axis.
            wheel.transform.localScale = new Vector3(0.2f, WheelRadius * 2f, WheelRadius * 2f);
            wheelMeshes.Add(wheel.transform);
        }

        // --- CarReal script: set every serialized field via SerializedObject (fields are private) ---
        CarReal car = root.AddComponent<CarReal>();
        SerializedObject so = new SerializedObject(car);

        SetFloat(so, "acceleration", 20f);
        SetFloat(so, "maxVelocity", 10f);
        SetFloat(so, "rotateSpeed", RotateSpeed);
        SetFloat(so, "maxNoseDownAngle", 45f);
        SetFloat(so, "maxRollAngle", 35f);
        SetFloat(so, "suspensionRestLength", SuspensionRestLength);
        SetFloat(so, "wheelRadius", WheelRadius);
        SetFloat(so, "springStrength", SpringStrength);
        SetFloat(so, "damperStrength", DamperStrength);
        SetFloat(so, "maxSpringAccel", MaxSpringAccel);

        // groundMask: everything by default. The ray origins sit BELOW the body box and the wheels
        // have no colliders, so the rays can't self-hit. Narrow this to a "Ground" layer if you like.
        SerializedProperty maskProp = so.FindProperty("groundMask");
        if (maskProp != null)
            maskProp.intValue = ~0;

        SetTransformArray(so, "wheelRayPoints", rayPoints);
        SetTransformArray(so, "wheelMeshes", wheelMeshes);

        so.ApplyModifiedPropertiesWithoutUndo();

        // --- Save as prefab, clean up the temporary scene object ---
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Built CarReal prefab at " + PrefabPath +
                      ". Drag it into the scene, ~0.6 units above the ground.");
        }
        else
        {
            Debug.LogError("Failed to build CarReal prefab.");
        }
    }

    static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null)
            p.floatValue = value;
        else
            Debug.LogWarning("CarReal field not found: " + name);
    }

    static void SetTransformArray(SerializedObject so, string name, List<Transform> items)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("CarReal field not found: " + name);
            return;
        }
        p.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
