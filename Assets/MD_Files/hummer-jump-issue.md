---
name: hummer-jump-issue
description: "Resolved - why the Hummer was launched into the air by small cubes, and the fix that was applied"
metadata:
  node_type: memory
  type: project
  originSessionId: 5ab888ac-81a1-48db-8bb5-d2a32b148546
  modified: 2026-11-04T19:45:44.521Z
---

**Resolved in code (September 2026), no need to remind the user.**

Problem: in TestScene3_Hammer the Hummer was sometimes flung high into the air after getting stuck on small cubes. Cause: the old `KeepSafeAngles()` wrote `transform.rotation` directly on a non-kinematic Rigidbody, snapping the collider into the cube; PhysX then fired a large depenetration impulse. Always-on forward thrust (pointing upward when tilted) and Discrete collision detection made it worse.

Applied (user approved fixes 1-3): tilt limiting moved to `ClampSafeAngles()` through `MoveRotation`; steering through `MoveRotation` in FixedUpdate instead of `transform.Rotate`. Fix 3 (Rigidbody collision detection → Continuous Dynamic) was an Inspector step for the user; on 2026-10-28 `Hummer (2) CircleColliders` in TestScene4 was still Discrete, so it may never have been set. Fix 4 (cut thrust when steeply pitched) was not done; the later stuck/recovery system cuts thrust when blocked instead.

If jumps come back, check first for direct `transform.rotation`/`position` writes on the car. Current design: [[car-controller-design]].
