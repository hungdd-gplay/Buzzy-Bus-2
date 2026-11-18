using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn vào Button/Toggle để tự phát tiếng khi bấm (qua SoundManager.SoundUI).
/// Thêm nhanh: chuột phải Button > "Add Click Sound", hoặc GameObject > Base Project > UI > Button.
/// </summary>
[DisallowMultipleComponent]
public class UIButtonSound : MonoBehaviour
{
    public SoundId soundId = SoundId.UIButtonClick;
    [Tooltip("Nếu gán, ưu tiên phát clip này thay cho soundId.")]
    public AudioClip clipOverride;

    private void Awake()
    {
        var _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(PlaySound);

        var _toggle = GetComponent<Toggle>();
        if (_toggle != null)
            _toggle.onValueChanged.AddListener(_ => PlaySound());
    }

    public void PlaySound()
    {
        if (clipOverride != null)
            SoundManager.PlayUI(clipOverride);
        else
            SoundManager.PlayUI(soundId);
    }
}
