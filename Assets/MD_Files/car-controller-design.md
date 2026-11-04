---
name: car-controller-design
description: "How the current car controller (Assets/Scripts/CarMovement.cs) works, what was measured, and open ideas"
metadata:
  node_type: memory
  type: project
  originSessionId: 5ab888ac-81a1-48db-8bb5-d2a32b148546
  modified: 2026-11-04T19:45:09.325Z
---

The active car controller is `Assets/Scripts/CarMovement.cs` + `CarMovementSpeed.cs` (adapted from the user's old "Hummer" project; joystick input replaced). State as of 2026-11-04.

**Controls:** always drives forward; LMB = steer left, RMB = steer right, both = brake (`StopRigidBody`, uses `movementSpeed.stopingPower`).

**Speed profiles (`CarMovementSpeed`):** one band per profile: `maxSpeed` (compared to velocity.sqrMagnitude, so it is a SQUARED speed: 54 ≈ 7.3 m/s, 400 = 20 m/s), `acceleration`, `rotateSpeed` (deg/s), `stopingPower`, `overspeedBraking`, `driftTime`.
- `movementSpeed` = normal profile. `SetSpeedOverride(profile, duration)` / `ClearSpeedOverride()` swap in another profile for turbo pads, mud, ice etc. (duration <= 0 = until cleared). No trigger objects built yet.
- `recoverySpeed` = profile blended in after getting unstuck (`recoveryBlend` 0→1 over `recoveryTime`).

**Systems in FixedUpdate:** `TickRecovery` → `CheckOnGround` (9 down-rays, length `checkGroundSensitivity`, layer `groundMask`) → `TickAirborne` (`isAirborne` only after `airborneGraceTime`) → if !isAirborne: `UpdateStuckState`, `MoveRigidBodyFwd`, `ApplyDrift` → extra 10 m/s² gravity when raw `!onGround` → `ApplyRotation` (steer via MoveRotation, scaled by `airSteerMultiplier` in air; `ClampSafeAngles` limits tilt to `maxTiltAngle`).
- Stuck: forward speed < `stuckSpeedThreshold` for `stuckDetectTime` → no thrust until the player steers `unstuckHeadingDelta` degrees. Latches forever with no input (and teleporting doesn't clear it).
- Drift: sideways velocity decays by exp(-dt/driftTime); grounded only.
- Air: `allowAirAcceleration` (off by default); speed cap uses horizontal speed only.

**Measured on the 20° test ramp (2026-10-28, Hummer (2) CircleColliders, TestScene4):** front and rear wheels each lose 2–3 m/s hitting the ramp foot; the rear hit pitches the car to ~28°; the 0.2 m ground rays then miss while wheels still touch, cutting thrust and adding extra gravity, so it slows 6.5 → 2.8 m/s ("stops then continues"); the jump landing killed all forward speed. Root factors: sphere colliders on the Rigidbody don't roll, they slide (default friction 0.6 = a brake), short ground rays, doubled gravity.
- Applied: `WheelSlide` physic material (friction 0.1, Minimum) on the 4 wheel spheres; `checkGroundSensitivity` 0.45; driving gated by `isAirborne` instead of raw `onGround`. Only on `Hummer (2) CircleColliders` in TestScene4, not on the prefab. Not re-measured yet.

**Open ideas (not approved/done):** extra gravity only when truly airborne or lower; stuck-state timeout; step assist (forward rays + lift); low friction may let the car slide back on slopes while braking; car is scaled 0.5 in the test scene.

Related: [[test-course]], [[unity-mcp-notes]], [[hummer-jump-issue]], [[minicargolf-game-design]].
