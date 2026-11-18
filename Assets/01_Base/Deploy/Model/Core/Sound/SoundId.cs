/// <summary>
/// Định danh âm thanh dùng chung. Thêm id mới ở cuối, KHÔNG đổi giá trị số đã có (asset lưu theo số).
/// </summary>
public enum SoundId
{
    None = 0,

    // ===== Nhạc nền (100+) =====
    MusicHome = 100,
    MusicGameplay = 101,
    MusicWin = 102,
    MusicLose = 103,

    // ===== UI (200+) =====
    UIButtonClick = 200,
    UIPopupOpen = 201,
    UIPopupClose = 202,
    UIToggle = 203,
    UITapScreen = 204,

    // ===== Gameplay / hiệu ứng (300+) =====
    CoinCollect = 300,
    ItemCollect = 301,
    RewardClaim = 302,
    LevelStart = 303,
    CountDown = 304,
}
