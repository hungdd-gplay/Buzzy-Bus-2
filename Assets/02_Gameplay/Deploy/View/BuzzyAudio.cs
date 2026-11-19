using UnityEngine;

namespace Buzzy.Game
{
    /// <summary>
    /// M7 (góp ý 03): phát âm thanh cho view: pool AudioSource cho hiệu ứng (phát chồng, có cao độ riêng) + 1 nguồn nhạc nền lặp.
    /// Gắn lên camera của BuzzyBootstrap nên nhạc không đứt khi đổi level. Bật / tắt theo cài đặt (MetaState.Sound / Music) qua SoundOn / MusicOn.
    /// Bộ chụp edit-mode không gắn → view im lặng.
    /// </summary>
    public class BuzzyAudio : MonoBehaviour
    {
        [Tooltip("Số nguồn hiệu ứng phát chồng")] public int voices = 8;
        public bool SoundOn = true;

        AudioSource[] _src; AudioSource _music; int _next; bool _musicOn = true;

        void Awake()
        {
            _src = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < _src.Length; i++) { _src[i] = gameObject.AddComponent<AudioSource>(); _src[i].playOnAwake = false; _src[i].spatialBlend = 0f; }
            _music = gameObject.AddComponent<AudioSource>(); _music.playOnAwake = false; _music.loop = true; _music.spatialBlend = 0f;
        }

        /// <summary>Phát 1 hiệu ứng; pitch 1 = gốc.</summary>
        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (!SoundOn || clip == null || _src == null) return;
            var s = _src[_next]; _next = (_next + 1) % _src.Length;
            s.pitch = pitch; s.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>Nhạc nền lặp; cùng clip đang phát thì chỉ chỉnh âm lượng (không phát lại từ đầu khi sang level).</summary>
        public void PlayMusic(AudioClip clip, float volume)
        {
            if (_music == null || clip == null) return;
            _music.volume = Mathf.Clamp01(volume);
            if (_music.clip == clip && _music.isPlaying) return;
            _music.clip = clip;
            if (_musicOn) _music.Play();
        }

        public bool MusicOn
        {
            get { return _musicOn; }
            set
            {
                _musicOn = value; if (_music == null) return;
                if (!value) _music.Pause(); else if (_music.clip != null && !_music.isPlaying) _music.Play();   // Play sau Pause: tiếp từ chỗ dừng
            }
        }
    }
}
