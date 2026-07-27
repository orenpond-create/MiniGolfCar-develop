using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// CarReal = Option C test variant, using the *lighter* raycast-suspension approach
// (not Unity WheelColliders). Per wheel: raycast straight down and apply a spring + damper
// force that holds the chassis up, so the body rides on its wheels over terrain.
//
// The DRIVE, STEER and TILT-CLAMP are intentionally the same arcade model as Car.cs so it
// feels as close to carA as possible; only the ground support differs (springs instead of a
// body box resting on the floor).
//
// Scene setup (see checklist):
//   - Body: Box (or convex mesh) Collider raised so it does NOT touch the ground.
//   - wheelRayPoints: one empty Transform per wheel at the TOP of that wheel's suspension travel.
//   - groundMask: set to the ground layer(s) only, so the down-rays never hit the car itself.
//   - Rigidbody: Use Gravity ON, Interpolate ON.
// Self-contained on purpose (no shared base logic) so this option can be lifted out on its own later.
public class CarReal : Vehicle
{
    #region Fields
    [Header("Drive (kept identical to carA for matching feel)")]
    [SerializeField] float acceleration = 20f;
    [SerializeField] float maxVelocity = 10f;
    [SerializeField] float rotateSpeed = 3f;
    [SerializeField] float maxNoseDownAngle = 45f;   // nose can't point down more than this many degrees below horizon
    [SerializeField] float maxRollAngle = 35f;       // car can't bank more than this many degrees to either side

    [Header("Raycast suspension")]
    [SerializeField] List<Transform> wheelRayPoints = new List<Transform>();  // ray origins, one per wheel (top of travel)
    [SerializeField] List<Transform> wheelMeshes = new List<Transform>();     // optional visuals to reposition (same order)
    [SerializeField] LayerMask groundMask = ~0;      // set to ground layer(s) ONLY, excluding the car
    [SerializeField] float suspensionRestLength = 0.6f;   // ray length / max droop from ray point to contact
    [SerializeField] float wheelRadius = 0.3f;            // used to place the wheel mesh on the contact point
    [SerializeField] float springStrength = 100f;         // per-wheel spring accel (multiplied by mass internally)
    [SerializeField] float damperStrength = 12f;          // per-wheel damping accel (multiplied by mass internally)
    [SerializeField] float maxSpringAccel = 25f;          // ceiling per wheel, so a hard landing can't launch the car

    Rigidbody myRigidBody;
    Collider myCollider;
    Transform myTransform;
    float rotateDirection = 0f;
    bool isGrounded = false;
    #endregion

    #region Methods
    private void Awake()
    {
        myRigidBody = GetComponentInChildren<Rigidbody>();
        myCollider = GetComponentInChildren<Collider>();
        myTransform = gameObject.transform;
    }

    private void FixedUpdate()
    {
        rotateDirection = 0;
        if (Input.GetMouseButton(0))
            rotateDirection = -1;
        if (Input.GetMouseButton(1))
            rotateDirection = 1;

        ApplySuspension();
        Move();
        SpinWheels();
    }

    // Purely cosmetic: rolls the wheel meshes at the rate they'd turn for the current speed.
    // Spins around the car's right axis (the axle direction), so it doesn't matter how each
    // wheel mesh's own local axes are oriented. If a wheel spins the wrong way, negate the angle.
    private void SpinWheels()
    {
        if (wheelRadius <= 0f)
            return;

        float forwardSpeed = Vector3.Dot(myRigidBody.velocity, myTransform.forward);   // signed: reverses when backing up
        float degreesThisStep = (forwardSpeed / wheelRadius) * Mathf.Rad2Deg * Time.fixedDeltaTime;

        for (int i = 0; i < wheelMeshes.Count; i++)
        {
            if (wheelMeshes[i] != null)
                wheelMeshes[i].Rotate(myTransform.right, degreesThisStep, Space.World);
        }
    }

    // Raycast spring per wheel: pushes the chassis up so it rides on its wheels.
    private void ApplySuspension()
    {
        int grounded = 0;
        Vector3 up = myTransform.up;

        for (int i = 0; i < wheelRayPoints.Count; i++)
        {
            Transform rayPoint = wheelRayPoints[i];
            if (rayPoint == null)
                continue;

            if (Physics.Raycast(rayPoint.position, -up, out RaycastHit hit, suspensionRestLength, groundMask))
            {
                grounded++;

                // Spring: force proportional to compression; Damper: opposes the wheel's vertical speed.
                // Multiplying by mass makes the tuning independent of the Rigidbody's mass.
                float compression = suspensionRestLength - hit.distance;               // > 0 when compressed
                Vector3 wheelVelocity = myRigidBody.GetPointVelocity(rayPoint.position);
                float springVelocity = Vector3.Dot(up, wheelVelocity);

                // Clamped to [0, maxSpringAccel]. The lower bound stops a strong damper from ever
                // pulling the car DOWN into the ground; the upper bound stops a big drop from
                // over-compressing the spring and catapulting it.
                float springAccel = compression * springStrength - springVelocity * damperStrength;
                springAccel = Mathf.Clamp(springAccel, 0f, maxSpringAccel);
                myRigidBody.AddForceAtPosition(up * (springAccel * myRigidBody.mass), rayPoint.position);

                // Place the wheel visual on the ground contact.
                if (i < wheelMeshes.Count && wheelMeshes[i] != null)
                    wheelMeshes[i].position = hit.point + up * wheelRadius;
            }
            else if (i < wheelMeshes.Count && wheelMeshes[i] != null)
            {
                // Airborne: drop the wheel to full droop.
                wheelMeshes[i].position = rayPoint.position - up * (suspensionRestLength - wheelRadius);
            }
        }

        isGrounded = grounded > 0;
    }

