---
name: xr-preview
description: Check live desktop XR behavior with the optional standalone Operator and Simulator.
---

Use only for a desktop XR check. For missing local setup, see `Docs/DesktopXR.md`.

1. Use Unity MCP to confirm the correct ready Editor and saved scene. Inspect **VR Basketball > Desktop XR > Show Local Status**.
2. Enter Play, then query `openxr_get_session_info`. Test input only after a fresh FOCUSED session; an offline tool list is insufficient.
3. Read actual tool schemas, use fresh poses, release simulated buttons, and capture the XR view only when it helps the check.
4. Stop Play, check console and saved scene, and record actual results in `AI_LOG.md`.

Desktop checks do not establish Android launch, headset performance, haptics, or throwing feel. Use Unity MCP for scene and asset editing.
