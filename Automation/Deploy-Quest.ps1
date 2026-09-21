#Requires -Version 7.2
<#
.SYNOPSIS
Lists devices, or installs and launches an APK on an explicitly selected device.
.DESCRIPTION
Without Serial this only lists devices and exits. Install uses -r to keep app data.
Does not uninstall, clear app data, clear device logs, or close the Unity Editor.
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$ApkPath,
    [string]$Serial,
    [string]$AdbPath,
    [string]$PackageId,
    [ValidateRange(1, 60)][int]$CaptureSeconds = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-AndroidPackageId {
    param([string]$Root)
    $settings = Get-Content -LiteralPath (Join-Path $Root 'ProjectSettings/ProjectSettings.asset') -Raw
    $identifierBlock = [regex]::Match($settings, '(?m)^  applicationIdentifier:\r?\n(?<platforms>(?:    .*(?:\r?\n|$))+)')
    $match = [regex]::Match($identifierBlock.Groups['platforms'].Value, '(?m)^    Android:\s*(\S+)')
    if (-not $match.Success) { throw 'No explicit Android application identifier found. Set it in Unity, or pass PackageId matching the APK.' }
    return $match.Groups[1].Value.Trim('"', "'")
}

function ConvertFrom-AdbDevices {
    param([string]$Text)
    foreach ($line in ($Text -split '\r?\n')) {
        if ($line -match '^([^\s]+)\s+(device|offline|unauthorized)\b(.*)$') {
            [pscustomobject]@{ serial = $Matches[1]; state = $Matches[2]; details = $Matches[3].Trim() }
        }
    }
}

function Invoke-DeploymentAdb {
    param([string]$Executable, [string[]]$Arguments, [string]$LogPath)
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) { throw 'ADB did not start.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(180000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw 'ADB command exceeded 180 seconds.'
        }
        $text = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText($LogPath, $text)
        if ($process.ExitCode -ne 0) { throw "ADB exited with $($process.ExitCode). See $LogPath." }
        return $text
    } finally { $process.Dispose() }
}

if ($MyInvocation.InvocationName -eq '.') { return }

