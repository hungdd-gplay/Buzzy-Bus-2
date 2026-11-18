using UnityEngine;

/// <summary>
/// Quản lý toàn bộ âm thanh. Cấu trúc GameObject:
///   SoundManager
///     ├─ Music   (MusicPlayer)  : nhạc nền
///     ├─ Sound   (SoundPlayer)  : âm thanh game, phát song song không ngắt nhau (pool 3+ AudioSource)
///     └─ SoundUI (SoundPlayer)  : click, chạm màn hình, mở/đóng popup...
/// Mọi hàm static đều an toàn khi tính năng Sound bị tắt (không làm gì).
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    public SoundDatabase soundDatabase;
    public MusicPlayer musicPlayer;
    public SoundPlayer soundPlayer;
    public SoundPlayer soundUIPlayer;

    private bool _isInited;

    public static bool IsReady => Instance != null && Instance._isInited;

    public void Init()
    {
        Instance = this;
        if (musicPlayer != null) musicPlayer.Init(_IsMusicOn());
        if (soundPlayer != null) soundPlayer.Init(_IsSoundOn());
        if (soundUIPlayer != null) soundUIPlayer.Init(_IsSoundOn());
        _isInited = true;
    }

    // ================== Setting ==================

    /// <summary>Gọi lại khi người chơi bật/tắt nhạc, âm thanh (UseProfile tự gọi).</summary>
    public static void ApplySetting()
    {
        if (!IsReady)
            return;
        var _instance = Instance;
        if (_instance.musicPlayer != null) _instance.musicPlayer.SetMusicOn(_instance._IsMusicOn());
        if (_instance.soundPlayer != null) _instance.soundPlayer.SetSoundOn(_instance._IsSoundOn());
        if (_instance.soundUIPlayer != null) _instance.soundUIPlayer.SetSoundOn(_instance._IsSoundOn());
    }

    // ================== Music ==================

    public static void PlayMusic(SoundId id)
    {
        if (!IsReady) return;
        PlayMusic(Instance._GetClip(id));
    }

    public static void PlayMusic(AudioClip clip)
    {
        if (!IsReady || Instance.musicPlayer == null) return;
        Instance.musicPlayer.PlayMusic(clip);
    }

    public static void StopMusic()
    {
        if (!IsReady || Instance.musicPlayer == null) return;
        Instance.musicPlayer.StopMusic();
    }

    public static void PauseMusic()
    {
        if (!IsReady || Instance.musicPlayer == null) return;
        Instance.musicPlayer.PauseMusic();
    }

    public static void ResumeMusic()
    {
        if (!IsReady || Instance.musicPlayer == null) return;
        Instance.musicPlayer.ResumeMusic();
    }

    public static void PlayMusicHome() => PlayMusic(SoundId.MusicHome);
    public static void PlayMusicGameplay() => PlayMusic(SoundId.MusicGameplay);

    // ================== Sound (game) ==================

    public static void PlaySound(SoundId id)
    {
        if (!IsReady || Instance.soundPlayer == null) return;
        var _entry = Instance._GetEntry(id);
        if (_entry != null)
            Instance.soundPlayer.PlaySound(_entry.clip, _entry.volume, _entry.minInterval);
    }

    public static void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (!IsReady || Instance.soundPlayer == null) return;
        Instance.soundPlayer.PlaySound(clip, volume);
    }

    public static void PlayWin() => PlaySound(SoundId.MusicWin);
    public static void PlayLose() => PlaySound(SoundId.MusicLose);

    // ================== Sound UI ==================

    public static void PlayUI(SoundId id)
    {
        if (!IsReady || Instance.soundUIPlayer == null) return;
        var _entry = Instance._GetEntry(id);
        if (_entry != null)
            Instance.soundUIPlayer.PlaySound(_entry.clip, _entry.volume, _entry.minInterval);
    }

    public static void PlayUI(AudioClip clip, float volume = 1f)
    {
        if (!IsReady || Instance.soundUIPlayer == null) return;
        Instance.soundUIPlayer.PlaySound(clip, volume);
    }

    public static void PlayClick() => PlayUI(SoundId.UIButtonClick);

    // ================== Private ==================

    private SoundDatabase.SoundEntry _GetEntry(SoundId id)
    {
        return soundDatabase != null ? soundDatabase.GetEntry(id) : null;
    }

    private AudioClip _GetClip(SoundId id)
    {
        return soundDatabase != null ? soundDatabase.GetClip(id) : null;
    }

    private bool _IsMusicOn()
    {
        return GameController.Instance == null || GameController.Instance.useProfile == null || GameController.Instance.useProfile.OnMusic;
    }

    private bool _IsSoundOn()
    {
        return GameController.Instance == null || GameController.Instance.useProfile == null || GameController.Instance.useProfile.OnSound;
    }
}
