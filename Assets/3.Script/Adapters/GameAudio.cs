using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 배경음 크로스페이드·효과음 재생(K12)입니다. Stage의 BGM 키를 받아 Inspector 목록에서 찾습니다. 클립은 교체할 수 있습니다.
    public sealed class GameAudio : MonoBehaviour
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string _key = "";
            [SerializeField] private AudioClip _clip = null;
            public string Key => _key;
            public AudioClip Clip => _clip;
        }

        private const float _crossfadeSeconds = 1f;
        [SerializeField, InspectorName("소리 목록")] private Entry[] _clips = Array.Empty<Entry>();
        [SerializeField] private AudioSource _musicA = null;
        [SerializeField] private AudioSource _musicB = null;
        [SerializeField] private AudioSource _effects = null;
        private readonly Dictionary<string, AudioClip> _lookup = new Dictionary<string, AudioClip>();
        private string _currentMusic;
        private bool _usingA = true;
        private float _fade = 1f;
        private float _musicVolume = 0.8f;
        private float _pauseScale = 1f;

        private void Awake() { foreach (Entry entry in _clips) if (entry != null && entry.Clip != null) _lookup[entry.Key] = entry.Clip; }

        public void Apply(GameSettings settings)
        {
            _musicVolume = settings.MusicPercent / 100f;
            _effects.volume = settings.EffectsPercent / 100f;
            UpdateVolumes();
        }

        // 일시정지 중 배경음 50%(O04).
        public void SetPaused(bool paused) { _pauseScale = paused ? 0.5f : 1f; UpdateVolumes(); }

        public void PlayMusic(string key)
        {
            if (string.IsNullOrEmpty(key) || key == _currentMusic || !_lookup.TryGetValue(key, out AudioClip clip)) return;
            _currentMusic = key;
            _usingA = !_usingA;
            AudioSource next = _usingA ? _musicA : _musicB;
            next.clip = clip; next.loop = true; next.Play();
            _fade = 0f;
            UpdateVolumes();
        }

        public void PlayEffect(string key)
        {
            if (_lookup.TryGetValue(key, out AudioClip clip)) _effects.PlayOneShot(clip);
        }

        private void Update()
        {
            if (_fade >= 1f) return;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / _crossfadeSeconds);
            UpdateVolumes();
            if (_fade >= 1f) (_usingA ? _musicB : _musicA).Stop();
        }

        private void UpdateVolumes()
        {
            float volume = _musicVolume * _pauseScale;
            (_usingA ? _musicA : _musicB).volume = volume * _fade;
            (_usingA ? _musicB : _musicA).volume = volume * (1f - _fade);
        }
    }
}