$summary = [ordered]@{ status = 'running'; startedUtc = [datetime]::UtcNow.ToString('o'); serial = $Serial; packageId = $null; apkPath = $null; sha256 = $null; devices = @(); error = $null }
$summaryPath = $null
$exitCode = 1
$selected = @()
try {
    $ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
    $runFolder = Join-Path $ProjectPath ('Logs/Verification/deploy-' + [datetime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
    New-Item -ItemType Directory -Path $runFolder -Force | Out-Null
    $summaryPath = Join-Path $runFolder 'deployment.json'
    if (-not $AdbPath) {
        $bundledAdb = 'C:/Program Files/Unity/Hub/Editor/6000.0.58f2/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
        $command = Get-Command adb -ErrorAction SilentlyContinue
        $AdbPath = if (Test-Path -LiteralPath $bundledAdb) { $bundledAdb } elseif ($command) { $command.Source } else { throw 'ADB not found. Pass AdbPath to the platform-tools adb.exe already installed with Android Build Support.' }
    }
    $AdbPath = (Resolve-Path -LiteralPath $AdbPath).Path
    $deviceText = Invoke-DeploymentAdb $AdbPath @('devices', '-l') (Join-Path $runFolder 'devices.log')
    $devices = @(ConvertFrom-AdbDevices $deviceText)
    $summary.devices = $devices
    if ($devices.Count) { $devices | Format-Table serial, state, details | Out-Host } else { Write-Host 'No ADB devices connected.' }
    if (-not $Serial) {
        $summary.status = 'listed'
        Write-Host 'No install performed. Run again with -Serial exactly matching a ready device.'
        $exitCode = 0
    } else {
        $selected = @($devices | Where-Object { $_.serial -ceq $Serial -and $_.state -eq 'device' })
        if ($selected.Count -ne 1) { throw "Serial '$Serial' is not one connected, authorized device. Check the devices list and authorize USB debugging in the headset." }
        if (-not $ApkPath) { $ApkPath = Join-Path $ProjectPath 'Builds/Android/AirballArenaVR.apk' }
        if (-not [IO.Path]::IsPathRooted($ApkPath)) { $ApkPath = Join-Path $ProjectPath $ApkPath }
        $ApkPath = (Resolve-Path -LiteralPath $ApkPath).Path
        if ([IO.Path]::GetExtension($ApkPath) -ne '.apk' -or (Get-Item -LiteralPath $ApkPath).Length -eq 0) { throw 'ApkPath must point to a nonempty .apk file.' }
        if (-not $PackageId) { $PackageId = Get-AndroidPackageId $ProjectPath }
        if ($PackageId -notmatch '^[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+$') { throw 'PackageId must be a valid Android package identifier.' }
        $summary.packageId = $PackageId
        $summary.apkPath = $ApkPath
        $summary.sha256 = (Get-FileHash -LiteralPath $ApkPath -Algorithm SHA256).Hash.ToLowerInvariant()
        Write-Host "Installing $PackageId on $Serial..."
        $install = Invoke-DeploymentAdb $AdbPath @('-s', $Serial, 'install', '-r', $ApkPath) (Join-Path $runFolder 'install.log')
        if ($install -notmatch '(?m)^Success\s*$') { throw 'ADB did not confirm installation success; inspect install.log.' }
        $launch = Invoke-DeploymentAdb $AdbPath @('-s', $Serial, 'shell', 'am', 'start', '-W', '-a', 'android.intent.action.MAIN', '-c', 'android.intent.category.LAUNCHER', $PackageId) (Join-Path $runFolder 'launch.log')
        if ($launch -match '(?im)^\s*(Error|Exception|Failure)\b' -or $launch -notmatch '(?im)^\s*Status:\s*ok\s*$') { throw 'Android did not confirm a successful launch; inspect launch.log. Verify PackageId matches the APK.' }
        Start-Sleep -Seconds $CaptureSeconds
        $appPid = (Invoke-DeploymentAdb $AdbPath @('-s', $Serial, 'shell', 'pidof', $PackageId) (Join-Path $runFolder 'pid.log')).Trim()
        if ($appPid -notmatch '^\d+(?:\s+\d+)*$') { throw 'The launched application is no longer running; inspect launch.log and device logs.' }
        $appPid = ($appPid -split '\s+')[0]
        Invoke-DeploymentAdb $AdbPath @('-s', $Serial, 'logcat', '-d', '-v', 'threadtime', "--pid=$appPid", '-t', '500') (Join-Path $runFolder 'app-logcat.log') | Out-Null
        $summary.status = 'success'
        $summary.appProcessId = $appPid
        Write-Host 'APK installed and app launch confirmed. Headset playtesting is still required.'
        $exitCode = 0
    }
} catch {
    $summary.status = 'failure'
    $summary.error = $_.Exception.Message
    Write-Host $summary.error -ForegroundColor Red
    # Preserve a bounded crash/Unity log on a failed install or launch as well.
    if ($Serial -and $selected -and $AdbPath) {
        try {
            Invoke-DeploymentAdb $AdbPath @('-s', $Serial, 'logcat', '-d', '-v', 'threadtime', '-t', '500', 'Unity:I', 'AndroidRuntime:E', '*:S') (Join-Path $runFolder 'failure-logcat.log') | Out-Null
        } catch { Write-Warning 'Failure log capture was unavailable; inspect the command logs.' }
    }
} finally {
    $summary.completedUtc = [datetime]::UtcNow.ToString('o')
    if ($summaryPath) {
        $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8
        Write-Host "Deployment $($summary.status). Report: $summaryPath"
    }
}
exit $exitCode
