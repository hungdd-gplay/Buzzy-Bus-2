using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Đường dẫn chuẩn của cấu trúc thư mục dự án (Tools > Base Project > Project Structure):
///   01_Base/Res + 01_Base/Deploy (Model / View / Controller / Editor)
///   02_Gameplay/Res + 02_Gameplay/Deploy
///   03_ThirdParty, 04_Environment, Resources, StreamingAssets
/// Tool editor nên tìm asset bằng FindAsset (theo kiểu) để không phụ thuộc vị trí, đường dẫn ở đây chỉ là nơi tạo mới mặc định.
/// </summary>
public static class ProjectPaths
{
    public const string BASE = "Assets/01_Base";
    public const string BASE_RES = BASE + "/Res";
    public const string BASE_DEPLOY = BASE + "/Deploy";

    public const string GAMEPLAY = "Assets/02_Gameplay";
    public const string GAMEPLAY_RES = GAMEPLAY + "/Res";
    public const string GAMEPLAY_DEPLOY = GAMEPLAY + "/Deploy";

    public const string THIRD_PARTY = "Assets/03_ThirdParty";
    public const string ENVIRONMENT = "Assets/04_Environment";
    public const string RESOURCES = "Assets/Resources";
    public const string STREAMING_ASSETS = "Assets/StreamingAssets";
    public const string TO_DELETE = "Assets/_ToDelete";

    public const string FOLDER_MODEL = "Model";
    public const string FOLDER_VIEW = "View";
    public const string FOLDER_CONTROLLER = "Controller";
    public const string FOLDER_EDITOR = "Editor";

    public const string SETTINGS = BASE_RES + "/Data/Settings";
    public const string SETTINGS_EDITOR = SETTINGS + "/Editor";
    public const string KEYSTORE = ENVIRONMENT + "/KeyStore";
    public const string GAME_CONTROLLER_PREFAB = BASE_RES + "/Prefabs/GameController.prefab";

    /// <summary>Tìm asset: đúng đường dẫn mặc định, nếu không có thì tìm theo kiểu trong toàn dự án.</summary>
    public static T FindAsset<T>(string pathDefault = null) where T : Object
    {
        if (!string.IsNullOrEmpty(pathDefault))
        {
            var _asset = AssetDatabase.LoadAssetAtPath<T>(pathDefault);
            if (_asset != null)
                return _asset;
        }
        var _guid = AssetDatabase.FindAssets("t:" + typeof(T).Name).FirstOrDefault();
        return string.IsNullOrEmpty(_guid) ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(_guid));
    }

    /// <summary>Tìm ScriptableObject theo kiểu, chưa có thì tạo ở pathDefault.</summary>
    public static T FindOrCreateAsset<T>(string pathDefault, System.Action<T> onCreate = null) where T : ScriptableObject
    {
        var _asset = FindAsset<T>(pathDefault);
        if (_asset != null)
            return _asset;
        EnsureFolder(Path.GetDirectoryName(pathDefault)?.Replace('\\', '/'));
        _asset = ScriptableObject.CreateInstance<T>();
        onCreate?.Invoke(_asset);
        AssetDatabase.CreateAsset(_asset, pathDefault);
        AssetDatabase.SaveAssets();
        return _asset;
    }

    /// <summary>Tìm prefab có component T (ví dụ GameController prefab có SegmentController).</summary>
    public static GameObject FindPrefabWithComponent<T>(string pathDefault = null) where T : Component
    {
        if (!string.IsNullOrEmpty(pathDefault))
        {
            var _go = AssetDatabase.LoadAssetAtPath<GameObject>(pathDefault);
            if (_go != null && _go.GetComponentInChildren<T>(true) != null)
                return _go;
        }
        foreach (var _guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var _go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(_guid));
            if (_go != null && _go.GetComponentInChildren<T>(true) != null)
                return _go;
        }
        return null;
    }

    public static void EnsureFolder(string pathFolder)
    {
        if (string.IsNullOrEmpty(pathFolder) || AssetDatabase.IsValidFolder(pathFolder))
            return;
        string _parent = Path.GetDirectoryName(pathFolder)?.Replace('\\', '/');
        EnsureFolder(_parent);
        AssetDatabase.CreateFolder(_parent, Path.GetFileName(pathFolder));
    }
}
