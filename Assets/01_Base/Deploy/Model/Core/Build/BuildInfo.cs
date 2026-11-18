using UnityEngine;

/// <summary>
/// Thông tin bản build, đọc được lúc runtime (Resources/Config/BuildInfo).
/// Tự ghi trước mỗi lần build (BuildPreprocess) từ Build Config + PlayerSettings, KHÔNG sửa tay.
/// </summary>
public class BuildInfo : ScriptableObject
{
    public const string PATH_RESOURCES = "Config/BuildInfo";

    public int versionCode;
    public string versionName;
    [Tooltip("Tiền tố product id IAP (thường = package Android).")]
    public string iapProductPrefix;
    public bool isTestBuild;
    public string buildTime;

    private static BuildInfo _instance;

    public static BuildInfo Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<BuildInfo>(PATH_RESOURCES);
                if (_instance == null)
                {
                    _instance = CreateInstance<BuildInfo>();
                    _instance.versionName = Application.version;
                    _instance.iapProductPrefix = Application.identifier;
                }
            }
            return _instance;
        }
    }

    public static int VersionCode => Instance.versionCode;
    public static string VersionName => string.IsNullOrEmpty(Instance.versionName) ? Application.version : Instance.versionName;
    public static string IapProductPrefix => string.IsNullOrEmpty(Instance.iapProductPrefix) ? Application.identifier : Instance.iapProductPrefix;
}
