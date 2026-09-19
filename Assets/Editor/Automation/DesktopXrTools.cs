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
        private static string Prefix => "VRBasketball.DesktopXR." + Hash128.Compute(Application.dataPath) + ".";

        static DesktopXrTools()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += RestoreAfterInterruptedSession;
        }

        [MenuItem("VR Basketball/Desktop XR/Configure Experimental Operator")]
        public static void ConfigureFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before configuring desktop XR tools.");
            string manifest = EditorUtility.OpenFilePanel("Select the Windows Operator layer JSON", "", "json");
            if (string.IsNullOrEmpty(manifest)) return;
            string runtime = EditorUtility.OpenFilePanel("Select meta_openxr_simulator.json", "", "json");
            if (string.IsNullOrEmpty(runtime)) return;
            ConfigureFromPaths(manifest, runtime);
        }

        public static void ConfigureFromPaths(string operatorManifest, string simulatorManifest)
        {
            AutomationReports.ValidateUnityVersion();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before configuring desktop XR tools.");
            RequireFile(operatorManifest);
            RequireFile(simulatorManifest);
            RequireFile(Path.Combine(Path.GetDirectoryName(operatorManifest), "XrApiLayer_METAX_operator.dll"));
            RequireFile(Path.Combine(Path.GetDirectoryName(simulatorManifest), "SIMULATOR.dll"));
            EditorPrefs.SetString(Prefix + "OperatorManifest", Path.GetFullPath(operatorManifest));
            EditorPrefs.SetString(Prefix + "SimulatorManifest", Path.GetFullPath(simulatorManifest));
            EditorPrefs.SetBool(Prefix + "Enabled", true);
            Debug.Log("Experimental desktop XR preview configured locally. Enter Play mode to use Operator and Simulator.");
        }

        [MenuItem("VR Basketball/Desktop XR/Disable Experimental Preview")]
        public static void Disable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before disabling desktop XR preview.");
            Restore();
            EditorPrefs.SetBool(Prefix + "Enabled", false);
            Debug.Log("Experimental desktop XR preview disabled; normal XR runtime selection is restored.");
        }

        [MenuItem("VR Basketball/Desktop XR/Show Local Status")]
        public static void ShowStatus()
        {
            Debug.Log("Experimental desktop XR preview: " + (EditorPrefs.GetBool(Prefix + "Enabled") ? "enabled" : "disabled")
                + "; configured Operator files: " + File.Exists(EditorPrefs.GetString(Prefix + "OperatorManifest"))
                + "; configured Simulator files: " + File.Exists(EditorPrefs.GetString(Prefix + "SimulatorManifest")));
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (Application.isBatchMode) return;
            if (state == PlayModeStateChange.ExitingEditMode && EditorPrefs.GetBool(Prefix + "Enabled"))
            {
                try { Prepare(); }
                catch (Exception exception)
                {
                    Restore();
                    EditorApplication.isPlaying = false;
                    Debug.LogError("Desktop XR preview could not start: " + exception.Message);
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode) Restore();
        }

        private static void Prepare()
        {
            AutomationReports.ValidateUnityVersion();
            string manifest = EditorPrefs.GetString(Prefix + "OperatorManifest");
            string runtime = EditorPrefs.GetString(Prefix + "SimulatorManifest");
            RequireFile(manifest);
            RequireFile(runtime);
            ApiLayersFeature feature = DesktopFeature();
            EditorPrefs.SetString(Prefix + "OriginalFeature", JsonUtility.ToJson(feature));
            string previousRuntime = Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");
            EditorPrefs.SetString(Prefix + "PreviousRuntime", previousRuntime ?? "");
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
            SaveFeature(feature);
            // Unity manages API-layer variables itself. Only select this process's runtime.
            Environment.SetEnvironmentVariable("XR_RUNTIME_JSON", runtime);
        }

        private static void RestoreAfterInterruptedSession()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore();
        }

        private static void Restore()
        {
            if (!EditorPrefs.GetBool(Prefix + "PendingRestore")) return;
            ApiLayersFeature feature = DesktopFeature();
            JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(Prefix + "OriginalFeature"), feature);
            SaveFeature(feature);
            string previousRuntime = EditorPrefs.GetString(Prefix + "PreviousRuntime");
            Environment.SetEnvironmentVariable("XR_RUNTIME_JSON", string.IsNullOrEmpty(previousRuntime) ? null : previousRuntime);
            EditorPrefs.SetBool(Prefix + "PendingRestore", false);
        }

        private static ApiLayersFeature DesktopFeature()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            var feature = settings == null ? null : settings.GetFeature<ApiLayersFeature>();
            if (feature == null) throw new InvalidOperationException("The Windows OpenXR API Layers feature is unavailable.");
            return feature;
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
