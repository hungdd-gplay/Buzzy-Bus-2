using UnityEngine;

/// <summary>Phát 1 âm thanh mỗi khi object được bật (popup mở, hiệu ứng xuất hiện...).</summary>
public class PlaySoundOnEnable : MonoBehaviour
{
    public SoundId soundId = SoundId.None;
    public AudioClip clipOverride;
    public bool isSoundUI = false;

    private void OnEnable()
    {
        if (clipOverride != null)
        {
            if (isSoundUI) SoundManager.PlayUI(clipOverride);
            else SoundManager.PlaySound(clipOverride);
            return;
        }
        if (soundId == SoundId.None)
            return;
        if (isSoundUI) SoundManager.PlayUI(soundId);
        else SoundManager.PlaySound(soundId);
    }
}
