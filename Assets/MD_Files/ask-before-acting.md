---
name: ask-before-acting
description: User wants explicit approval before any code change or action; default to discussion only
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 6d94369d-23b1-45fd-a29f-a45c89d0b1e4
---

When discussing this project, do NOT write/edit code, create files, run commands, or make any change without the user's explicit approval first. Often the user is just explaining game logic/design and wants discussion only — not implementation.

**Why:** On 2026-06-28 the user was explaining the MiniCarGolf game loop and I implemented the combined Player.cs unprompted; they asked me to restore it and to ask first from then on.

**How to apply:** Treat design/explanation messages as discussion. Propose, then wait for a clear go-ahead before acting. Exceptions only when the user explicitly says to proceed for a given task. See [[minicargolf-game-design]].
