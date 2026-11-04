---
name: minicargolf-game-design
description: "Intended game loop for 3D_LearnZone_MiniCarGolf (ball<->vehicle transform, turns, fuel)"
metadata: 
  node_type: memory
  type: project
  originSessionId: 6d94369d-23b1-45fd-a29f-a45c89d0b1e4
---

3D_LearnZone_MiniCarGolf is a learning prototype: a mini-golf / vehicle hybrid where one Player changes form during a turn (the `PlayerType { Ball, Car }` enum = current form, not separate entities). Intended loop as explained by the user (2026-06-28), NOT yet implemented:

- Every turn the player starts as a **Ball**; aim + slingshot-flick it (existing Ball.cs mechanic). Collect **fuel** along the way (pickup mechanic not built yet).
- **Transform trigger:** while the ball is moving, **any left-mouse-button click** (anywhere, including on the ball itself) turns it into a **Car** facing the ball's movement direction, carrying momentum. May support more vehicle types later.
- **Car control:** drive as in Car.cs (LMB/RMB steer), burning fuel.
- **Brake / end turn:** both mouse buttons = brake; holding both for X seconds ends the turn.
- **Turn can end in either form:** as a ball (rolls to a stop, or reaches destination) or as a car (fuel out, hold-both-buttons, or reaches destination).
- **Form persists between turns** — if you end as a vehicle you stay a vehicle until the next turn; the revert to ball happens at the start of the next turn.

Open/unaddressed: camera control for both forms, and "tightening" the car feel — user set these aside, not yet scoped. Honor [[ask-before-acting]] before implementing any of this.
