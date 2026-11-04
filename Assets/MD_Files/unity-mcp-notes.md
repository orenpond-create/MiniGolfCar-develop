---
name: unity-mcp-notes
description: "How to work with the Unity MCP (UnityMCP / MCP for Unity) in this project, and traps to avoid"
metadata:
  node_type: memory
  type: reference
  originSessionId: 5ab888ac-81a1-48db-8bb5-d2a32b148546
  modified: 2026-11-04T19:45:23.372Z
---

The user installed a Unity MCP server (tools `mcp__UnityMCP__*`, resources `mcpforunity://...`) around 2026-10-28. It lets me read the live scene, set component properties, run in-memory C# (`execute_code`), enter/exit Play mode, read the Console and save scenes. Unity 2022.3.62f3, Cinemachine 2.10.4, Terrain Tools 5.0.6. ProBuilder not installed.

**Traps learned:**
- Reading a collider's components through the `mcpforunity://scene/gameobject/{id}/components` resource touches `Collider.material`, which makes Unity create and assign a material COPY (it replaced the WheelSlide asset reference with "WheelSlide (Instance)" and gave the body box an unnamed instance). Read colliders via `execute_code` using `sharedMaterial` instead, and verify asset paths with `AssetDatabase.GetAssetPath` before saving.
- `execute_code` compiles with CodeDom (C# 6): write `UnityEngine.Object.FindObjectsOfType`, not `Object.` (ambiguous).
- Instance IDs can change after a recompile; find objects again by path.
- Timed sampling in Play mode: register an `EditorApplication.update` callback that logs to the Console when done, then read it with `read_console`. Clear private state (e.g. CarMovement `isStuck`) via reflection when teleporting the car.
- Prefer menu-item editor scripts or the MCP over hand-editing `.unity`/`.prefab` YAML. Terrain `.asset` files are binary.
- Check the scene's dirty state before saving: saving also saves the user's unsaved edits. Ask first.

Still follow [[ask-before-acting]] for every change. Related: [[car-controller-design]], [[test-course]].
