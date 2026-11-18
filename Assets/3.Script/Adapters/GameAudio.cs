using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using HumanGhost.Data.Storage;
using HumanGhost.Data.Definitions;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 배경음 크로스페이드·효과음 재생(K12)입니다. Stage의 BGM 키를 받아 Inspector 목록에서 찾습니다. 클립은 교체할 수 있습니다.
    public class GameAudio : MonoBehaviour
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string _key = "";
            [SerializeField] private AudioClip _clip = null;
            [SerializeField, Min(0)] private int _soundId = 0;
            public int SoundId => _soundId;
            public string Key => _key;
            public AudioClip Clip => _clip;
        }

        private const int _crossfadeMilliseconds = 1000;
        private MillisecondClock _fadeClock;
        private MusicFade _fadeA, _fadeB;

        private struct MusicFade
        {
            // [정수화 미적용 이유] AudioSource.volume에 곱하는 0~1 페이드 이득입니다. 페이드 시간은 정수 ms로 진행하고 이득만 오디오 경계의 실수로 둡니다.
            private float _start, _target;
            private int _elapsed, _duration;
            // [정수화 미적용 이유] AudioSource.volume에 곱하는 0~1 페이드 이득입니다(오디오 출력 경계).
            public float Gain { get; private set; }
            public bool IsRunning => _elapsed < _duration;
            public bool IsFadingOut => IsRunning && _target == 0f;

            public void Begin(float target, int milliseconds)
            {
                _start = Gain;
                _target = target;
                _elapsed = 0;
                _duration = milliseconds;
            }

            public void Step(AudioSource source, int milliseconds)
            {
                if (!IsRunning) return;
                _elapsed += Math.Min(milliseconds, _duration - _elapsed);
                Gain = Mathf.Lerp(_start, _target, _elapsed / (float)_duration);
                if (!IsRunning && _target == 0f) source.Stop();
            }
        }

        private ref MusicFade CurrentFade => ref (_usingA ? ref _fadeA : ref _fadeB);
        public bool IsMusicFadingOut => _fadeA.IsFadingOut || _fadeB.IsFadingOut;
        [SerializeField, InspectorName("소리 목록")] private Entry[] _clips = Array.Empty<Entry>();
        [SerializeField] private AudioSource _musicA = null;
        [SerializeField] private AudioSource _musicB = null;
        [SerializeField] private AudioSource _effects = null;
        // 방 환경음은 Sound 표 ID로 TableSoundPlayer가 재생합니다(Category 1 환경음: 배경음 음량·일시정지 50%).
        private int _ambienceId, _ambienceVoice;
        // 맵 이동은 씬을 다시 읽어 GameAudio가 새로 생깁니다. 같은 음원 주소면 마지막 재생 위치(샘플)에서 이어 갑니다.
        private static string _carriedAddress;
        private static int _carriedSample;
        private static long _carriedAtMs;
        private readonly Dictionary<string, List<AudioClip>> _lookup = new Dictionary<string, List<AudioClip>>();
        private readonly System.Random _effectRandom = new System.Random();
        private string _currentMusic;
        private int _tableMusicId;
        private IReadOnlyDataCatalog _catalog;
        private readonly Dictionary<string, List<int>> _cueIds = new Dictionary<string, List<int>>();

        // 프리팹이 소유한 클립은 해제하지 않습니다. 미연결 주소는 기존 주소 공급자를 사용합니다.
        private sealed class PrefabAudioSource : IAssetSource<AudioClip>
        {
            private readonly Dictionary<string, AudioClip> _clips;
            private readonly AddressableAssetSource<AudioClip> _fallback = new AddressableAssetSource<AudioClip>();
            public PrefabAudioSource(Dictionary<string, AudioClip> clips) => _clips = clips;
            public Task<AssetLease<AudioClip>> LoadAsync(string address, CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                return _clips.TryGetValue(address, out AudioClip clip)
                    ? Task.FromResult(new AssetLease<AudioClip>(clip, () => { }))
                    : _fallback.LoadAsync(address, cancellation);
            }
        }
        private bool _usingA = true;
        private int _fadeInMilliseconds = _crossfadeMilliseconds;
        // [정수화 미적용 이유] AudioSource.volume에 넣는 음량 배율입니다. 설정은 정수 퍼센트로 저장하고 출력 직전에만 0~1로 바꿉니다.
        private float _musicVolume = 0.8f;
        private float _pauseScale = 1f;
        private GameSettings _settings = new GameSettings();
        private TableSoundPlayer _tablePlayer;
        private readonly HashSet<object> _cutsceneOwners = new HashSet<object>();
        public void SetCutsceneOwner(object owner, bool muted)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (muted) _cutsceneOwners.Add(owner); else _cutsceneOwners.Remove(owner);
            bool blocked = _cutsceneOwners.Count > 0;
            _musicA.mute = _musicB.mute = _effects.mute = blocked;
            if (_tablePlayer != null) _tablePlayer.SetMuted(blocked);
        }

        private void Awake()
        {
            foreach (Entry entry in _clips)
            {
                if (entry == null || entry.Clip == null || string.IsNullOrEmpty(entry.Key)) continue;
                if (!_lookup.TryGetValue(entry.Key, out List<AudioClip> clips)) _lookup.Add(entry.Key, clips = new List<AudioClip>());
                clips.Add(entry.Clip);
            }
        }

        private void OnEnable()
        {
            if (_catalog == null) return;
            string music = _currentMusic;
            _currentMusic = null; _tableMusicId = 0;
            StopSourceMusic();
            ConnectTable(_catalog);
            if (!string.IsNullOrEmpty(music)) PlayMusic(music, _fadeInMilliseconds);
        }

        public void Apply(GameSettings settings)
        {
            _settings = settings.Clone();
            _musicVolume = settings.MusicPercent / 100f;
            _effects.volume = settings.EffectsPercent / 100f;
            UpdateVolumes();
            if (_tablePlayer != null) _tablePlayer.Apply(_settings);
        }

        // 기존 문자열 호출은 유지하며 새 상황 어댑터는 이 사운드 ID 진입점을 사용합니다.
        public void ConnectTable(IReadOnlyDataCatalog data)
        {
            if (_tablePlayer == null) _tablePlayer = GetComponent<TableSoundPlayer>();
            if (_tablePlayer == null) _tablePlayer = gameObject.AddComponent<TableSoundPlayer>();
            if (data == null) throw new ArgumentNullException(nameof(data));
            var sounds = data.GetTable<Sound>();
            var clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            var ids = new List<int>();
            _cueIds.Clear();
            foreach (Entry entry in _clips)
            {
                if (entry == null || entry.SoundId == 0) continue;
                if (entry.Clip == null || string.IsNullOrEmpty(entry.Key)) throw new InvalidOperationException("Sound ID에 연결할 소리 키·클립이 없습니다.");
                Sound sound = sounds.Get(entry.SoundId);
                if (clips.TryGetValue(sound.Address, out AudioClip existing) && existing != entry.Clip)
                    throw new InvalidOperationException("같은 Sound 주소에 서로 다른 클립이 연결됐습니다: " + sound.Address);
                clips[sound.Address] = entry.Clip;
                if (!_cueIds.TryGetValue(entry.Key, out List<int> group)) _cueIds.Add(entry.Key, group = new List<int>());
                if (!group.Contains(sound.ID)) group.Add(sound.ID);
                if (!ids.Contains(sound.ID)) ids.Add(sound.ID);
            }
            // 같은 음원 주소를 쓰는 다른 Sound 행(맵별 환경음 행 등)도 프리팹 클립으로 함께 준비합니다.
            foreach (Sound sound in sounds.All)
                if (clips.ContainsKey(sound.Address) && !ids.Contains(sound.ID)) ids.Add(sound.ID);
            string music = _tableMusicId == 0 ? null : _currentMusic;
            if (music != null) _currentMusic = null;
            _tableMusicId = 0; _catalog = data;
            _tablePlayer.Connect(data, new PrefabAudioSource(clips));
            // 이 목록은 이미 프리팹에 들어 있는 클립뿐이라 준비가 즉시 끝납니다. 비동기 대기는 하지 않습니다.
            Task preparation = _tablePlayer.PrepareAsync(ids);
            if (!preparation.IsCompleted) throw new InvalidOperationException("프리팹 소리 준비가 즉시 완료되지 않았습니다.");
            preparation.GetAwaiter().GetResult();
            _tablePlayer.Apply(_settings);
            _tablePlayer.SetPaused(_pauseScale < 1f);
            _tablePlayer.SetMuted(_cutsceneOwners.Count > 0);
            if (music != null) PlayMusic(music);
            // 재연결은 재생 채널을 비우므로 요청된 환경음을 다시 시작합니다.
            _ambienceVoice = 0;
            StartAmbience();
        }
        public Task PrepareAsync(IReadOnlyList<int> soundIds, CancellationToken cancellation = default)
        {
            if (_tablePlayer == null) throw new InvalidOperationException("Sound 표를 먼저 연결해 주세요.");
            return _tablePlayer.PrepareAsync(soundIds, cancellation);
        }
        public bool Play(int soundId)
        {
            if (_tablePlayer == null) throw new InvalidOperationException("Sound 표를 먼저 연결해 주세요.");
            return _tablePlayer.Play(soundId);
        }
        public void Stop(int soundId) { if (_tablePlayer != null) _tablePlayer.Stop(soundId); }

        // 일시정지 중 배경음 50%(O04).
        public void SetPaused(bool paused)
        {
            _pauseScale = paused ? 0.5f : 1f;
            UpdateVolumes();
            if (_tablePlayer != null) _tablePlayer.SetPaused(paused);
        }

        public void PlayMusic(string key) => PlayMusic(key, _crossfadeMilliseconds);

        public void PlayMusic(string key, int fadeInMilliseconds)
        {
            if (fadeInMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(fadeInMilliseconds));
            if (string.IsNullOrEmpty(key) || key == _currentMusic) return;
            if (_cueIds.TryGetValue(key, out List<int> ids))
            {
                _tablePlayer.Play(ids[0]);
                if (_tableMusicId != 0) _tablePlayer.Stop(_tableMusicId);
                StopSourceMusic();
                _tableMusicId = ids[0]; _currentMusic = key;
                _fadeInMilliseconds = fadeInMilliseconds;
                return;
            }
            if (!_lookup.TryGetValue(key, out List<AudioClip> clips)) return;
            if (_tableMusicId != 0) { _tablePlayer.Stop(_tableMusicId); _tableMusicId = 0; }
            AudioClip clip = clips[0];
            if (_currentMusic != null && !CurrentFade.IsFadingOut)
                CurrentFade.Begin(0f, _crossfadeMilliseconds);
            _currentMusic = key;
            _fadeInMilliseconds = fadeInMilliseconds;
            _usingA = !_usingA;
            AudioSource next = _usingA ? _musicA : _musicB;
            CurrentFade = default;
            CurrentFade.Begin(1f, fadeInMilliseconds);
            next.clip = clip;
            next.loop = true;
            UpdateVolumes();
            next.Play();
        }

        // 배경음과 겹쳐 도는 환경음(Sound ID, 루프)입니다. 빈 값은 정지, 같은 ID·같은 음원 주소는 끊지 않고 이어서 재생합니다.
        public void PlayAmbience(int? soundId)
        {
            int id = soundId ?? 0;
            if (id == _ambienceId) return;
            _ambienceId = id;
            StartAmbience();
        }
        private void StartAmbience()
        {
            // 표 연결 전 요청은 기억해 두고 ConnectTable에서 시작합니다.
            if (_tablePlayer == null || _catalog == null) return;
            IReadOnlyDataTable<Sound> sounds = _catalog.GetTable<Sound>();
            string address = _ambienceId == 0 ? null : sounds.Get(_ambienceId).Address;
            if (_ambienceVoice != 0 && address == sounds.Get(_ambienceVoice).Address) return;
            // 실제로 끈 환경음만 이어 재생 기록을 지웁니다. 새 씬의 표 연결(아직 요청 전)은 기록을 유지합니다.
            if (_ambienceVoice != 0) { _tablePlayer.Stop(_ambienceVoice); _carriedAddress = null; }
            _ambienceVoice = 0;
            if (address == null) return;
            string carried = _carriedAddress;
            if (!_tablePlayer.IsPrepared(_ambienceId)) { Debug.LogError($"환경음 Sound {_ambienceId}의 클립이 GameAudio 소리 목록에 없습니다.", this); return; }
            _tablePlayer.Play(_ambienceId);
            _ambienceVoice = _ambienceId;
            if (carried == address) _tablePlayer.Seek(_ambienceId, _carriedSample, Math.Max(0, NowMilliseconds() - _carriedAtMs));
            CarryAmbience();
        }
        private void CarryAmbience()
        {
            int sample = _tablePlayer.ReadSample(_ambienceVoice);
            if (sample < 0) return;
            _carriedAddress = _catalog.GetTable<Sound>().Get(_ambienceVoice).Address;
            _carriedSample = sample;
            _carriedAtMs = NowMilliseconds();
        }
        // Unity 경계: 실제 시각(초)은 읽는 즉시 정수 ms로 바꿉니다.
        private static long NowMilliseconds() => checked((long)Math.Floor(Time.unscaledTimeAsDouble * 1000.0));

        public void FadeOutMusic(int milliseconds)
        {
            if (milliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (_currentMusic == null) return;
            if (_tableMusicId != 0) { StopMusic(); return; }
            _currentMusic = null;
            CurrentFade.Begin(0f, milliseconds);
        }

        public void StopMusic()
        {
            if (_tableMusicId != 0 && _tablePlayer != null) _tablePlayer.Stop(_tableMusicId);
            _tableMusicId = 0; _currentMusic = null;
            StopSourceMusic();
        }

        private void StopSourceMusic()
        {
            _fadeA = _fadeB = default;
            _fadeClock = default;
            if (_musicA != null) _musicA.Stop();
            if (_musicB != null) _musicB.Stop();
        }

        // 같은 프레임 근처에 여러 몹이 함께 맞으면 같은 피격음이 겹쳐 크게 들립니다.
        // 같은 소리 묶음(몹 피격음은 종류와 무관하게 한 묶음, 플레이어 소리는 별도)은 짧은 간격 안에 한 번만 재생합니다.
        private const long _effectMergeMilliseconds = 60;
        private readonly Dictionary<string, long> _lastEffectTimes = new Dictionary<string, long>(StringComparer.Ordinal);
        private bool ShouldMergeEffect(string key)
        {
            string group = key.EndsWith("_HIT", StringComparison.Ordinal) && !key.StartsWith("PLAYER_", StringComparison.Ordinal) ? "*_HIT" : key;
            // 누적하지 않고 두 시각의 차이만 비교합니다.
            long now = NowMilliseconds();
            if (_lastEffectTimes.TryGetValue(group, out long last) && now - last < _effectMergeMilliseconds) return true;
            _lastEffectTimes[group] = now;
            return false;
        }

        public void PlayEffect(string key)
        {
            if (string.IsNullOrEmpty(key) || ShouldMergeEffect(key)) return;
            if (_cueIds.TryGetValue(key, out List<int> ids))
            {
                _tablePlayer.Play(ids[_effectRandom.Next(ids.Count)]);
                return;
            }
            if (_effects != null && _lookup.TryGetValue(key, out List<AudioClip> clips))
                _effects.PlayOneShot(clips[_effectRandom.Next(clips.Count)]);
        }

        private void Update()
        {
            if (_ambienceVoice != 0 && _tablePlayer != null) CarryAmbience();
            if (!_fadeA.IsRunning && !_fadeB.IsRunning) return;
            int milliseconds = _fadeClock.AdvanceFrame(Time.unscaledDeltaTime);
            _fadeA.Step(_musicA, milliseconds);
            _fadeB.Step(_musicB, milliseconds);
            UpdateVolumes();
        }

        private void UpdateVolumes()
        {
            float volume = _musicVolume * _pauseScale;
            _musicA.volume = volume * _fadeA.Gain;
            _musicB.volume = volume * _fadeB.Gain;
        }
    }
}
