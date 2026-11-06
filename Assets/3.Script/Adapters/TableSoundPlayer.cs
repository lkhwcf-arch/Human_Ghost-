using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 조건 판단은 외부에서 받습니다. 이 모듈은 표 조회·음원 수명·재생 채널만 소유합니다.
    [DisallowMultipleComponent]
    public class TableSoundPlayer : MonoBehaviour
    {
        private sealed class Voice
        {
            public Sound Definition;
            public AudioSource Source;
            public SoundEnvelope Envelope;
        }
        private readonly List<Voice> _voices = new List<Voice>();
        private readonly Stack<AudioSource> _pool = new Stack<AudioSource>();
        private IReadOnlyDataTable<Sound> _sounds;
        private IAssetSource<AudioClip> _source;
        private PreparedAssetStore<AudioClip> _prepared;
        private GameSettings _settings = new GameSettings();
        private MillisecondClock _clock;
        private bool _paused;
        private bool _muted;
        public void SetMuted(bool muted)
        {
            _muted = muted;
            foreach (Voice voice in _voices) voice.Source.mute = muted;
        }
        public int PlayingCount => _voices.Count;
        public event Action<int> Started;
        public event Action<int> Finished;

        public void Connect(IReadOnlyDataCatalog data, IAssetSource<AudioClip> source = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var sounds = data.GetTable<Sound>();
            EndLifetime();
            _sounds = sounds;
            _source = source ?? new AddressableAssetSource<AudioClip>();
            if (isActiveAndEnabled) BeginLifetime();
        }

        public void Apply(GameSettings settings)
        {
            _settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Clone();
            foreach (Voice voice in _voices) ApplyVolume(voice);
        }
        public void SetPaused(bool paused)
        {
            _paused = paused;
            foreach (Voice voice in _voices) ApplyVolume(voice);
        }

        // 준비할 목록은 호출자가 정합니다. 재생 명령 안에서는 파일을 읽지 않습니다.
        public Task PrepareAsync(IReadOnlyList<int> soundIds, CancellationToken cancellation = default)
        {
            RequireActive();
            if (soundIds == null) throw new ArgumentNullException(nameof(soundIds));
            var addresses = new string[soundIds.Count];
            for (int index = 0; index < addresses.Length; index++) addresses[index] = _sounds.Get(soundIds[index]).Address;
            // 비동기 작업은 준비 저장소만 소유하며 이 MonoBehaviour를 완료 콜백으로 호출하지 않습니다.
            return _prepared.PrepareAsync(addresses, cancellation);
        }

        public bool IsPrepared(int soundId) => this != null && isActiveAndEnabled && _sounds != null && _prepared != null
            && _prepared.TryGet(_sounds.Get(soundId).Address, out _);

        public bool Play(int soundId)
        {
            RequireActive();
            Sound definition = _sounds.Get(soundId);
            if (!_prepared.TryGet(definition.Address, out AudioClip clip) || clip == null)
                throw new InvalidOperationException($"사운드 {soundId}가 준비되지 않았습니다. 준비 단계에서 PrepareAsync를 완료해 주세요. 주소: {definition.Address}");
            AudioSource channel = AcquireChannel();
            channel.mute = _muted;
            try
            {
                var voice = new Voice { Definition = definition, Source = channel,
                    Envelope = new SoundEnvelope(definition.FadeInMs, definition.FadeOutMs) };
                channel.clip = clip;
                channel.loop = definition.Loop;
                ApplyVolume(voice);
                channel.gameObject.SetActive(true);
                channel.Play();
                if (definition.Category == 1)
                    foreach (Voice existing in _voices)
                        if (existing.Definition.Category == 1) { existing.Envelope.Stop(); ApplyVolume(existing); }
                _voices.Add(voice);
            }
            catch { ReturnChannel(channel); throw; }
            Started?.Invoke(soundId);
            return true;
        }

        private void RequireActive()
        {
            if (this == null || !isActiveAndEnabled || _sounds == null || _prepared == null)
                throw new InvalidOperationException("사운드 재생기를 활성화하고 준비된 데이터를 먼저 연결해 주세요.");
        }

        public void Stop(int soundId)
        {
            foreach (Voice voice in _voices)
                if (voice.Definition.ID == soundId) { voice.Envelope.Stop(); ApplyVolume(voice); }
        }
        public void StopAll()
        {
            foreach (Voice voice in _voices) { voice.Envelope.Stop(); ApplyVolume(voice); }
        }

        protected virtual void OnEnable() { if (_sounds != null) BeginLifetime(); }
        protected virtual void OnDisable() => EndLifetime();
        private void BeginLifetime()
        {
            if (_prepared != null) return;
            _clock = default;
            _prepared = new PreparedAssetStore<AudioClip>(_source);
        }
        private void EndLifetime()
        {
            var prepared = _prepared;
            _prepared = null;
            // 채널에서 음원 참조를 먼저 제거한 뒤 준비 리소스를 반환합니다.
            foreach (Voice voice in _voices) Release(voice);
            _voices.Clear();
            prepared?.Dispose();
        }

        protected virtual void Update()
        {
            if (_voices.Count == 0) return;
            int elapsed = _clock.Advance(Time.unscaledDeltaTime);
            for (int index = _voices.Count - 1; index >= 0; --index)
            {
                Voice voice = _voices[index];
                if (voice.Source == null || voice.Source.clip == null)
                {
                    _voices.RemoveAt(index);
                    Release(voice);
                    Debug.LogError($"재생 중인 사운드 {voice.Definition.ID}의 채널 또는 음원 연결이 제거됐습니다.", this);
                    continue;
                }
                if (!voice.Definition.Loop && !voice.Envelope.IsStopping
                    && (voice.Source.clip.length - voice.Source.time) * 1000f <= voice.Definition.FadeOutMs)
                    voice.Envelope.Stop();
                voice.Envelope.Tick(elapsed);
                ApplyVolume(voice);
                if (!voice.Envelope.IsStopped && voice.Source.isPlaying) continue;
                _voices.RemoveAt(index);
                Release(voice);
                Finished?.Invoke(voice.Definition.ID);
            }
        }

        private void ApplyVolume(Voice voice)
        {
            // 외부에서 채널을 제거한 경우 다음 갱신에서 정리하며 빈 객체는 호출하지 않습니다.
            if (voice.Source == null) return;
            int category = voice.Definition.Category == 1 ? _settings.MusicPercent : _settings.EffectsPercent;
            voice.Source.volume = voice.Definition.Volume / 10000f * category / 100f * voice.Envelope.Gain / 10000f
                * (voice.Definition.Category == 1 && _paused ? 0.5f : 1f);
        }
        private AudioSource AcquireChannel()
        {
            while (_pool.Count > 0)
            {
                AudioSource channel = _pool.Pop();
                if (channel != null) return channel;
            }
            var root = new GameObject("SoundChannel");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            var created = root.AddComponent<AudioSource>();
            created.playOnAwake = false;
            created.spatialBlend = 0f;
            return created;
        }
        private void ReturnChannel(AudioSource channel)
        {
            if (channel == null) return;
            channel.Stop();
            channel.clip = null;
            channel.gameObject.SetActive(false);
            _pool.Push(channel);
        }
        private void Release(Voice voice)
        {
            ReturnChannel(voice.Source);
        }
    }
}
