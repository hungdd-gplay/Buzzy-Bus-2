using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaseProject.EditorTools.ImageLayoutScanner
{
    /// <summary>
    /// Cấu hình tạo Popup từ kết quả quét ảnh demo (Model). Lưu EditorPrefs dạng JSON.
    /// </summary>
    [Serializable]
    public class PopupBuildSetting
    {
        [Tooltip("Tên class / prefab. Nên kết thúc bằng 'Box', ví dụ: DailyRewardBox.")]
        public string namePopup = "NewPopupBox";
        [Tooltip("Thư mục chứa script sinh ra.")]
        public string folderScript = ProjectPaths.BASE_DEPLOY + "/View/Popups/Generated";
        [Tooltip("Thư mục chứa prefab. BẮT BUỘC nằm trong 1 thư mục Resources để BoxFactory load được.")]
        public string folderPrefab = "Assets/Resources/UI/Popups/Generated";

        [Header("Canvas")]
        [Tooltip("CanvasScaler.matchWidthOrHeight (project đang dùng 1 = theo chiều cao).")]
        public float matchWidthOrHeight = 1f;

        [Header("Nền tối")]
        public bool isAddDim = true;
        public Color colorDim = new Color(0f, 0f, 0f, 0.75f);
        public bool isCloseOnClickDim;

        [Header("Nút bấm (nhận diện theo tên ảnh thành phần)")]
        public bool isDetectButton = true;
        [Tooltip("Tên ảnh chứa 1 trong các từ này (tách theo _ - khoảng trắng, chữ hoa) => tạo Button.")]
        public string keywordsButton = "btn,button,bt";
        [Tooltip("Nút có từ này => gắn sẵn hàm Close().")]
        public string keywordsClose = "close,exit,x";
        public bool isAddClickSound = true;

        [Header("BaseBox")]
        public BoxAnimType animType = BoxAnimType.Scale;
        public bool isPlaySoundOpenClose = true;
        public bool isCloseByBackKey = true;

        [Header("Script")]
        public bool isGenerateScript = true;
        public bool isOpenPrefabAfterBuild = true;
    }

    /// <summary>1 nút được nhận diện trong popup.</summary>
    [Serializable]
    public class PopupButtonData
    {
        public string fieldName;      // _btnPlay
        public string pathInPrefab;   // MainPanel/bg/btn_play
        public string nameMethod;     // _OnClickPlay (rỗng nếu là nút đóng)
        public bool isClose;
    }

    /// <summary>
    /// Thông tin cần gắn script vào prefab sau khi Unity biên dịch xong script mới.
    /// Lưu ở SessionState để sống qua domain reload.
    /// </summary>
    [Serializable]
    public class PopupPendingWire
    {
        public string pathPrefab;
        public string nameClass;
        public BoxAnimType animType;
        public bool isPlaySoundOpenClose;
        public bool isCloseOnClickDim;
        public bool isCloseByBackKey;
        public bool isOpenPrefabAfterBuild;
        public List<PopupButtonData> buttons = new List<PopupButtonData>();
    }
}
