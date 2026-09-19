using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VRBasketball.EditorAutomation
{
    public static class QuestBuild
    {
        private const string PackageId = "com.vrbasketball.game";

        [MenuItem("VR Basketball/Automation/Build Android APK")]
        public static void BuildFromMenu()
        {
            Build(null);
        }

        // Use -buildTarget Android alongside -executeMethod in batch mode.
        public static void BuildFromCommandLine()
        {
            string output = null;
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length; index++)
            {
                if (!string.Equals(args[index], "-buildOutput", StringComparison.OrdinalIgnoreCase)) continue;
                output = index + 1 < args.Length ? args[index + 1] : string.Empty;
                break;
            }
            if (!Build(output) && Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static bool Build(string output)
        {
            var summary = new QuestBuildSummary();
            var timer = Stopwatch.StartNew();
            bool success = false;
            try
            {
                AutomationReports.Write("quest-build.json", summary);
                AutomationReports.ValidateUnityVersion();
                ValidateSettings();
                ValidatePackages();
                summary.scenes = EnabledScenes();
                summary.outputPath = ResolveOutput(output);
                Directory.CreateDirectory(Path.GetDirectoryName(summary.outputPath));
                EditorUserBuildSettings.buildAppBundle = false;
                AutomationReports.Write("quest-build.json", summary);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = summary.scenes,
                    locationPathName = summary.outputPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });
                summary.warnings = (int)report.summary.totalWarnings;
                summary.errors = (int)report.summary.totalErrors;
                summary.durationSeconds = report.summary.totalTime.TotalSeconds;
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Android build ended with " + report.summary.result + ". Read the Unity build log.");
                if (!File.Exists(summary.outputPath))
                    throw new FileNotFoundException("Build succeeded but the APK was not found.", summary.outputPath);
                using (var algorithm = SHA256.Create())
                using (var stream = File.OpenRead(summary.outputPath))
                    summary.sha256 = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                summary.status = "success";
                success = true;
                Debug.Log("Android APK ready: " + summary.outputPath);
            }
            catch (Exception exception)
            {
                summary.status = "failure";
                summary.error = exception.Message;
                summary.errors = Math.Max(1, summary.errors);
                Debug.LogError("Android build failed: " + exception.Message);
            }
            finally
            {
                summary.completedUtc = DateTime.UtcNow.ToString("o");
                if (summary.durationSeconds == 0) summary.durationSeconds = timer.Elapsed.TotalSeconds;
                AutomationReports.Write("quest-build.json", summary);
            }
            return success;
        }

        private static void ValidateSettings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play mode and wait for compilation before building.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Switch the active build target to Android before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support, SDK/NDK tools, and OpenJDK for this Editor.");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                throw new InvalidOperationException("Android Scripting Backend must be IL2CPP.");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                throw new InvalidOperationException("Android Target Architectures must contain ARM64 only.");
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != PackageId)
                throw new InvalidOperationException("Set the Android package name to " + PackageId + ".");
            ValidateInputHandling();
            for (int index = 0; index < EditorSceneManager.sceneCount; index++)
                if (EditorSceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save all open scenes before building.");
        }

        private static void ValidateInputHandling()
        {
            var playerSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")
                .OfType<PlayerSettings>().SingleOrDefault();
            if (playerSettings == null)
            {
                // Input System 1.14.2 uses this Unity 6 global-settings field too.
                var globalField = typeof(BuildProfile).GetField("s_GlobalPlayerSettings", BindingFlags.Static | BindingFlags.NonPublic);
                playerSettings = globalField?.GetValue(null) as PlayerSettings;
            }
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile != null)
            {
                var overrideField = typeof(BuildProfile).GetField("m_PlayerSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                if (overrideField == null)
                    throw new InvalidOperationException("Cannot inspect active Build Profile Player Settings overrides.");
                var profileSettings = overrideField.GetValue(profile) as PlayerSettings;
                if (profileSettings != null) playerSettings = profileSettings;
            }
            if (playerSettings == null)
                throw new InvalidOperationException("Cannot inspect effective Player Settings for input handling.");
            using (var serialized = new SerializedObject(playerSettings))
            {
                serialized.Update();
                var input = serialized.FindProperty("activeInputHandler");
                if (input == null || input.propertyType != SerializedPropertyType.Integer)
                    throw new InvalidOperationException("Cannot inspect Active Input Handling in effective Player Settings.");
                if (input.intValue != 1)
                    throw new InvalidOperationException("Set Active Input Handling to Input System Package (New), save settings, and restart the Editor before building for Android.");
            }
        }

        private static void ValidatePackages()
        {
            // The All-in-One aggregator includes Interaction SDK; tracking/rendering packages are allowed.
            var forbidden = new Regex("\"(?:(?:com\\.unity\\.xr\\.interaction\\.toolkit|com\\.meta\\.xr\\.(?:sdk\\.interaction|interaction|sdk\\.all))(?:\\.[^\"]+)?|com\\.oculus\\.interaction)\"", RegexOptions.IgnoreCase);
            foreach (string name in new[] { "manifest.json", "packages-lock.json" })
            {
                string path = Path.Combine(AutomationReports.ProjectRoot, "Packages", name);
                if (forbidden.IsMatch(File.ReadAllText(path)))
                    throw new InvalidOperationException("Remove forbidden interaction packages (including Meta All-in-One, which includes Interaction SDK) from Packages/" + name + ".");
            }
        }

        private static string[] EnabledScenes()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("Enable at least one saved scene in Build Profiles.");
            foreach (string scene in scenes)
                if (string.IsNullOrEmpty(scene) || !File.Exists(Path.Combine(AutomationReports.ProjectRoot, scene)))
                    throw new InvalidOperationException("Enabled scene is missing or unsaved: " + scene);
            return scenes;
        }

        private static string ResolveOutput(string output)
        {
            if (output == null) output = Path.Combine(AutomationReports.ProjectRoot, "Builds", "Android", "VRBasketball.apk");
            if (string.IsNullOrWhiteSpace(output) || !string.Equals(Path.GetExtension(output), ".apk", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("-buildOutput must point to an .apk file.");
            return Path.GetFullPath(Path.IsPathRooted(output) ? output : Path.Combine(AutomationReports.ProjectRoot, output));
        }
    }
}
