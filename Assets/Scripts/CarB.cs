using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// CarB = Option B test variant.
// The DRIVING is intentionally identical to Car.cs (arcade: AddForce + MoveRotation yaw), so it
// should feel the same as carA. The difference lives entirely in the SCENE collider setup:
//   - Body: a Box Collider raised so it does NOT touch the ground.
//   - Wheels: a Sphere Collider on each wheel, which becomes the actual ground contact.
// Self-contained on purpose (no shared base logic) so this option can be lifted out on its own later.
public class CarB : Vehicle
{
    #region Fields
    [SerializeField] float acceleration = 20f;
    [SerializeField] float maxVelocity = 10f;
    [SerializeField] float rotateSpeed = 3f;
    [SerializeField] float maxNoseDownAngle = 45f;   // nose can't point down more than this many degrees below horizon
    [SerializeField] float maxRollAngle = 35f;       // car can't bank more than this many degrees to either side
    Rigidbody myRigidBody;
    Collider myCollider;
    Transform myTransform;
    float rotateDirection = 0f;
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

        Move();
    }

    private void Move()
    {
        // Steer via physics so rotation respects collisions instead of teleporting the transform.
        // Build the steered target, clamp its tilt, then apply it in a single MoveRotation.
        Quaternion yaw = Quaternion.Euler(0f, rotateSpeed * Time.fixedDeltaTime * rotateDirection, 0f);
        Quaternion targetRotation = ClampTilt(myRigidBody.rotation * yaw);
        myRigidBody.MoveRotation(targetRotation);

        // ForceMode.Acceleration integrates over the step internally, so don't multiply by fixedDeltaTime.
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
    #endregion
}
