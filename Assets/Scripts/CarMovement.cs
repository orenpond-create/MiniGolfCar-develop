using UnityEngine;
// Adapted from the old project's virtual-joystick car into this project's mouse scheme:
//   - Always drives forward (when grounded).
//   - Left mouse button  = steer left.
//   - Right mouse button = steer right.
//   - Both buttons       = brake, coasting to a stop.
// Kept from the original: 9-point ground check, extra gravity while airborne and anti-flip clamping.
// Speed is driven by CarMovementSpeed profiles: movementSpeed (normal), recoverySpeed (after
// getting unstuck) and temporary overrides (SetSpeedOverride) from turbo pads / slow zones.
// Removed: LevelManager/virtualJoystick input.
public class CarMovement : MonoBehaviour
{
    #region Fields
    // Public Fields
    [HideInInspector]
    public Rigidbody myRigidBody;
    [Header("RigidBody Values")]
    public Vector3 centerOfMass;
    [Header("Speed / Steering Values")]
    [SerializeField] CarMovementSpeed movementSpeed;          // the normal profile
    [SerializeField] float maxTiltAngle = 50f;             // anti-flip: max tilt from upright (deg); was hardcoded 50 in KeepSafeAngles
    [Header("Ground Check")]
    [SerializeField] LayerMask groundMask;                   // replaces LevelManager.layerMaskEnemy_Solids_NotPlayer
    [SerializeField] float checkGroundSensitivity = 1f;
    [Header("Air Control")]
    [SerializeField, Range(0f, 1f)] float airSteerMultiplier = 1f;   // 0 = no steering in the air, 1 = full (today's behavior)
    [SerializeField] bool allowAirAcceleration = false;              // false = momentum only (today's behavior)
    [SerializeField] float airborneGraceTime = 0.1f;                 // off the ground this long before it counts as "in the air"
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
    bool isAirborne;            // true once the car has been off the ground longer than airborneGraceTime
    float timeOffGround;
    Vector3 down;
    RaycastHit hit;
    bool isStuck;
    float stuckTimer;
    float recoveryTimer;      // counts down while recovering; 0 = fully back to normal
    float recoveryBlend = 1f; // 0 = just unstuck (recoverySpeed values), 1 = fully on the active profile
    float stuckHeading;      // yaw (deg) recorded when we got stuck
    CarMovementSpeed activeSpeed;   // profile currently driving with: movementSpeed normally, or an override
    float overrideTimer;            // seconds left on a timed override; Infinity = indefinite until cleared
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
        activeSpeed = movementSpeed;
    }

    void Update()
    {
        if (onFliping || pauseMovement)
            return;

        ReadInput();
        TickSpeedOverride();
    }

    void FixedUpdate()
    {
        if (onFliping || pauseMovement)
            return;

        TickRecovery();
        CheckOnGround();
        TickAirborne();

        // Driving uses the grace-time state (isAirborne), not the raw ray result, so a brief ray miss
        // (a bump, or the pitch change at the foot of a ramp) doesn't cut the engine mid-climb.
        if (!isAirborne)
        {
            if (!isStoping)
            {
                UpdateStuckState();
                if (!isStuck)
                    MoveRigidBodyFwd();
                ApplyDrift();
            }
            else
                StopRigidBody();
        }
        else if (allowAirAcceleration && !isStoping)
        {
            if (messageLabel != null)
                messageLabel.text = "In air";     // MoveRigidBodyFwd appends to the label, so reset it here
            MoveRigidBodyFwd();
        }

        if (!onGround)
            myRigidBody.AddForce(10f * Vector3.down, ForceMode.Acceleration);

        ApplyRotation();
    }

    // onGround flickers on bumps and small cubes, so only treat the car as airborne after it has
    // stayed off the ground for airborneGraceTime. Used to gate driving and air steering.
    void TickAirborne()
    {
        timeOffGround = onGround ? 0f : timeOffGround + Time.fixedDeltaTime;
        isAirborne = timeOffGround > airborneGraceTime;
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

    // Temporarily drive with a different speed profile (e.g. a turbo pad or speed zone).
    // duration > 0  -> reverts automatically after that many seconds.
    // duration <= 0 -> stays until ClearSpeedOverride() is called (good for enter/exit zones).
    public void SetSpeedOverride(CarMovementSpeed profile, float duration)
    {
        if (profile == null)
            return;
        activeSpeed = profile;
        overrideTimer = (duration > 0f) ? duration : Mathf.Infinity;
    }

    public void ClearSpeedOverride()
    {
        activeSpeed = movementSpeed;
        overrideTimer = 0f;
    }

    void TickSpeedOverride()
    {
        if (activeSpeed == movementSpeed || float.IsInfinity(overrideTimer))
            return;                           // no override active, or indefinite until cleared
        overrideTimer -= Time.deltaTime;
        if (overrideTimer <= 0f)
            ClearSpeedOverride();
    }

    // Steering + tilt clamp, both applied through the Rigidbody (MoveRotation) in FixedUpdate so
    // they respect collisions instead of teleporting the collider into obstacles (fixes 1 & 2).
    void ApplyRotation()
    {
        Quaternion target = myRigidBody.rotation;

        // Steer (yaw) unless braking. In the air the turn rate is scaled by airSteerMultiplier.
        float steerFactor = isAirborne ? airSteerMultiplier : 1f;
        if (!isStoping && rotateDirection != 0f && steerFactor > 0f)
        {
            float turnRate = Mathf.Lerp(recoverySpeed.rotateSpeed, activeSpeed.rotateSpeed, recoveryBlend) * steerFactor;
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

    // Counts down the post-stuck recovery and updates the shared blend used by driving AND steering.
    // blend: 0 = just unstuck (recoverySpeed values), 1 = fully on the active profile.
    void TickRecovery()
    {
        if (recoveryTimer > 0f)
            recoveryTimer = Mathf.Max(0f, recoveryTimer - Time.fixedDeltaTime);
        recoveryBlend = (recoveryTime > 0f) ? 1f - (recoveryTimer / recoveryTime) : 1f;
    }

    // Drift: the body turns instantly but the travel direction catches up over driftTime seconds.
    // Each physics step the sideways part of the velocity (across the car's facing) decays
    // exponentially, so the feel is framerate-independent. Grounded only - in the air we keep momentum.
    void ApplyDrift()
    {
        float driftTime = Mathf.Lerp(recoverySpeed.driftTime, activeSpeed.driftTime, recoveryBlend);
        float keep = (driftTime > 0f) ? Mathf.Exp(-Time.fixedDeltaTime / driftTime) : 0f;   // fraction of sideways speed kept this step

        Vector3 velocity = myRigidBody.velocity;
        Vector3 right = transform.right;
        float sidewaysSpeed = Vector3.Dot(velocity, right);
        myRigidBody.velocity = velocity - right * (sidewaysSpeed * (1f - keep));
    }

    void MoveRigidBodyFwd()
    {
        float maxSpeed         = Mathf.Lerp(recoverySpeed.maxSpeed,         activeSpeed.maxSpeed,         recoveryBlend);
        float acceleration     = Mathf.Lerp(recoverySpeed.acceleration,     activeSpeed.acceleration,     recoveryBlend);
        float overspeedBraking = Mathf.Lerp(recoverySpeed.overspeedBraking, activeSpeed.overspeedBraking, recoveryBlend);

        // Horizontal speed only: a fast fall or jump must not count as "at the speed cap",
        // and the overspeed braking must not slow the vertical movement.
        Vector3 velocity = myRigidBody.velocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float sqrSpeed = horizontal.sqrMagnitude;
        if (sqrSpeed < maxSpeed)
        {
            myRigidBody.AddForce(transform.forward * acceleration * Time.fixedDeltaTime, ForceMode.Force);
        }
        else if (sqrSpeed > maxSpeed && overspeedBraking > 0f)
        {
            // Above the active cap (turbo just ended, or a slow zone started): ease back down to it.
            // maxSpeed is a squared speed, so the target speed is its square root.
            float newSpeed = Mathf.MoveTowards(horizontal.magnitude, Mathf.Sqrt(maxSpeed), overspeedBraking * Time.fixedDeltaTime);
            Vector3 newHorizontal = horizontal.normalized * newSpeed;
            myRigidBody.velocity = new Vector3(newHorizontal.x, velocity.y, newHorizontal.z);
        }
        // else at the cap with no overspeed braking: coast, no extra force

        if (messageLabel != null)
        {
            messageLabel.text += "\n " + "maxSpeed " + maxSpeed;
            messageLabel.text += "\n " + "acceleration " + acceleration;
            messageLabel.text += "\n " + "recoveryBlend " + recoveryBlend;
        }
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
