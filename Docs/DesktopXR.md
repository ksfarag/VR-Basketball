# Optional desktop XR preview

Meta XR Core SDK **201.0.0** is installed, while this optional desktop preview uses official **standalone** Meta XR Operator and Simulator binaries with OpenXR 1.18.0. Core 201 does not include the integrated Operator panel. [Core 203](https://developers.meta.com/horizon/downloads/package/meta-xr-core-sdk/203.0/?view=full_width) and Core 205 require Unity 6000.0.66f2, above this project's 6000.0.58f2. The standalone route does not provide Android Operator instrumentation.

## Local setup

The setup machine keeps Operator and Simulator under ignored `.local-tools/`; these binaries and local paths are not part of the repository. On another Windows machine, obtain the [standalone Operator](https://developers.meta.com/horizon/downloads/package/meta-xr-operator-standalone/) and [Simulator](https://developers.meta.com/horizon/downloads/package/meta-xr-simulator-windows/) from Meta. Simulator also needs [Windows App Runtime 1.5](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads-archive). If bootstrap fails with `0x80070491` despite the runtime installer, check the matching x64 DDLM registration; that resolved the setup-machine failure.

With Unity out of Play mode, choose **VR Basketball > Desktop XR > Configure Experimental Operator**, then select the Operator JSON and `meta_openxr_simulator.json`. Register the proxy in Claude Code using its actual path:

```powershell
claude mcp add --scope local --transport stdio meta-xr-operator -- 'C:/path/to/windows/meta-xr-operator-mcp-proxy.exe'
claude mcp list
```

The Unity helper stores paths in local Editor preferences. During Play it selects the Simulator and enables the Operator layer; on exit it restores the prior desktop runtime/layer state. **Show Local Status** checks configuration; **Disable Experimental Preview** turns off this optional path. Keep the original distribution files together and stop Play before moving them.

## What was checked

Operator returned a fresh `XR_SESSION_STATE_FOCUSED` session, tracked head/controller poses, controller pose and trigger input with readback, and an XR image of the scaffold scene. A full Play/stop cycle restored the prior runtime setting and removed the temporary layer. Local evidence is under ignored `Logs/OperatorResearch/`.

For a new check, use Unity MCP to confirm the saved scene and ready Editor, enter Play, query Operator session state, then test input only after the session is FOCUSED. Release simulated buttons, stop Play, and inspect the console. A connected proxy alone is not a live session. Unity MCP remains the tool for scene and asset editing.

This setup verifies desktop behavior only. Headset launch, Android performance, haptics, and throwing feel still require device testing. The repository opens without the optional local binaries.
