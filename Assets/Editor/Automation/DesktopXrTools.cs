#if UNITY_EDITOR_WIN
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace VRBasketball.EditorAutomation
{
    // Local desktop instrumentation only. No Core SDK, runtime gameplay code,
    // Android layer registration, or system-wide OpenXR runtime changes.
    [InitializeOnLoad]
    public static class DesktopXrTools
    {
        [Serializable]
        private sealed class LayerManifest
        {
            public LayerData api_layer;
        }

        [Serializable]
        private sealed class LayerData
        {
            public string name;
            public string library_path;
            public string api_version;
            public string implementation_version;
            public string description;
        }

        private const string LayerName = "XR_APILAYER_METAX_operator";
        // A Simulator request older than this is stale (for example, Play never started).
        private const float RequestLifetimeSeconds = 30f;
        private static string Prefix => "VRBasketball.DesktopXR." + Hash128.Compute(Application.dataPath) + ".";
        private static string RequestKey => Prefix + "SimulatorPlayRequestedAt";
        private static string FeatureWasDirtyKey => Prefix + "FeatureWasDirty";
        // Meta XR Core's Simulator toggle; its toolbar button calls the same code.
        private const string MetaSimActivateMenu = "Meta/Meta XR Simulator/Activate";
        private const string MetaSimDeactivateMenu = "Meta/Meta XR Simulator/Deactivate";
        private static string ActivatedMetaSimKey => Prefix + "ActivatedMetaSimulator";

        static DesktopXrTools()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += RestoreAfterInterruptedSession;
        }

        [MenuItem("Airball Arena VR/Desktop XR/Configure Experimental Operator")]
        public static void ConfigureFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before configuring desktop XR tools.");
            string manifest = EditorUtility.OpenFilePanel("Select the Windows Operator layer JSON", "", "json");
            if (string.IsNullOrEmpty(manifest)) return;
            ConfigureFromPaths(manifest);
        }

        public static void ConfigureFromPaths(string operatorManifest)
        {
            AutomationReports.ValidateUnityVersion();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before configuring desktop XR tools.");
            RequireFile(operatorManifest);
            RequireFile(Path.Combine(Path.GetDirectoryName(operatorManifest), "XrApiLayer_METAX_operator.dll"));
            EditorPrefs.SetString(Prefix + "OperatorManifest", Path.GetFullPath(operatorManifest));
            Debug.Log("Operator layer configured locally. Use Airball Arena VR > Desktop XR > Play in Simulator for a Simulator session with the Operator.");
        }

        // Normal Play uses the system OpenXR runtime (for example, Quest Link) unless Meta's
        // Simulator toggle is on. This turns that toggle on for one Play session, adds the
        // Operator layer, and restores both when Play stops.
        [MenuItem("Airball Arena VR/Desktop XR/Play in Simulator")]
        public static void PlayInSimulator()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before starting a Simulator session.");
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed)
                throw new InvalidOperationException("Wait for scripts to compile without errors before starting a Simulator session.");
            // Without this, Play freezes after the first frames whenever Unity is not the focused window.
            if (!PlayerSettings.runInBackground)
                throw new InvalidOperationException("Enable Player Settings > Resolution and Presentation > Run In Background before a Simulator session.");
            RequireFile(EditorPrefs.GetString(Prefix + "OperatorManifest"));
            if (!IsMetaSimulatorActive())
            {
                EditorApplication.ExecuteMenuItem(MetaSimActivateMenu);
                if (!IsMetaSimulatorActive())
                    throw new InvalidOperationException("Meta XR Simulator could not be activated. Check Meta > Meta XR Simulator.");
                SessionState.SetBool(ActivatedMetaSimKey, true);
            }
            SessionState.SetFloat(RequestKey, (float)EditorApplication.timeSinceStartup);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Airball Arena VR/Desktop XR/Play in Simulator", true)]
        private static bool CanPlayInSimulator() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Airball Arena VR/Desktop XR/Show Local Status")]
        public static void ShowStatus()
        {
            Debug.Log("Desktop XR: normal Play uses the system OpenXR runtime unless Meta's Simulator toggle is on"
                + "; configured Operator files: " + File.Exists(EditorPrefs.GetString(Prefix + "OperatorManifest"))
                + "; Meta Simulator active: " + IsMetaSimulatorActive());
        }

        // Mirrors Meta's own check: its toggle selects the Simulator through this variable.
        private static bool IsMetaSimulatorActive()
        {
            string selected = Environment.GetEnvironmentVariable("XR_SELECTED_RUNTIME_JSON");
            return !string.IsNullOrEmpty(selected) && selected.EndsWith("meta_openxr_simulator.json", StringComparison.OrdinalIgnoreCase);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (Application.isBatchMode) return;
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (!ConsumeSimulatorRequest())
                {
                    // A stale request must not leave Meta's toggle on for a normal Play.
                    Restore();
                    return;
                }
                try
                {
                    Prepare();
                    Debug.Log("Play is using the Simulator with the Operator layer; desktop XR settings are restored when Play stops.");
                }
                catch (Exception exception)
                {
                    Restore();
                    EditorApplication.isPlaying = false;
                    Debug.LogError("Simulator session could not start: " + exception.Message);
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                Restore();
                RepairSavedSettings();
            }
        }

        // Every Play entry clears the request, so a stale one cannot redirect a later normal Play.
        private static bool ConsumeSimulatorRequest()
        {
            float requestedAt = SessionState.GetFloat(RequestKey, -1f);
            SessionState.EraseFloat(RequestKey);
            return requestedAt >= 0f && EditorApplication.timeSinceStartup - requestedAt < RequestLifetimeSeconds;
        }

        private static void Prepare()
        {
            AutomationReports.ValidateUnityVersion();
            string manifest = EditorPrefs.GetString(Prefix + "OperatorManifest");
            RequireFile(manifest);
            if (!IsMetaSimulatorActive())
                throw new InvalidOperationException("Meta XR Simulator is not active.");
            ApiLayersFeature feature = DesktopFeature();
            EditorPrefs.SetString(Prefix + "OriginalFeature", JsonUtility.ToJson(feature));
            SessionState.SetBool(FeatureWasDirtyKey, EditorUtility.IsDirty(feature));
            EditorPrefs.SetBool(Prefix + "PendingRestore", true);
            if (!feature.apiLayers.IsEnabled(LayerName))
            {
                // Reuse Unity's previously imported copy when it exists. The DLL can remain
                // loaded for the lifetime of the Editor process, so overwriting it on a repeat
                // Play-mode cycle would fail even though the files are already valid.
                string importedManifest = Path.Combine(
                    Application.dataPath,
                    "XR",
                    "APILayers~",
                    "WindowsLayers",
                    "x64",
                    Path.GetFileName(manifest));
                string importedLibrary = Path.Combine(
                    Path.GetDirectoryName(importedManifest),
                    "XrApiLayer_METAX_operator.dll");
                bool alreadyRegistered = feature.apiLayers.collection.Any(
                    layer => layer.name == LayerName && layer.libraryArchitecture == Architecture.X64);
                if (!alreadyRegistered)
                {
                    bool importedFilesExist = File.Exists(importedManifest) && File.Exists(importedLibrary);
                    bool registered = importedFilesExist
                        ? RegisterExistingImportedLayer(feature.apiLayers, importedManifest)
                        : feature.apiLayers.TryAdd(manifest, Architecture.X64, BuildTargetGroup.Standalone, out _);
                    if (!registered)
                        throw new InvalidOperationException("Unity could not import the Windows Operator layer.");
                }
                feature.apiLayers.SetEnabled(LayerName, Architecture.X64, true);
            }
            feature.enabled = true;
            KeepInMemoryOnly(feature);
        }

        private static void RestoreAfterInterruptedSession()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Restore();
            RepairSavedSettings();
        }

        private static void Restore()
        {
            if (EditorPrefs.GetBool(Prefix + "PendingRestore"))
            {
                ApiLayersFeature feature = DesktopFeature();
                JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(Prefix + "OriginalFeature"), feature);
                KeepInMemoryOnly(feature);
                EditorPrefs.SetBool(Prefix + "PendingRestore", false);
            }
            // Switch Meta's toggle back off only if Play in Simulator switched it on.
            if (SessionState.GetBool(ActivatedMetaSimKey, false))
            {
                SessionState.EraseBool(ActivatedMetaSimKey);
                EditorApplication.ExecuteMenuItem(MetaSimDeactivateMenu);
            }
        }

        private static ApiLayersFeature DesktopFeature()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            var feature = settings == null ? null : settings.GetFeature<ApiLayersFeature>();
            if (feature == null) throw new InvalidOperationException("The Windows OpenXR API Layers feature is unavailable.");
            return feature;
        }

        // OpenXR reads the in-memory feature when Play starts, so the Simulator layer never needs
        // to reach disk. Leaving the object clean means neither a save nor a crash can write it.
        private static void KeepInMemoryOnly(ApiLayersFeature feature)
        {
            if (!SessionState.GetBool(FeatureWasDirtyKey, false))
                EditorUtility.ClearDirty(feature);
        }

        // Removes the Operator layer if the tracked settings file ever contains it, for example
        // after an older version of this helper was interrupted during Play.
        private static void RepairSavedSettings()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            var feature = settings == null ? null : settings.GetFeature<ApiLayersFeature>();
            string path = feature == null ? null : AssetDatabase.GetAssetPath(feature);
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || !File.ReadAllText(path).Contains(LayerName))
                return;
            RemoveOperatorLayer(feature.apiLayers);
            if (feature.apiLayers.collection.Count == 0)
                feature.enabled = false;
            SaveFeature(feature);
            Debug.LogWarning("Removed the Desktop XR Operator layer from saved OpenXR settings: " + path);
        }

        // ApiLayers.TryRemove also deletes the imported layer files, which later sessions reuse.
        private static void RemoveOperatorLayer(ApiLayers apiLayers)
        {
            FieldInfo collectionField = typeof(ApiLayers).GetField("m_Collection", BindingFlags.Instance | BindingFlags.NonPublic);
            IList collection = collectionField == null ? null : collectionField.GetValue(apiLayers) as IList;
            if (collection == null)
                throw new InvalidOperationException("Unity's OpenXR API layer list is unavailable.");
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (((ApiLayers.ApiLayer)collection[i]).name == LayerName)
                    collection.RemoveAt(i);
            }
        }

        private static void SaveFeature(ApiLayersFeature feature)
        {
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssetIfDirty(feature);
        }

        private static bool RegisterExistingImportedLayer(ApiLayers apiLayers, string manifestPath)
        {
            LayerManifest manifest = JsonUtility.FromJson<LayerManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.api_layer == null || manifest.api_layer.name != LayerName)
                return false;

            const BindingFlags instanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;
            Type jsonType = typeof(ApiLayers).GetNestedType("ApiLayerJson", BindingFlags.NonPublic);
            FieldInfo collectionField = typeof(ApiLayers).GetField("m_Collection", instanceMembers);
            if (jsonType == null || collectionField == null)
                return false;

            object json = Activator.CreateInstance(jsonType);
            jsonType.GetField("name").SetValue(json, manifest.api_layer.name);
            jsonType.GetField("library_path").SetValue(json, manifest.api_layer.library_path);
            jsonType.GetField("api_version").SetValue(json, manifest.api_layer.api_version);
            jsonType.GetField("implementation_version").SetValue(json, manifest.api_layer.implementation_version);
            jsonType.GetField("description").SetValue(json, manifest.api_layer.description);

            ConstructorInfo constructor = typeof(ApiLayers.ApiLayer).GetConstructor(
                instanceMembers,
                null,
                new[] { jsonType, typeof(string), typeof(Architecture), typeof(bool) },
                null);
            IList collection = collectionField.GetValue(apiLayers) as IList;
            if (constructor == null || collection == null)
                return false;

            collection.Add(constructor.Invoke(new object[]
            {
                json,
                Path.GetFileName(manifestPath),
                Architecture.X64,
                true
            }));
            return true;
        }

        private static void RequireFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException("Required desktop XR tool file is missing.", path);
        }
    }
}
#endif
