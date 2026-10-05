using System;
using UnityEngine.Serialization;

// One speed profile = one driving "mode". CarMovement uses movementSpeed as the normal profile,
// recoverySpeed after getting unstuck, and any profile a trigger passes to SetSpeedOverride()
// (turbo pad, mud zone, ice patch...) - so an override can be faster OR slower than normal.
// maxSpeed is compared against velocity.sqrMagnitude (squared speed), as before.
[Serializable]
public class CarMovementSpeed
{
    public float maxSpeed;                                                  // speed cap while this profile is active
    [FormerlySerializedAs("accelerationNormalSpeed")]
    public float acceleration;                                              // forward acceleration below the cap
    public float rotateSpeed = 90f;                                         // steering rate, deg/sec
    public float stopingPower;                                              // braking when both mouse buttons are held
    public float overspeedBraking;                                          // gentle decel (speed/sec) when above maxSpeed; 0 = just coast
}
