# Verification and deployment

## Project open in Unity

Use **VR Basketball > Automation > Run EditMode Tests**, **Run PlayMode Tests**, **Run All Tests**, or **Build Android APK**. Runs are asynchronous: wait for the JSON result, then inspect totals and the console. Test results are in `Logs/Verification/{editmode,playmode}-tests.json` with NUnit XML; the combined status is `Logs/Verification/tests.json`. Build status is `Logs/Verification/quest-build.json`, and the default APK is `Builds/Android/VRBasketball.apk`.

`success` means a completed pass; `failure` is a failed run; `running` is pending; `not_ready` includes an empty suite. The current PlayMode suite is empty. Save the scene before building. The build helper requires Input System only; after changing Active Input Handling, restart Unity.

Unity MCP's local server can start automatically: **Window > MCP for Unity > Toggle MCP Window > Advanced > Auto-Start Server on Editor Load**. This is a per-user Editor preference, enabled on the setup machine.

## Project closed in Unity

From the project root, run `Automation\Verify-All.cmd` for EditMode, PlayMode, and Android build. The launcher needs PowerShell 7.2+ (`PWSH_EXE` may point to its executable). Pass `-UnityPath` to `Automation\Verify-All.ps1` if Unity is installed outside `C:/Program Files/Unity/Hub/Editor/6000.0.58f2/Editor/Unity.exe`.

Useful options: `-ProjectPath`, `-UnityPath`, `-BuildOutput`, `-ProcessTimeoutSeconds`, and `-AllowEmptyPlayMode`. The last permits a scaffold build after an empty PlayMode suite but leaves verification incomplete. Results appear under `Logs/Verification/cli-<run>/verification.json`; exit codes are **0 pass**, **1 failure**, **2 incomplete**. The script refuses to run against an open project lock. An older APK is not evidence of a fresh build.

## Headset deployment

`Automation\Deploy-Quest.cmd` lists devices without installing. After choosing the intended serial, `Automation\Deploy-Quest.cmd -Serial YOUR_DEVICE_SERIAL` installs and launches the default APK. Optional flags include `-ApkPath`, `-AdbPath`, `-PackageId`, and `-CaptureSeconds`. The script requires one ready matching device and preserves app data. Inspect `Logs/Verification/deploy-<run>/deployment.json` and the captured logs, then perform the [headset checks](HeadsetChecks.md). A successful install/launch is not a gameplay pass.

`Builds/` and `Logs/Verification/` are ignored by Git. Report actual outcomes and provide generated evidence separately when needed.
