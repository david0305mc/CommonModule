using System.Collections;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DavidTiles3D
{
    public class DavidTiles3D_Settings : ScriptableObject
    {
        private static DavidTiles3D_Settings _settings;

        private const string DefaultSettingsAssetName = "DavidTiles3D_Settings.asset";
        private const string DefaultFolderPath = "Assets/Settings";
        private const string DefaultAssetPath = DefaultFolderPath + "/" + DefaultSettingsAssetName;

        [FormerlySerializedAs("UseUndoAPI")]
        [SerializeField]
        private bool _useUndoAPI = false; // recommend off (Unity Undo API is slow)
        public bool UseUndoAPI => _useUndoAPI;

        [FormerlySerializedAs("SuppressTileAmountWarning")]
        [SerializeField]
        private bool _suppressTileAmountWarning = false;
        public bool SuppressTileAmountWarning => _suppressTileAmountWarning;

        public void SetUndoAPI(bool value)
        {
            if (_useUndoAPI == value)
                return;

            _useUndoAPI = value;
            MarkDirty();
        }

        public void SetSuppressTileAmountWarning(bool value)
        {
            if (_suppressTileAmountWarning == value)
                return;

            _suppressTileAmountWarning = value;
            MarkDirty();
        }

        // for editor use
        public static bool IsLocked;

        private void MarkDirty()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
#endif
        }

#if UNITY_EDITOR

        public static DavidTiles3D_Settings EditorInstance
        {
            get
            {
                if (_settings == null)
                {
                    _settings = LoadOrCreateSettings();
                }

                return _settings;
            }
        }

        private static DavidTiles3D_Settings LoadOrCreateSettings()
        {
            // 1. fixed path first
            var settings = AssetDatabase.LoadAssetAtPath<DavidTiles3D_Settings>(DefaultAssetPath);
            if (settings != null)
                return settings;

            // 2. fallback: project-wide search for legacy support
            string[] guids = AssetDatabase.FindAssets("t:DavidTiles3D_Settings");

            var paths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();

            if (paths.Count > 0)
            {
                if (paths.Count > 1)
                {
                    Debug.LogWarning($"DavidTiles3D: Multiple settings assets found. Using '{paths[0]}'. Consider cleaning up duplicates.");
                }

                settings = AssetDatabase.LoadAssetAtPath<DavidTiles3D_Settings>(paths[0]);

                if (settings != null)
                {
                    Debug.LogWarning($"DavidTiles3D: Settings asset found at legacy path '{paths[0]}'. Move it to '{DefaultAssetPath}' for consistency.");
                    return settings;
                }
            }

            // 3. create if missing
            return CreateSettingsAsset();
        }

        private static DavidTiles3D_Settings CreateSettingsAsset()
        {
            // create the folder if missing
            if (!AssetDatabase.IsValidFolder(DefaultFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "DavidTiles3D");
            }

            var settings = ScriptableObject.CreateInstance<DavidTiles3D_Settings>();

            AssetDatabase.CreateAsset(settings, DefaultAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"DavidTiles3D: Created settings asset at '{DefaultAssetPath}'");

            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(EditorInstance);
        }

#endif
    }
}
