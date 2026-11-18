using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phát âm thanh song song, KHÔNG ngắt quãng nhau.
/// Giữ 1 pool AudioSource con (mặc định 3). Khi tất cả đang bận thì tạo thêm (tới countSourceMax),
/// vượt giới hạn thì dùng lại nguồn đã phát lâu nhất.
/// Dùng chung cho "Sound" (hiệu ứng game) và "SoundUI" (âm thanh giao diện).
/// </summary>
public class SoundPlayer : MonoBehaviour
{
    [Tooltip("Số AudioSource con tạo sẵn.")]
    [Min(1)] public int countSourceInit = 3;
    [Tooltip("Số AudioSource tối đa được phép tạo thêm khi các nguồn đều bận.")]
    [Min(1)] public int countSourceMax = 8;
    [Range(0f, 1f)] public float volumeMax = 1f;
    [Tooltip("Ngẫu nhiên cao độ nhẹ cho đỡ nhàm (0 = tắt).")]
    [Range(0f, 0.3f)] public float rangePitchRandom = 0f;

    private readonly List<AudioSource> _lstAudioSources = new List<AudioSource>();
    private readonly Dictionary<AudioClip, float> _dictLastTimePlay = new Dictionary<AudioClip, float>();
    private bool _isSoundOn = true;

    public void Init(bool isSoundOn)
    {
        _lstAudioSources.Clear();
        GetComponentsInChildren(true, _lstAudioSources);
        while (_lstAudioSources.Count < countSourceInit)
            _CreateSource();
        SetSoundOn(isSoundOn);
    }

    public void SetSoundOn(bool isOn)
    {
        _isSoundOn = isOn;
        if (!isOn)
        {
            foreach (var _source in _lstAudioSources)
                if (_source != null) _source.Stop();
        }
    }

    /// <summary>Phát clip trên nguồn đang rảnh. minInterval > 0 sẽ bỏ qua nếu cùng clip vừa phát.</summary>
    public AudioSource PlaySound(AudioClip clip, float volume = 1f, float minInterval = 0f)
    {
        if (!_isSoundOn || clip == null)
            return null;

        if (minInterval > 0f)
        {
            if (_dictLastTimePlay.TryGetValue(clip, out float _lastTime) && Time.unscaledTime - _lastTime < minInterval)
                return null;
            _dictLastTimePlay[clip] = Time.unscaledTime;
        }

        var _source = _GetFreeSource();
        _source.clip = clip;
        _source.volume = Mathf.Clamp01(volume) * volumeMax;
        _source.pitch = rangePitchRandom > 0f ? Random.Range(1f - rangePitchRandom, 1f + rangePitchRandom) : 1f;
        _source.Play();
        return _source;
    }

    public void StopAll()
    {
        foreach (var _source in _lstAudioSources)
            if (_source != null) _source.Stop();
    }

    private AudioSource _GetFreeSource()
    {
        AudioSource _sourceOldest = null;
        float _timePlayedMax = -1f;
        for (int i = 0; i < _lstAudioSources.Count; i++)
        {
            var _source = _lstAudioSources[i];
            if (_source == null)
                continue;
            if (!_source.isPlaying)
                return _source;
            if (_source.time > _timePlayedMax)
            {
                _timePlayedMax = _source.time;
                _sourceOldest = _source;
            }
        }

        if (_lstAudioSources.Count < countSourceMax || _sourceOldest == null)
            return _CreateSource();

        return _sourceOldest;
    }

    private AudioSource _CreateSource()
    {
        var _goSource = new GameObject("Source_" + _lstAudioSources.Count);
        _goSource.transform.SetParent(transform, false);
        var _source = _goSource.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _lstAudioSources.Add(_source);
        return _source;
    }
}
