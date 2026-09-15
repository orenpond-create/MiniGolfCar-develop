using UnityEngine;
// Adapted from the old project's virtual-joystick car into this project's mouse scheme:
//   - Always drives forward (when grounded).
//   - Left mouse button  = steer left.
//   - Right mouse button = steer right.
//   - Both buttons       = brake, coasting to a stop.
// Kept from the original: 9-point ground check, extra gravity while airborne, the two-tier
// (normal/max) speed profile, and KeepSafeAngles() anti-flip clamping.
// Removed: LevelManager/virtualJoystick input, the unused turbo "override movement speed" system.
public class CarMovement : MonoBehaviour
{
    #region Fields
    // Public Fields
    [HideInInspector]
    public Rigidbody myRigidBody;
    [Header("RigidBody Values")]
    public Vector3 centerOfMass;
    [Header("Speed / Steering Values")]
    [SerializeField] CarMovementSpeed movementSpeed;          // was referenced but never declared in the old project
    public float rotateSpeedOnNormalSpeed = 90f;             // deg/sec while below normalSpeed
    public float rotateSpeedOnMaxSpeed = 120f;              // deg/sec at high speed
    [SerializeField] float maxTiltAngle = 50f;             // anti-flip: max tilt from upright (deg); was hardcoded 50 in KeepSafeAngles
    [Header("Ground Check")]
    [SerializeField] LayerMask groundMask;                   // replaces LevelManager.layerMaskEnemy_Solids_NotPlayer
    [SerializeField] float checkGroundSensitivity = 1f;
    [Header("Stuck / Recovery")]
    [SerializeField] CarMovementSpeed recoverySpeed;        // slow accel + low max used right after getting unstuck
    [SerializeField] float stuckSpeedThreshold = 0.5f;      // forward speed below this counts as "not moving"
    [SerializeField] float stuckDetectTime = 0.4f;          // blocked this long -> stuck
    [SerializeField] float recoveryTime = 1.5f;             // X: slow-drive duration after getting unstuck
    [SerializeField] float unstuckHeadingDelta = 20f;       // must rotate this many degrees from the stuck heading to retry
    [Header("Object Bottom Points")]
    public Transform forwardPoint;
    public Transform backPoint;
    public Transform leftPoint;
    public Transform rightPoint;
    public Transform centerPoint;
    public Transform rightForwardPoint;
    public Transform rightBackPoint;
    public Transform leftForwardPoint;
    public Transform leftBackPoint;
    [HideInInspector]
    public bool pauseMovement = false;
    [HideInInspector]
    public Status status;
    // Private Fields
    float rotateDirection;      // -1 = left (LMB), +1 = right (RMB), 0 = straight
    bool isStoping;             // true while braking (both buttons held)
    bool onFliping;
    bool onGround;
    Vector3 down;
    RaycastHit hit;
    bool isStuck;
    float stuckTimer;
    float recoveryTimer;      // counts down while recovering; 0 = fully back to normal
    float stuckHeading;       // yaw (deg) recorded when we got stuck
    [SerializeField] TMPro.TextMeshProUGUI messageLabel;
    #endregion

    #region Enums
    public enum Status { Driving, Stoping };
    #endregion

    #region Methods
    void Awake()
    {
        myRigidBody = GetComponentInChildren<Rigidbody>();
        onFliping = false;
        myRigidBody.centerOfMass = centerOfMass;
    }

    void Update()
    {
        if (onFliping || pauseMovement)
            return;

        ReadInput();
    }

    void FixedUpdate()
    {
        if (onFliping || pauseMovement)
            return;

        CheckOnGround();
        if (onGround)
        {
            if (!isStoping)
            {
                UpdateStuckState();
                if (!isStuck)
                    MoveRigidBodyFwd();
            }
            else
                StopRigidBody();
        }
        else
        {
            myRigidBody.AddForce(10f * Vector3.down, ForceMode.Acceleration);
        }

        ApplyRotation();
    }

    // Always driving forward; LMB steers left, RMB steers right, BOTH brakes to a stop.
    void ReadInput()
    {
        bool left = Input.GetMouseButton(0);
        bool right = Input.GetMouseButton(1);

        if (left && right)
        {
            isStoping = true;
            rotateDirection = 0f;
            status = Status.Stoping;
        }
        else
        {
            isStoping = false;
            rotateDirection = 0f;
            if (left) rotateDirection = -1f;    // turn left
            if (right) rotateDirection = 1f;    // turn right
            status = Status.Driving;
        }
    }

    // Steering + tilt clamp, both applied through the Rigidbody (MoveRotation) in FixedUpdate so
    // they respect collisions instead of teleporting the collider into obstacles (fixes 1 & 2).
    void ApplyRotation()
    {
        Quaternion target = myRigidBody.rotation;

        // Steer (yaw) unless braking.
        if (!isStoping && rotateDirection != 0f)
        {
            float turnRate = (myRigidBody.velocity.sqrMagnitude <= movementSpeed.normalSpeed)
                ? rotateSpeedOnNormalSpeed
                : rotateSpeedOnMaxSpeed;
            target = target * Quaternion.Euler(0f, rotateDirection * turnRate * Time.fixedDeltaTime, 0f);
        }

        target = ClampSafeAngles(target);
        myRigidBody.MoveRotation(target);
    }

