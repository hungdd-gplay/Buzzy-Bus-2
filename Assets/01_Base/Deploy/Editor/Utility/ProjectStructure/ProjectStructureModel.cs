using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaseProject.EditorTools.ProjectStructure
{
    public enum MoveCategory
    {
        Base = 0,           // 01_Base (Res / Deploy)
        ThirdParty = 1,     // 03_ThirdParty
        Environment = 2,    // 04_Environment
        Resources = 3,      // Assets/Resources (gộp các thư mục Resources con)
        StreamingAssets = 4,
        Unused = 5,         // _ToDelete
        Conflict = 6        // Trùng đích, không chuyển
    }

    /// <summary>1 thao tác di chuyển trong kế hoạch.</summary>
    [Serializable]
    public class MoveEntry
    {
        public string pathFrom;
        public string pathTo;
        public MoveCategory category;
        public string reason;
        public bool isSelected = true;
        public bool isFolder;
    }

    [Serializable]
    public class MovePlan
    {
        public string timeCreate;
        public List<MoveEntry> entries = new List<MoveEntry>();
        public List<string> warnings = new List<string>();
    }

    /// <summary>
    /// Luật phân loại thư mục gốc (Model). Lưu EditorPrefs dạng JSON, sửa được trong cửa sổ Project Structure.
    /// </summary>
    [Serializable]
    public class ProjectStructureRules
    {
        [Tooltip("Giữ nguyên ở Assets/ (đường dẫn đặc biệt của Unity hoặc SDK nhạy đường dẫn).")]
        public List<string> keepAtRoot = new List<string>
        {
            "Plugins", "Resources", "StreamingAssets", "Editor Default Resources", "Gizmos",
            "ExternalDependencyManager", "GeneratedLocalRepo", "Firebase", "Parse", "PlayServicesResolver",
            "MaxSdk", "GoogleMobileAds", "FacebookSDK", "Adjust", "AppsFlyer", "Falcon", "GplayGoogleUMP",
            "PlayFabSDK", "PlayFabEditorExtensions", "TextMesh Pro", "Notification",
            "FR2_Cache.asset",
            "01_Base", "02_Gameplay", "03_ThirdParty", "04_Environment", "_ToDelete"
        };

        [Tooltip("Thư mục gốc của bên thứ 3 an toàn để chuyển vào 03_ThirdParty.")]
        public List<string> thirdParty = new List<string>
        {
            "Asset refs in scenes", "Best HTTP (Pro)", "Camera Fit", "com.unity.uiextensions", "Confetti FX", "Demigiant", "EnhancedScroller v2",
            "Epic Toon FX", "EpicVictoryEffects", "FindReference2", "Fx_Colletion", "Gamemaster Audio - Fun Casual Sounds",
            "GPlayStudio", "JsonDotNet", "Magnetic Scroll View", "OneP Packer", "OnQ Ultimate FX Pack 1 Cartoon",
            "ProCamera2D", "ReviewIAP", "Spine", "StompyRobot"
        };

        [Tooltip("Mẫu tên file môi trường (đuôi hoặc tên đầy đủ) -> 04_Environment.")]
        public List<string> environmentPatterns = new List<string>
        {
            "google-services", "GoogleService-Info", "agconnect-services", ".keystore", ".jks", ".p12", ".mobileprovision"
        };

        [Tooltip("File / thư mục gốc là rác -> _ToDelete.")]
        public List<string> junkAtRoot = new List<string> { "___www.unityassetcollection.com___.txt" };

        [Tooltip("Thư mục gốc chính của dự án base: không thêm tiền tố tên thư mục khi chuyển.")]
        public string mainBaseFolder = "01_BaseCode";

        [Tooltip("Quét asset không dùng tới và chuyển vào _ToDelete.")]
        public bool isScanUnused = true;

        /// <summary>Đường dẫn (tiền tố) phải giữ nguyên vì SDK/Unity sinh file vào đó cố định (vd Unity IAP Obfuscator).</summary>
        public List<string> keepPaths = new List<string> { "Assets/Scripts/UnityPurchasing" };
    }
}
