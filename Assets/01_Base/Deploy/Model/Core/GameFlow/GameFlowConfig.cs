using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Bật / tắt 1 page ở màn Home.</summary>
[Serializable]
public class HomePageSetting
{
    public PageName page;
    public bool isEnable = true;
}

/// <summary>
/// Cấu hình luồng màn hình của từng game (Model): có màn Home không, Loading vào thẳng GamePlay tới level nào,
/// page nào được bật ở Home. Sửa trong Tools > Base Project > Game Flow (Home / Page).
/// File: Resources/Config/GameFlowConfig.asset
/// </summary>
[CreateAssetMenu(menuName = "Base Project/Game Flow Config", fileName = "GameFlowConfig")]
public class GameFlowConfig : ScriptableObject
{
    public const string PATH_RESOURCE = "Config/GameFlowConfig";

    [Header("Màn Home")]
    [Tooltip("Tắt = game không có Home: Loading vào thẳng GamePlay, mọi lệnh về Home sẽ load lại GamePlay.")]
    public bool isUseHome = true;
    [Tooltip("Level <= giá trị này: Loading vào thẳng GamePlay (remote config 'start_level_back_to_home' ghi đè).")]
    public int levelPlayDirectFromLoading = 5;

    [Header("Page ở Home")]
    public PageName defaultPage = PageName.Home;
    [Tooltip("Page không có trong danh sách được coi là bật.")]
    public List<HomePageSetting> pages = new List<HomePageSetting>();

    private static GameFlowConfig _instance;

    public static GameFlowConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<GameFlowConfig>(PATH_RESOURCE);
                if (_instance == null)
                    _instance = CreateInstance<GameFlowConfig>(); // Chưa tạo asset: dùng mặc định (có Home, mọi page bật)
            }
            return _instance;
        }
    }

    public bool IsPageEnable(PageName page)
    {
        if (pages == null)
            return true;
        for (int i = 0; i < pages.Count; i++)
            if (pages[i] != null && pages[i].page == page)
                return pages[i].isEnable;
        return true;
    }

    public void SetPageEnable(PageName page, bool isEnable)
    {
        if (pages == null)
            pages = new List<HomePageSetting>();
        var _setting = pages.Find(p => p != null && p.page == page);
        if (_setting == null)
            pages.Add(new HomePageSetting { page = page, isEnable = isEnable });
        else
            _setting.isEnable = isEnable;
    }
}
