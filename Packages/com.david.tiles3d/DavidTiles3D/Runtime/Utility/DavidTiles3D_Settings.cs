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

        [FormerlySerializedAs("UseUndoAPI")]
        [SerializeField]
        private bool _useUndoAPI = false; //recommend off, because Unitys Undo API is incredible slow and inefficient.
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
        //for editor use
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
                    var settings = FindExistingSettingsAsset();
                    if (settings == null)
                    {
                        settings = CreateSettingsAsset();
                    }
                    _settings = settings;
                }
                return _settings;
            }
        }

        private static DavidTiles3D_Settings FindExistingSettingsAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:DavidTiles3D_Settings");
            if (guids.Length == 0)
                return null;

            var assetPaths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .OrderBy(path => string.Equals(Path.GetFileName(path), DefaultSettingsAssetName, System.StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(path => path)
                .ToList();

            if (assetPaths.Count > 1)
            {
                Debug.LogWarning($"DavidTiles3D: Multiple settings assets found. Using '{assetPaths[0]}'.");
            }

            foreach (var assetPath in assetPaths)
            {
                var settings = AssetDatabase.LoadAssetAtPath<DavidTiles3D_Settings>(assetPath);
                if (settings != null)
                    return settings;
            }

            return null;
        }

        private static DavidTiles3D_Settings CreateSettingsAsset()
        {
            var settings = ScriptableObject.CreateInstance<DavidTiles3D_Settings>();
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/{DefaultSettingsAssetName}");
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(EditorInstance);
        }
#endif
    }



}
