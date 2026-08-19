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
    [Header("Ground Check")]
    [SerializeField] LayerMask groundMask;                   // replaces LevelManager.layerMaskEnemy_Solids_NotPlayer
    [SerializeField] float checkGroundSensitivity = 1f;
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
        if (!isStoping)
            Rotate();
        KeepSafeAngles();
    }

    void FixedUpdate()
    {
        if (onFliping || pauseMovement)
            return;

        CheckOnGround();
        if (onGround)
        {
            if (!isStoping)
                MoveRigidBodyFwd();
            else
                StopRigidBody();
        }
        else
        {
            myRigidBody.AddForce(10f * Vector3.down, ForceMode.Acceleration);
        }
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

    void Rotate()
    {
        float turnRate = (myRigidBody.velocity.sqrMagnitude <= movementSpeed.normalSpeed)
            ? rotateSpeedOnNormalSpeed
            : rotateSpeedOnMaxSpeed;
        transform.Rotate(0f, rotateDirection * turnRate * Time.deltaTime, 0f);
    }

    void MoveRigidBodyFwd()
    {
        float sqrSpeed = myRigidBody.velocity.sqrMagnitude;
        if (sqrSpeed < movementSpeed.normalSpeed)
            myRigidBody.AddForce(transform.forward * movementSpeed.accelerationNormalSpeed * Time.fixedDeltaTime, ForceMode.Force);
        else if (sqrSpeed < movementSpeed.maxSpeed)
            myRigidBody.AddForce(transform.forward * movementSpeed.accelerationMaxSpeed * Time.fixedDeltaTime, ForceMode.Force);
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

    void KeepSafeAngles()
    {
        float x = transform.eulerAngles.x;
        float y = transform.eulerAngles.y;
        float z = transform.eulerAngles.z;
        if (transform.eulerAngles.x > 100 && transform.eulerAngles.x < 310)
            x = 310;
        if (transform.eulerAngles.x < 100 && transform.eulerAngles.x > 50)
            x = 50;
        if (transform.eulerAngles.z > 100 && transform.eulerAngles.z < 310)
            z = 310;
        if (transform.eulerAngles.z < 100 && transform.eulerAngles.z > 50)
            z = 50;

        transform.rotation = Quaternion.Euler(x, y, z);
    }
    #endregion
}
