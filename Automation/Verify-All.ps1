#Requires -Version 7.2
<#
.SYNOPSIS
Checks project requirements, runs both Unity test suites, and builds an Android APK.
.DESCRIPTION
Run with the project closed in Unity. An open Editor is never stopped by this script.
Exit codes: 0 verified; 1 failed; 2 incomplete (an explicitly allowed empty PlayMode suite).
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.0.58f2/Editor/Unity.exe',
    [string]$BuildOutput,
    [switch]$AllowEmptyPlayMode,
    [ValidateRange(60, 7200)][int]$ProcessTimeoutSeconds = 1200
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ProjectRequirements {
    param([string]$Root)
    $versionFile = Join-Path $Root 'ProjectSettings/ProjectVersion.txt'
    $version = [regex]::Match((Get-Content -LiteralPath $versionFile -Raw), '(?m)^m_EditorVersion:\s*(\S+)').Groups[1].Value
    if ($version -ne '6000.0.58f2') { throw "Required Unity version is 6000.0.58f2; project specifies '$version'." }
    $settings = Get-Content -LiteralPath (Join-Path $Root 'ProjectSettings/ProjectSettings.asset') -Raw
    $inputHandlers = [regex]::Matches($settings, '(?m)^  activeInputHandler:[ \t]*(\d+)[ \t]*\r?$')
    if ($inputHandlers.Count -ne 1) { throw 'Cannot inspect Active Input Handling in ProjectSettings/ProjectSettings.asset.' }
    if ($inputHandlers[0].Groups[1].Value -ne '1') { throw 'Set Active Input Handling to Input System Package (New), save settings, and restart the Editor before Android verification.' }
    foreach ($relativePath in @('Packages/manifest.json', 'Packages/packages-lock.json')) {
        $json = Get-Content -LiteralPath (Join-Path $Root $relativePath) -Raw | ConvertFrom-Json -AsHashtable
        if (-not $json.ContainsKey('dependencies')) { throw "Missing dependencies object in $relativePath." }
        $banned = @($json.dependencies.Keys | Where-Object {
            $_ -eq 'com.unity.xr.interaction.toolkit' -or $_ -match '^com\.meta\.xr\.(?:sdk\.)?interaction(?:\.|$)' -or $_ -eq 'com.meta.xr.sdk.all' -or $_ -eq 'com.oculus.interaction'
        })
        if ($banned.Count) { throw "Prebuilt interaction package in ${relativePath}: $($banned -join ', ')." }
    }
    return $version
}

function Assert-EditorClosed {
    param([string]$Root)
    $lockPath = Join-Path $Root 'Temp/UnityLockfile'
    if (-not (Test-Path -LiteralPath $lockPath)) { return }
    try {
        $lockStream = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $lockStream.Dispose()
    } catch {
        throw 'This project is open in Unity, or its lock is inaccessible. Use the connected Unity MCP and the Airball Arena VR/Automation/Run All Tests and Build Android APK menus. For this CLI script, save and close the Editor yourself first.'
    }
}

function Invoke-UnityVerificationProcess {
    param([string]$Executable, [string[]]$Arguments, [string]$LogPrefix, [int]$TimeoutSeconds)
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $stdout = $null
    $stderr = $null
    try {
        if (-not $process.Start()) { throw 'Unity did not start.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "The batch Unity process started by this script exceeded $TimeoutSeconds seconds."
        }
        return $process.ExitCode
    } finally {
        if ($null -ne $stdout) {
            [IO.File]::WriteAllText("$LogPrefix.stdout.log", $stdout.GetAwaiter().GetResult())
            [IO.File]::WriteAllText("$LogPrefix.stderr.log", $stderr.GetAwaiter().GetResult())
        }
        $process.Dispose()
    }
}

function Read-UnityTestResults {
    param([string]$Path, [string]$Mode, [int]$ProcessExitCode)
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Mode produced no test result XML (exit $ProcessExitCode)." }
    $xml = [xml](Get-Content -LiteralPath $Path -Raw)
    $cases = @($xml.SelectNodes('//test-case'))
    $passed = @($cases | Where-Object { $_.GetAttribute('result') -in @('Passed', 'Success') }).Count
    $failed = @($cases | Where-Object { $_.GetAttribute('result') -in @('Failed', 'Failure', 'Error') }).Count
    $other = $cases.Count - $passed - $failed
    $rootResult = $xml.DocumentElement.GetAttribute('result')
    $status = 'success'
    if ($ProcessExitCode -ne 0 -or $failed -gt 0 -or $other -gt 0 -or $rootResult -in @('Failed', 'Failure', 'Error')) { $status = 'failure' }
    elseif ($cases.Count -eq 0) { $status = 'not_ready' }
    if ($cases.Count -gt 0 -and $rootResult -and $rootResult -notin @('Passed', 'Success')) { $status = 'failure' }
    return [ordered]@{
        mode = $Mode; status = $status; total = $cases.Count; passed = $passed
        failed = $failed; skippedOrInconclusive = $other; processExitCode = $ProcessExitCode; resultsPath = $Path
    }
}

