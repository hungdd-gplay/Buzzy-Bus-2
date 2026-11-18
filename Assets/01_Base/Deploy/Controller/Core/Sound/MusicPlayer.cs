using System.Collections;
using UnityEngine;

/// <summary>
/// Phát nhạc nền (1 AudioSource, loop, chuyển bài có fade).
/// Nằm trên GameObject con "Music" của SoundManager.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSourceMusic;
    [Range(0f, 1f)] public float volumeMusicMax = 0.7f;
    public float durationFade = 0.4f;

    private bool _isMusicOn = true;
    private Coroutine _coroutineFade;

    public AudioClip CurrentClip => audioSourceMusic != null ? audioSourceMusic.clip : null;
    public bool IsPlaying => audioSourceMusic != null && audioSourceMusic.isPlaying;

    public void Init(bool isMusicOn)
    {
        if (audioSourceMusic == null)
            audioSourceMusic = GetComponent<AudioSource>();
        audioSourceMusic.loop = true;
        audioSourceMusic.playOnAwake = false;
        SetMusicOn(isMusicOn);
    }

    public void SetMusicOn(bool isOn)
    {
        _isMusicOn = isOn;
        if (audioSourceMusic != null)
            audioSourceMusic.volume = isOn ? volumeMusicMax : 0f;
    }

    public void PlayMusic(AudioClip clip, bool isRestartIfSame = false)
    {
        if (clip == null || audioSourceMusic == null)
            return;
        if (!isRestartIfSame && audioSourceMusic.clip == clip && audioSourceMusic.isPlaying)
            return;

        if (_coroutineFade != null)
            StopCoroutine(_coroutineFade);
        _coroutineFade = StartCoroutine(_FadeToClip(clip));
    }

    public void StopMusic()
    {
        if (_coroutineFade != null)
            StopCoroutine(_coroutineFade);
        if (audioSourceMusic != null)
            audioSourceMusic.Stop();
    }

    public void PauseMusic()
    {
        if (audioSourceMusic != null)
            audioSourceMusic.Pause();
    }

    public void ResumeMusic()
    {
        if (audioSourceMusic != null)
            audioSourceMusic.UnPause();
    }

    private IEnumerator _FadeToClip(AudioClip clip)
    {
        float _volumeTarget = _isMusicOn ? volumeMusicMax : 0f;
        if (audioSourceMusic.isPlaying && durationFade > 0f)
        {
            float _volumeStart = audioSourceMusic.volume;
            for (float t = 0; t < durationFade; t += Time.unscaledDeltaTime)
            {
                audioSourceMusic.volume = Mathf.Lerp(_volumeStart, 0f, t / durationFade);
                yield return null;
            }
        }

        audioSourceMusic.clip = clip;
        audioSourceMusic.Play();

        if (durationFade > 0f)
        {
            for (float t = 0; t < durationFade; t += Time.unscaledDeltaTime)
            {
                audioSourceMusic.volume = Mathf.Lerp(0f, _volumeTarget, t / durationFade);
                yield return null;
            }
        }
        audioSourceMusic.volume = _volumeTarget;
        _coroutineFade = null;
    }
}
