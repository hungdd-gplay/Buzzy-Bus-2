namespace BaseProject.Theme
{
    /// <summary>
    /// Sự kiện theme (tên + id). Id được dùng trong RemoteConfig (key theme_event_id).
    /// Thêm sự kiện mới: thêm 1 dòng với id KHÔNG trùng, không đổi id cũ.
    /// </summary>
    public enum ThemeEventId
    {
        Default = 0,
        Halloween = 1,
        Christmas = 2,
        LunarNewYear = 3,
        Valentine = 4,
        Summer = 5,
        BlackFriday = 6,
    }

    /// <summary>Nhóm lớn, cũng là tên thư mục con của mỗi theme.</summary>
    public enum ThemeCategory
    {
        Loading = 1,
        Home = 2,
        Gameplay = 3,
    }

    /// <summary>
    /// Các vị trí ảnh có thể đổi theo theme. Tên enum = tên file ảnh (không đuôi).
    /// Ảnh trang trí con dùng thêm hậu tố: Home_ButtonPlay_Decor_1, Home_ButtonPlay_Decor_2...
    /// Id theo dải: Loading 100-199, Home 200-299, Gameplay 300-399.
    /// </summary>
    public enum ThemeSlotId
    {
        None = 0,

        // 1 - Loading
        Loading_BG = 100,
        Loading_IconTitle = 101,
        Loading_Process = 102,
        Loading_ProcessBG = 103,

        // 2 - Home
        Home_BG = 200,
        Home_ButtonPlay = 201,
        Home_NavBar = 202,
        Home_ButtonSetting = 203,
        Home_CoinUI = 204,
        Home_HealthUI = 205,
        Home_NavButton = 206,

        // 3 - Gameplay (Gameplay_BG là danh sách: Gameplay_BG_01, Gameplay_BG_02...)
        Gameplay_BG = 300,
        Gameplay_ButtonReplay = 301,
        Gameplay_ButtonSetting = 302,
        Gameplay_ButtonItem = 303,
        Gameplay_ButtonPause = 304,
        Gameplay_ButtonBack = 305,
    }

    /// <summary>Cách chọn BG gameplay từ danh sách.</summary>
    public enum ThemeBackgroundMode
    {
        Random = 0,          // Ngẫu nhiên mỗi lần vào level
        ByLevelOrder = 1,    // Lần lượt theo level (level 1 -> BG 1, level 2 -> BG 2... rồi lặp lại)
        ByLevelRange = 2,    // Mỗi BG dùng cho N level liên tiếp (levelPerBackground)
    }
}
