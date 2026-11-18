using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thư viện âm thanh: map SoundId -> AudioClip (+ âm lượng, khoảng cách tối thiểu giữa 2 lần phát).
/// Tạo: Create > Base Project > Sound Database.
/// </summary>
[CreateAssetMenu(fileName = "SoundDatabase", menuName = "Base Project/Sound Database")]
public class SoundDatabase : ScriptableObject
{
    [Serializable]
    public class SoundEntry
    {
        public SoundId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Khoảng thời gian tối thiểu (giây) giữa 2 lần phát cùng id, tránh dồn tiếng.")]
        public float minInterval = 0f;
    }

    public List<SoundEntry> lstSoundEntries = new List<SoundEntry>();

    private Dictionary<SoundId, SoundEntry> _dictSoundEntries;

    public SoundEntry GetEntry(SoundId id)
    {
        if (_dictSoundEntries == null)
            _BuildDictionary();
        _dictSoundEntries.TryGetValue(id, out var _entry);
        return _entry;
    }

    public AudioClip GetClip(SoundId id)
    {
        var _entry = GetEntry(id);
        return _entry != null ? _entry.clip : null;
    }

    /// <summary>Thêm/cập nhật 1 entry (dùng cho tool migrate).</summary>
    public void SetClip(SoundId id, AudioClip clip)
    {
        if (clip == null)
            return;
        var _entry = lstSoundEntries.Find(x => x.id == id);
        if (_entry == null)
        {
            _entry = new SoundEntry { id = id };
            lstSoundEntries.Add(_entry);
        }
        _entry.clip = clip;
        _dictSoundEntries = null;
    }

    private void _BuildDictionary()
    {
        _dictSoundEntries = new Dictionary<SoundId, SoundEntry>();
        foreach (var _entry in lstSoundEntries)
        {
            if (_entry != null && !_dictSoundEntries.ContainsKey(_entry.id))
                _dictSoundEntries.Add(_entry.id, _entry);
        }
    }

    private void OnValidate()
    {
        _dictSoundEntries = null;
    }
}
