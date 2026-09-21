using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace VRBasketball.EditorAutomation
{
    [InitializeOnLoad]
    public static class VerificationTests
    {
        private const string SessionKey = "VRBasketball.EditorAutomation.TestBatch";
        private static readonly TestRunnerApi Api;
        private static readonly MethodInfo IsRunActiveMethod = typeof(TestRunnerApi).GetMethod(
            "IsRunActive", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        static VerificationTests()
        {
            Api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.hideFlags = HideFlags.HideAndDontSave;
            Api.RegisterCallbacks(new ResultsCallbacks());
            // RunFinished precedes runner cleanup. Keep observing persisted pending work
            // until both the Editor and runner are idle, including after a domain reload.
            EditorApplication.update += StartNextSuite;
        }

        [MenuItem("Airball Arena VR/Automation/Run EditMode Tests")]
        public static void RunEditMode() => Begin("editmode");

        [MenuItem("Airball Arena VR/Automation/Run PlayMode Tests")]
        public static void RunPlayMode() => Begin("playmode");

        [MenuItem("Airball Arena VR/Automation/Run All Tests")]
        public static void RunAll() => Begin("editmode", "playmode");

        private static void Begin(params string[] suites)
        {
            if (ReadBatch() != null)
            {
                Debug.LogError("A verification run is already active. Wait for its report before starting another.");
                return;
            }
            try
            {
                AutomationReports.ValidateUnityVersion();
                if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Stop Play mode and wait for compilation before starting verification.");
                SaveBatch(new TestBatchSummary { requestedSuites = suites, nextSuitePending = true });
            }
            catch (Exception exception)
            {
                SessionState.EraseString(SessionKey);
                AutomationReports.Write("tests.json", new TestBatchSummary
                {
                    status = "failure", requestedSuites = suites, error = exception.Message,
                    completedUtc = DateTime.UtcNow.ToString("o")
                });
                Debug.LogError("Verification could not start: " + exception.Message);
            }
        }

        private static void StartNextSuite()
        {
            TestBatchSummary batch = ReadBatch();
            if (batch == null || !batch.nextSuitePending) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            try
            {
                // Test Framework 1.5.1 exposes this only internally; inspect its actual
                // state rather than assuming a result callback means cleanup is done.
                if (IsRunActiveMethod == null)
                    throw new InvalidOperationException("Cannot inspect Test Framework runner readiness.");
                if ((bool)IsRunActiveMethod.Invoke(null, null)) return;
                batch.currentSuite = batch.requestedSuites[batch.nextSuiteIndex];
                batch.nextSuitePending = false;
                SaveBatch(batch);
                AutomationReports.Write(batch.currentSuite + "-tests.json", new TestSuiteSummary { suite = batch.currentSuite });
                Api.Execute(new ExecutionSettings(new Filter
                {
                    testMode = batch.currentSuite == "editmode" ? TestMode.EditMode : TestMode.PlayMode
                }));
            }
            catch (Exception exception)
            {
                if (!batch.nextSuitePending)
                    CompleteSuite(new TestSuiteSummary { suite = batch.currentSuite, status = "failure", error = exception.Message });
                else
                {
                    batch.status = "failure";
                    batch.error = exception.Message;
                    batch.completedUtc = DateTime.UtcNow.ToString("o");
                    SaveBatch(batch);
                    SessionState.EraseString(SessionKey);
                    Debug.LogError("Verification could not continue: " + exception.Message);
                }
            }
        }

        private static TestBatchSummary ReadBatch()
        {
            string json = SessionState.GetString(SessionKey, string.Empty);
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<TestBatchSummary>(json);
        }

        private static void SaveBatch(TestBatchSummary batch)
        {
            SessionState.SetString(SessionKey, JsonUtility.ToJson(batch));
            AutomationReports.Write("tests.json", batch);
        }

        private static void CompleteSuite(TestSuiteSummary suite)
        {
            TestBatchSummary batch = ReadBatch();
            if (batch == null || batch.nextSuitePending) return;
            suite.completedUtc = DateTime.UtcNow.ToString("o");
            AutomationReports.Write(suite.suite + "-tests.json", suite);
            batch.suiteResults = batch.suiteResults.Concat(new[] { suite }).ToArray();
            batch.nextSuiteIndex++;
            if (batch.nextSuiteIndex < batch.requestedSuites.Length)
            {
                batch.nextSuitePending = true;
                SaveBatch(batch);
                return;
            }
            batch.status = batch.suiteResults.Any(result => result.status == "failure") ? "failure" :
                batch.suiteResults.Any(result => result.status == "not_ready") ? "not_ready" : "success";
            batch.completedUtc = DateTime.UtcNow.ToString("o");
            SaveBatch(batch);
            SessionState.EraseString(SessionKey);
            Debug.Log("Verification finished: " + batch.status + ". Reports: " + AutomationReports.DirectoryPath);
        }

        private sealed class ResultsCallbacks : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                TestBatchSummary batch = ReadBatch();
                if (batch == null || batch.nextSuitePending) return;
                var suite = new TestSuiteSummary
                {
                    suite = batch.currentSuite, passed = result.PassCount, failed = result.FailCount,
                    skipped = result.SkipCount, inconclusive = result.InconclusiveCount,
                    durationSeconds = result.Duration
                };
                suite.total = suite.passed + suite.failed + suite.skipped + suite.inconclusive;
                suite.status = suite.total == 0 ? "not_ready" :
                    suite.failed > 0 || suite.inconclusive > 0 || suite.skipped > 0 || result.ResultState.StartsWith("Failed", StringComparison.Ordinal)
                    ? "failure" : suite.passed == 0 ? "not_ready" : "success";
                if (suite.status == "not_ready") suite.error = "No passing tests were executed; gameplay readiness has not been verified.";
                try
                {
                    Directory.CreateDirectory(AutomationReports.DirectoryPath);
                    if (suite.suite != "editmode" && suite.suite != "playmode")
                        throw new InvalidOperationException("Unexpected verification suite name.");
                    suite.xmlPath = Path.Combine(AutomationReports.DirectoryPath, suite.suite + "-results.xml");
                    // Only replace our fixed generated result file inside Logs/Verification.
                    if (File.Exists(suite.xmlPath)) File.Delete(suite.xmlPath);
                    TestRunnerApi.SaveResultToFile(result, suite.xmlPath);
                    if (!File.Exists(suite.xmlPath) || new FileInfo(suite.xmlPath).Length == 0)
                        throw new IOException("The NUnit XML export was not written.");
                }
                catch (Exception exception)
                {
                    suite.status = "failure";
                    suite.error = "Could not export test results: " + exception.Message;
                }
                CompleteSuite(suite);
            }

            public void OnError(string message)
            {
                TestBatchSummary batch = ReadBatch();
                if (batch == null || batch.nextSuitePending) return;
                CompleteSuite(new TestSuiteSummary
                {
                    suite = batch.currentSuite,
                    status = message.IndexOf("no tests", StringComparison.OrdinalIgnoreCase) >= 0 ? "not_ready" : "failure",
                    error = message
                });
            }
        }
    }
}