    private void Move()
    {
        // Steer via physics so rotation respects collisions instead of teleporting the transform.
        // Build the steered target, clamp its tilt, then apply it in a single MoveRotation.
        Quaternion yaw = Quaternion.Euler(0f, rotateSpeed * Time.fixedDeltaTime * rotateDirection, 0f);
        Quaternion targetRotation = ClampTilt(myRigidBody.rotation * yaw);
        myRigidBody.MoveRotation(targetRotation);

        // Only drive while at least one wheel is on the ground (carA is always grounded, so on flat
        // ground this behaves the same). ForceMode.Acceleration integrates the step internally.
        if (isGrounded)
            myRigidBody.AddForce(myTransform.forward * acceleration, ForceMode.Acceleration);

        // Clamp only horizontal speed; keep the Y component (gravity) and the current heading (momentum).
        Vector3 velocity = myRigidBody.velocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        if (horizontalVelocity.sqrMagnitude > (maxVelocity * maxVelocity))
        {
            horizontalVelocity = horizontalVelocity.normalized * maxVelocity;
            myRigidBody.velocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);
        }
    }

    // Clamps how far the car can tilt, without ever fully locking either axis:
    //   X (pitch): nose can't point down past maxNoseDownAngle (free to come back up).
    //   Z (roll):  can't bank past maxRollAngle to either side (free to level out).
    // At each limit it also cancels only the angular velocity that would push further past it,
    // so the car holds at the edge but can always rotate back toward level. Returns the clamped
    // rotation for the caller to apply; angular velocity is adjusted here directly.
    private Quaternion ClampTilt(Quaternion rotation)
    {
        Vector3 angularVelocity = myRigidBody.angularVelocity;

        // --- Pitch (X axis): limit nose-DOWN only ---
        Vector3 forward = rotation * Vector3.forward;
        float pitchDown = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;   // positive = nose down
        if (pitchDown > maxNoseDownAngle)
        {
            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z).normalized;
            float rad = maxNoseDownAngle * Mathf.Deg2Rad;
            Vector3 desiredForward = flatForward * Mathf.Cos(rad) + Vector3.down * Mathf.Sin(rad);
            rotation = Quaternion.FromToRotation(forward, desiredForward) * rotation;

            Vector3 carRight = rotation * Vector3.right;                    // the pitch axis
            float noseDownRate = Vector3.Dot(angularVelocity, carRight);    // > 0 means pitching nose-down
            if (noseDownRate > 0f)
                angularVelocity -= carRight * noseDownRate;
        }

        // --- Roll (Z axis): limit banking to EITHER side ---
        Vector3 right = rotation * Vector3.right;
        float rollAngle = Mathf.Asin(Mathf.Clamp(right.y, -1f, 1f)) * Mathf.Rad2Deg;       // signed lean
        if (Mathf.Abs(rollAngle) > maxRollAngle)
        {
            float clampedRad = Mathf.Sign(rollAngle) * maxRollAngle * Mathf.Deg2Rad;
            Vector3 flatRight = new Vector3(right.x, 0f, right.z).normalized;
            Vector3 desiredRight = flatRight * Mathf.Cos(clampedRad) + Vector3.up * Mathf.Sin(clampedRad);
            rotation = Quaternion.FromToRotation(right, desiredRight) * rotation;

            Vector3 carForward = rotation * Vector3.forward;               // the roll axis
            float rollRate = Vector3.Dot(angularVelocity, carForward);
            if (rollRate * rollAngle > 0f)                                 // same sign => rolling further past the limit
                angularVelocity -= carForward * rollRate;
        }

        myRigidBody.angularVelocity = angularVelocity;
        return rotation;
    }

    // Visualise the suspension rays in the editor for easier setup/tuning.
    private void OnDrawGizmosSelected()
    {
        if (wheelRayPoints == null)
            return;

        Gizmos.color = Color.yellow;
        foreach (Transform rayPoint in wheelRayPoints)
        {
            if (rayPoint == null)
                continue;
            Vector3 down = -(Application.isPlaying ? transform.up : rayPoint.up);
            Gizmos.DrawLine(rayPoint.position, rayPoint.position + down * suspensionRestLength);
        }
    }
    #endregion
}
