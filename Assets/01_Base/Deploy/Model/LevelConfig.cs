using System;

/// <summary>
/// Model dữ liệu level (MVC - Model) đọc từ Resources/Levels/Level_{n}.
/// Bản rút gọn sau khi gỡ gameplay Bird Sort: chỉ giữ các trường core đang dùng
/// (Home, Challenge). Gameplay mới có thể kế thừa hoặc bổ sung trường riêng.
/// </summary>
[Serializable]
public class LevelConfig
{
    public int id;
    public int idHardMode;
    public int min;
    public int second;

    public int TotalTime
    {
        get { return (min * 60) + second; }
    }
}