    // Detects being jammed (trying to drive but not actually moving forward), and re-arms once
    // you've steered far enough away to be worth retrying. Rotation is untouched, so you can
    // always turn while stuck.
    void UpdateStuckState()
    {
        float forwardSpeed = Vector3.Dot(myRigidBody.velocity, transform.forward);

        if (!isStuck)
        {
            if (forwardSpeed < stuckSpeedThreshold)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer >= stuckDetectTime)
                {
                    isStuck = true;
                    stuckHeading = transform.eulerAngles.y;
                    stuckTimer = 0f;
                }
            }
            else
                stuckTimer = 0f;             // making progress -> not stuck
        }
        else
        {
            // Re-arm once you've rotated far enough from the heading you got stuck at,
            // then resume driving in slow-recovery mode.
            float headingDelta = Mathf.Abs(Mathf.DeltaAngle(stuckHeading, transform.eulerAngles.y));
            if (headingDelta >= unstuckHeadingDelta)
            {
                isStuck = false;
                recoveryTimer = recoveryTime;
            }
        }
        if (messageLabel != null)
        {
            messageLabel.text = "Is Stuck " + isStuck + " " + stuckTimer + "Time";
            messageLabel.text += "\n" + "SQR Velocity " + myRigidBody.velocity.sqrMagnitude;
        }
    }

    void MoveRigidBodyFwd()
    {
        // Count down the recovery timer and blend accel/max from the slow recovery profile
        // back up to the normal one. blend: 0 = just unstuck (slow), 1 = normal.
        if (recoveryTimer > 0f)
            recoveryTimer = Mathf.Max(0f, recoveryTimer - Time.fixedDeltaTime);
        float blend = (recoveryTime > 0f) ? 1f - (recoveryTimer / recoveryTime) : 1f;

        float normalSpeed = Mathf.Lerp(recoverySpeed.normalSpeed, movementSpeed.normalSpeed, blend);
        float maxSpeed    = Mathf.Lerp(recoverySpeed.maxSpeed,    movementSpeed.maxSpeed,    blend);
        float accelNormal = Mathf.Lerp(recoverySpeed.accelerationNormalSpeed, movementSpeed.accelerationNormalSpeed, blend);
        float accelMax    = Mathf.Lerp(recoverySpeed.accelerationMaxSpeed,    movementSpeed.accelerationMaxSpeed,    blend);

        float sqrSpeed = myRigidBody.velocity.sqrMagnitude;
        if (sqrSpeed < normalSpeed)
            myRigidBody.AddForce(transform.forward * accelNormal * Time.fixedDeltaTime, ForceMode.Force);
        else if (sqrSpeed < maxSpeed)
            myRigidBody.AddForce(transform.forward * accelMax * Time.fixedDeltaTime, ForceMode.Force);
        // else at/over maxSpeed: coast, no extra force
    }

    void StopRigidBody()
    {
        // Bleed off speed regardless of travel direction. (The original only decelerated when all
        // velocity components were >= 0, which failed to stop the car when moving in -x/-z.)
        if (myRigidBody.velocity.magnitude > 0.1f)
            myRigidBody.velocity = myRigidBody.velocity.normalized *
                (myRigidBody.velocity.magnitude - movementSpeed.stopingPower * Time.fixedDeltaTime);
        else
            myRigidBody.velocity = Vector3.zero;
    }

    void CheckOnGround()
    {
        down = (transform.position - centerPoint.position).normalized;
        onGround =
            RayCast(forwardPoint.position, down) ||
            RayCast(centerPoint.position, down) ||
            RayCast(backPoint.position, down) ||
            RayCast(leftPoint.position, down) ||
            RayCast(rightPoint.position, down) ||
            RayCast(leftForwardPoint.position, down) ||
            RayCast(rightForwardPoint.position, down) ||
            RayCast(leftBackPoint.position, down) ||
            RayCast(rightBackPoint.position, down);
    }

    bool RayCast(Vector3 origin, Vector3 direction)
    {
        Debug.DrawRay(origin, direction * checkGroundSensitivity, Color.green);
        return Physics.Raycast(origin, direction, out hit, checkGroundSensitivity, groundMask);
    }

    // Physics-friendly anti-flip: limits total tilt (pitch+roll combined) to maxTiltAngle from
    // upright, preserving heading. Replaces the old KeepSafeAngles() which hard-set
    // transform.rotation and teleported the collider into obstacles (the "jump" bug).
    Quaternion ClampSafeAngles(Quaternion rotation)
    {
        Vector3 up = rotation * Vector3.up;
        float tilt = Vector3.Angle(Vector3.up, up);
        if (tilt <= maxTiltAngle)
            return rotation;

        // Bring 'up' back to exactly maxTiltAngle from world-up along the same lean direction,
        // then apply that minimal correction (which keeps yaw intact).
        Vector3 desiredUp = Vector3.RotateTowards(Vector3.up, up, maxTiltAngle * Mathf.Deg2Rad, 0f);
        return Quaternion.FromToRotation(up, desiredUp) * rotation;
    }
    #endregion
}
