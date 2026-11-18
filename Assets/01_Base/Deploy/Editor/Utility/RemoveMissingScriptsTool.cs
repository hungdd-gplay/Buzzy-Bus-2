using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dọn các component "Missing Script" còn sót lại sau khi gỡ gameplay.
/// Menu: Tools/Base Project/...
/// </summary>
public static class RemoveMissingScriptsTool
{
    [MenuItem("Tools/Base Project/Remove Missing Scripts In Open Scene")]
    public static void RemoveMissingScriptsInOpenScene()
    {
        int _totalRemoved = 0;
        var _scene = EditorSceneManager.GetActiveScene();
        foreach (var _root in _scene.GetRootGameObjects())
        {
            _totalRemoved += _RemoveMissingRecursive(_root);
        }
        if (_totalRemoved > 0)
            EditorSceneManager.MarkSceneDirty(_scene);
        Debug.Log($"[RemoveMissingScripts] Scene '{_scene.name}': đã xoá {_totalRemoved} component missing.");
    }

    [MenuItem("Tools/Base Project/Remove Missing Scripts In All Prefabs")]
    public static void RemoveMissingScriptsInAllPrefabs()
    {
        int _totalRemoved = 0;
        int _prefabChanged = 0;
        string[] _guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (var _guid in _guids)
        {
            string _path = AssetDatabase.GUIDToAssetPath(_guid);
            var _root = PrefabUtility.LoadPrefabContents(_path);
            int _removed = _RemoveMissingRecursive(_root);
            if (_removed > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(_root, _path);
                _totalRemoved += _removed;
                _prefabChanged++;
            }
            PrefabUtility.UnloadPrefabContents(_root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[RemoveMissingScripts] Đã xoá {_totalRemoved} component missing trong {_prefabChanged} prefab.");
    }

    private static int _RemoveMissingRecursive(GameObject gameObjectTarget)
    {
        int _count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObjectTarget);
        foreach (Transform _child in gameObjectTarget.transform)
        {
            _count += _RemoveMissingRecursive(_child.gameObject);
        }
        return _count;
    }
}
