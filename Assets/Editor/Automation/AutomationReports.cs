using System;
using System.IO;
using UnityEngine;

namespace VRBasketball.EditorAutomation
{
    internal static class AutomationReports
    {
        internal const string RequiredUnityVersion = "6000.0.58f2";
        internal static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        internal static string DirectoryPath => Path.Combine(ProjectRoot, "Logs", "Verification");

        internal static void Write(string name, object report)
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath, name), JsonUtility.ToJson(report, true));
        }

        internal static void ValidateUnityVersion()
        {
            if (Application.unityVersion != RequiredUnityVersion)
                throw new InvalidOperationException("Use Unity " + RequiredUnityVersion + " exactly. Current Editor: " + Application.unityVersion);
        }
    }

    [Serializable]
    internal sealed class QuestBuildSummary
    {
        public string status = "running";
        public string unityVersion = Application.unityVersion;
        public string[] scenes = Array.Empty<string>();
        public string outputPath;
        public string sha256;
        public string completedUtc;
        public double durationSeconds;
        public int warnings;
        public int errors;
        public string error;
    }

    [Serializable]
    internal sealed class TestSuiteSummary
    {
        public string suite;
        public string status = "running";
        public string unityVersion = Application.unityVersion;
        public string completedUtc;
        public string xmlPath;
        public int total;
        public int passed;
        public int failed;
        public int skipped;
        public int inconclusive;
        public double durationSeconds;
        public string error;
    }

    [Serializable]
    internal sealed class TestBatchSummary
    {
        public string status = "running";
        public string unityVersion = Application.unityVersion;
        public string startedUtc = DateTime.UtcNow.ToString("o");
        public string completedUtc;
        public string[] requestedSuites;
        public TestSuiteSummary[] suiteResults = Array.Empty<TestSuiteSummary>();
        public int nextSuiteIndex;
        public string currentSuite;
        public bool nextSuitePending;
        public string error;
    }
}