function Assert-QuestBuildResult {
    param([string]$ReportPath, [string]$ExpectedOutput, [datetime]$StartedUtc, [int]$ProcessExitCode)
    if ($ProcessExitCode -ne 0) { throw "Unity build process exited with $ProcessExitCode." }
    if (-not (Test-Path -LiteralPath $ReportPath)) { throw 'Build produced no quest-build.json report.' }
    $reportFile = Get-Item -LiteralPath $ReportPath
    if ($reportFile.LastWriteTimeUtc -lt $StartedUtc) { throw 'Build report predates this build; refusing a stale success.' }
    $report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
    if ($report.status -ne 'success') { throw "BuildReport did not succeed: $($report.error)" }
    if ($report.unityVersion -ne '6000.0.58f2') { throw "Build used unexpected Unity version '$($report.unityVersion)'." }
    if ([IO.Path]::GetFullPath($report.outputPath) -ne [IO.Path]::GetFullPath($ExpectedOutput)) { throw 'BuildReport output does not match the requested APK.' }
    if (@($report.scenes).Count -eq 0) { throw 'BuildReport contains no enabled scenes.' }
    if (-not (Test-Path -LiteralPath $ExpectedOutput -PathType Leaf)) { throw 'BuildReport succeeded, but its APK is absent.' }
    $apk = Get-Item -LiteralPath $ExpectedOutput
    if ($apk.Length -eq 0 -or $apk.LastWriteTimeUtc -lt $StartedUtc) { throw 'APK is empty or predates this build.' }
    $hash = (Get-FileHash -LiteralPath $ExpectedOutput -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $report.sha256.ToLowerInvariant()) { throw 'APK SHA-256 does not match BuildReport.' }
    return $report
}

# Dot-sourcing exposes the small validation helpers without launching Unity.
if ($MyInvocation.InvocationName -eq '.') { return }

$summary = [ordered]@{ status = 'running'; startedUtc = [datetime]::UtcNow.ToString('o'); tests = @(); build = $null; error = $null }
$summaryPath = $null
$exitCode = 1
try {
    $ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
    $runFolder = Join-Path $ProjectPath ('Logs/Verification/cli-' + [datetime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
    New-Item -ItemType Directory -Path $runFolder -Force | Out-Null
    $summaryPath = Join-Path $runFolder 'verification.json'
    $summary.projectPath = $ProjectPath
    $summary.unityVersion = Assert-ProjectRequirements $ProjectPath
    $UnityPath = (Resolve-Path -LiteralPath $UnityPath).Path
    $binaryVersion = (Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion
    if ($binaryVersion -notmatch '^6000\.0\.58f2(?:_|$)') { throw "Unity executable version '$binaryVersion' does not match 6000.0.58f2." }
    Assert-EditorClosed $ProjectPath
    if (-not $BuildOutput) { $BuildOutput = Join-Path $ProjectPath 'Builds/Android/AirballArenaVR.apk' }
    if (-not [IO.Path]::IsPathRooted($BuildOutput)) { $BuildOutput = Join-Path $ProjectPath $BuildOutput }
    $BuildOutput = [IO.Path]::GetFullPath($BuildOutput)
    if ([IO.Path]::GetExtension($BuildOutput) -ne '.apk') { throw 'BuildOutput must name an .apk file.' }
    $incomplete = $false
    foreach ($mode in @('EditMode', 'PlayMode')) {
        Write-Host "Running $mode tests..."
        $prefix = Join-Path $runFolder $mode.ToLowerInvariant()
        $xmlPath = "$prefix-results.xml"
        # Do not use -quit with -runTests: the test runner owns batch termination.
        $processExit = Invoke-UnityVerificationProcess $UnityPath @('-batchmode', '-nographics', '-projectPath', $ProjectPath, '-runTests', '-testPlatform', $mode, '-testResults', $xmlPath, '-logFile', "$prefix-unity.log") $prefix $ProcessTimeoutSeconds
        $result = Read-UnityTestResults $xmlPath $mode $processExit
        $summary.tests += $result
        if ($result.status -eq 'not_ready' -and $mode -eq 'PlayMode' -and $AllowEmptyPlayMode -and $processExit -eq 0) {
            $incomplete = $true
            Write-Warning 'PlayMode has no tests. Continuing the bootstrap build; verification remains incomplete.'
        } elseif ($result.status -ne 'success') {
            throw "$mode verification is $($result.status): $($result.total) tests, $($result.failed) failures, $($result.skippedOrInconclusive) skipped/inconclusive; process exit $processExit."
        }
    }
    Write-Host 'Building Android APK...'
    $buildStart = [datetime]::UtcNow
    $prefix = Join-Path $runFolder 'build'
    $processExit = Invoke-UnityVerificationProcess $UnityPath @('-batchmode', '-nographics', '-quit', '-projectPath', $ProjectPath, '-buildTarget', 'Android', '-executeMethod', 'VRBasketball.EditorAutomation.QuestBuild.BuildFromCommandLine', '-buildOutput', $BuildOutput, '-logFile', "$prefix-unity.log") $prefix $ProcessTimeoutSeconds
    $reportPath = Join-Path $ProjectPath 'Logs/Verification/quest-build.json'
    $summary.build = Assert-QuestBuildResult $reportPath $BuildOutput $buildStart $processExit
    Copy-Item -LiteralPath $reportPath -Destination (Join-Path $runFolder 'quest-build.json')
    $summary.status = if ($incomplete) { 'not_ready' } else { 'success' }
    $exitCode = if ($incomplete) { 2 } else { 0 }
} catch {
    $summary.status = 'failure'
    $summary.error = $_.Exception.Message
    Write-Host $summary.error -ForegroundColor Red
} finally {
    $summary.completedUtc = [datetime]::UtcNow.ToString('o')
    if ($summaryPath) {
        $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding utf8
        Write-Host "Verification $($summary.status). Report: $summaryPath"
    }
}
exit $exitCode
