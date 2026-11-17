---
name: resume-occlusion-check
description: "Follow-up on the ball/car hidden-object outline - all my tests pass; waiting for the user's own check"
metadata:
  node_type: memory
  type: project
  originSessionId: 5ab888ac-81a1-48db-8bb5-d2a32b148546
  modified: 2026-11-17T17:56:11.034Z
---

The user asked (2026-11-16) to check/fix the ball & car "visibility behind objects" and that it only shows when actually hidden. On 2026-11-17 it was redone as a Golf-With-Your-Friends-style cyan outline only (see [[ball-course]]). All my Play-mode tests passed after the final shader fixes: hidden ball (TestScene5_BallCourseZoomIn) and hidden car (TestScene4_TerrianClaude) → closed 3 px ring; ball/car visible against a wall → nothing; no false ring from the floor. The user saved all scenes.

Still open: the user hasn't checked it themselves yet. If they report problems, edge cases to look at: partly hidden pieces, thin walls, the car's antennas, terrain, very close/far camera distances.
