---
name: sync-md-backup
description: "After every memory change, copy all memory .md files into the project's Assets/MD_Files backup folder"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 5ab888ac-81a1-48db-8bb5-d2a32b148546
  modified: 2026-11-04T19:49:57.438Z
---

Whenever I create, update or delete a memory file, also update the backup copy in `E:\Unity Projects V2\3D_LearnZone_MiniCarGolf\Assets\MD_Files` so it stays identical to the memory folder (delete a backup file if its memory file was deleted).

**Why:** On 2026-11-04 the user asked for the memory notes to be backed up inside the project and said to re-sync the backup every time the notes change.

**How to apply:** After any memory write, copy all `*.md` from the memory folder into `Assets/MD_Files` and check they match (ignore Unity's `.meta` files). This is a standing approval for the sync itself; other actions still follow [[ask-before-acting]].
