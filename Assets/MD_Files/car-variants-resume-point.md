---
name: car-variants-resume-point
description: History only - the carA/carB/carReal collider experiment (July 2026) is finished and superseded by CarMovement
metadata:
  node_type: memory
  type: project
  originSessionId: 5ab888ac-81a1-48db-8bb5-d2a32b148546
  modified: 2026-11-04T19:45:30.225Z
---

**Concluded, nothing to resume.** In July 2026 we compared three car approaches: carA (`Car.cs`, one box collider), carB (sphere colliders on wheels) and carReal (raycast spring suspension, built by a prefab-builder editor script). carReal kept flying or not driving because of nested non-uniform scales and a wrong ground layer, and was never fully tuned.

The user then imported the "Hummer" controller from an older project, which became `CarMovement.cs` and replaced all three. As of 2026-11-04, `CarB.cs`, `CarReal.cs`, the prefab builder and the TestScene3_CarB / TestScene3_CarC scenes are deleted; `Car.cs` is in `Assets/Scripts/Archive/`. `Assets/Prefabs/CarReal.prefab` still exists and probably has a missing script now (a likely source of the "referenced script (Unknown) is missing" Console warning).

Lessons that still apply: avoid nested non-uniform scales on physics objects; make sure ray origins can never end up below the floor; check `groundMask` against the ground's actual layer. The sphere-collider idea (carB) came back later for the Hummer wheels.

Current design: [[car-controller-design]].
