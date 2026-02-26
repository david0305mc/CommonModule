#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

public static class SceneChangeEditor
{
    private const string SceneFolderPath = "Assets/01_Scenes";

    private static string[] GetSortedScenes()
    {
        var guids = AssetDatabase.FindAssets("t:Scene", new[] { SceneFolderPath });

        return guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path) // 파일명 기준 정렬
            .ToArray();
    }

    [MenuItem("SceneMove/Open Scene 1 &1")]
    private static void OpenScene1()
    {
        OpenSceneByIndex(0);
    }

    [MenuItem("SceneMove/Open Scene 2 &2")]
    private static void OpenScene2()
    {
        OpenSceneByIndex(1);
    }

    [MenuItem("SceneMove/Open Scene 3 &3")]
    private static void OpenScene3()
    {
        OpenSceneByIndex(2);
    }

    private static void OpenSceneByIndex(int index)
    {
        var scenes = GetSortedScenes();

        if (index < 0 || index >= scenes.Length)
        {
            Debug.LogWarning("해당 인덱스에 씬이 없음");
            return;
        }

        EditorSceneManager.OpenScene(scenes[index]);
        Debug.Log($"Move Scene: {scenes[index]}");
    }
}
#endif