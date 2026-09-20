# Optional desktop XR preview

Meta XR Core SDK **201.0.0** is installed, while this optional desktop preview uses official **standalone** Meta XR Operator and Simulator binaries with OpenXR 1.18.0. Core 201 does not include the integrated Operator panel. [Core 203](https://developers.meta.com/horizon/downloads/package/meta-xr-core-sdk/203.0/?view=full_width) and Core 205 require Unity 6000.0.66f2, above this project's 6000.0.58f2. The standalone route does not provide Android Operator instrumentation.

## Local setup

The setup machine keeps Operator and Simulator under ignored `.local-tools/`; these binaries and local paths are not part of the repository. On another Windows machine, obtain the [standalone Operator](https://developers.meta.com/horizon/downloads/package/meta-xr-operator-standalone/) and [Simulator](https://developers.meta.com/horizon/downloads/package/meta-xr-simulator-windows/) from Meta. Simulator also needs [Windows App Runtime 1.5](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads-archive). If bootstrap fails with `0x80070491` despite the runtime installer, check the matching x64 DDLM registration; that resolved the setup-machine failure.

Select the Simulator in Meta Core's own settings so that its toolbar Simulator toggle (**Meta > Meta XR Simulator > Activate**) works. With Unity out of Play mode, choose **VR Basketball > Desktop XR > Configure Experimental Operator** and select the Operator JSON. Register the proxy in Claude Code using its actual path:

```powershell
claude mcp add --scope local --transport stdio meta-xr-operator -- 'C:/path/to/windows/meta-xr-operator-mcp-proxy.exe'
claude mcp list
```

The Unity helper stores the Operator path in local Editor preferences. Normal Play uses the system OpenXR runtime, such as Quest Link, unless Meta's Simulator toggle is on. **VR Basketball > Desktop XR > Play in Simulator** turns Meta's toggle on for one Play session if it was off, and adds the Operator layer. When Play stops, the helper removes the layer and turns the toggle back off, so the next normal Play uses the headset. The layer exists only in memory during that session and is never saved to the tracked OpenXR settings; if that file ever contains it, the helper removes it and logs a warning when Unity loads or leaves Play. **Player Settings > Run In Background** must stay enabled. Without it, Play started while Unity is not the focused window, for example by an agent, freezes after two frames; the Simulator never receives frames, `GfxDevice::UpdateBufferRanges` errors repeat, and Unity once crashed. **Play in Simulator** refuses to start if the setting is off. **Show Local Status** checks configuration. Keep the original distribution files together and stop Play before moving them.

## What was checked

Operator returned a fresh `XR_SESSION_STATE_FOCUSED` session, tracked head/controller poses, controller pose and trigger input with readback, and an XR image of the scaffold scene. A full Play/stop cycle restored the prior runtime setting and removed the temporary layer. Local evidence is under ignored `Logs/OperatorResearch/`.

For a new check, use Unity MCP to confirm the saved scene and ready Editor, run **Play in Simulator**, query Operator session state, then test input only after the session is FOCUSED. Release simulated buttons, stop Play with a deferred stop rather than Unity MCP `manage_editor stop` (two Operator teardowns crashed Unity after direct stops; see `AI_LOG.md`), and inspect the console. A connected proxy alone is not a live session. Unity MCP remains the tool for scene and asset editing.

This setup verifies desktop behavior only. Headset launch, Android performance, haptics, and throwing feel still require device testing. The repository opens without the optional local binaries.
