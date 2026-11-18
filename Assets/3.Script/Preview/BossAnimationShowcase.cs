using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 애니메이션 확인 씬 전용: 쇄문아비 1페이즈 스프라이트 상태와 2페이즈 리그 클립을 차례로 반복합니다.
    // 프레임감·재생 배율은 게임 프리팹의 BossPhaseTwoView 값을 그대로 읽습니다. 판정·데이터와 무관합니다.
    public sealed class BossAnimationShowcase : MonoBehaviour
    {
        [Serializable] private sealed class PhaseOneStep { public string State = ""; [SerializeField, Min(1)] private int _milliseconds = 2000; public int Milliseconds => _milliseconds; }
        [Serializable] private sealed class PhaseTwoStep { public string Label = ""; public AnimationClip Clip = null; [SerializeField, Min(1)] private int _milliseconds = 2000; public int Milliseconds => _milliseconds; }
        [SerializeField, InspectorName("게임 2페이즈 표시 설정")] private BossPhaseTwoView _settings = null;
        [SerializeField, InspectorName("1페이즈 Animator")] private Animator _phaseOne = null;
        [SerializeField, InspectorName("1페이즈 이름표")] private TextMesh _phaseOneLabel = null;
        [SerializeField] private PhaseOneStep[] _phaseOneSteps = Array.Empty<PhaseOneStep>();
        [SerializeField, InspectorName("2페이즈 리그")] private GameObject _phaseTwoRig = null;
        [SerializeField, InspectorName("2페이즈 이름표")] private TextMesh _phaseTwoLabel = null;
        [SerializeField] private PhaseTwoStep[] _phaseTwoSteps = Array.Empty<PhaseTwoStep>();
        [SerializeField, InspectorName("바닥선(게임 판정 바닥과 같은 기준)")] private SpriteRenderer _ground = null;
        private PreviewBossSprite _gameSprite;
        // [정수화 미적용 이유] 편집기 확인 장면의 리그 Transform 배치 보정값입니다.
        private float _phaseTwoOriginOffset;
        private int _oneIndex = -1, _twoIndex = -1;
        private int _oneRemainingMilliseconds, _twoRemainingMilliseconds;
        private long _twoElapsedMilliseconds;
        private MillisecondClock _clock, _sampleClock;
        private RigBindPose _bind;

        private void Awake()
        {
            if (_settings == null || _phaseOne == null || _phaseTwoRig == null || _phaseOneSteps.Length == 0 || _phaseTwoSteps.Length == 0)
                throw new InvalidOperationException("보스 애니메이션 확인 씬의 참조와 재생 목록을 연결해 주세요.");
            _bind = new RigBindPose(_phaseTwoRig);
            if (_ground == null) return;
            // 게임 프리팹의 1페이즈 프레임별 접지값과 2페이즈 리그 높이(판정 상자 바닥 기준)를 그대로 씁니다.
            _gameSprite = _settings.GetComponent<PreviewBossSprite>();
            Transform body = _settings.transform.Find("PreviewVisuals/Body"), rig = _settings.transform.Find("PhaseTwoRig");
            float bodyBottom = body.localPosition.y - body.localScale.y * .5f;
            _phaseTwoOriginOffset = (rig.localPosition.y - bodyBottom) / rig.localScale.y;
        }

        private void LateUpdate()
        {
            if (_ground == null) return;
            float line = _ground.bounds.max.y;
            var sprite = _phaseOne.GetComponent<SpriteRenderer>();
            if (_gameSprite != null && sprite.sprite != null)
            {
                Vector3 position = _phaseOne.transform.position;
                position.y = line - _gameSprite.GroundContactOf(sprite.sprite) * _phaseOne.transform.lossyScale.y;
                _phaseOne.transform.position = position;
            }
            Vector3 rigPosition = _phaseTwoRig.transform.position;
            rigPosition.y = line + _phaseTwoOriginOffset * _phaseTwoRig.transform.lossyScale.y;
            _phaseTwoRig.transform.position = rigPosition;
        }

        private void Update()
        {
            int milliseconds = _clock.AdvanceFrame(Time.deltaTime);
            _oneRemainingMilliseconds -= milliseconds;
            if (_oneRemainingMilliseconds <= 0)
            {
                _oneIndex = (_oneIndex + 1) % _phaseOneSteps.Length;
                PhaseOneStep step = _phaseOneSteps[_oneIndex];
                // Animator.Play의 세 번째 인자는 초가 아니라 정규화 시각(0=클립 시작, 1=끝, float)입니다. 단계 길이는 정수 ms로 따로 셉니다.
                _phaseOne.Play(step.State, 0, 0f);
                _oneRemainingMilliseconds = step.Milliseconds;
                if (_phaseOneLabel != null) _phaseOneLabel.text = "쇄문아비 1페이즈\n" + step.State;
            }
            _twoRemainingMilliseconds -= milliseconds;
            if (_twoRemainingMilliseconds <= 0)
            {
                _twoIndex = (_twoIndex + 1) % _phaseTwoSteps.Length;
                _twoElapsedMilliseconds = 0; _sampleClock = default;
                _twoRemainingMilliseconds = _phaseTwoSteps[_twoIndex].Milliseconds;
                if (_phaseTwoLabel != null) _phaseTwoLabel.text = "쇄문아비 2페이즈\n" + _phaseTwoSteps[_twoIndex].Label;
            }
            AnimationClip clip = _phaseTwoSteps[_twoIndex].Clip;
            // 게임과 같이 대기·걷기만 재생 배율을 적용합니다. 공격 클립은 실제 게임에서 판정 시간에 맞춰 늘거나 줄어듭니다.
            _twoElapsedMilliseconds = checked(_twoElapsedMilliseconds + _sampleClock.AdvanceFrame(Time.deltaTime * (clip.isLooping ? _settings.LoopRate : 10000) / 10000d));
            // [정수화 미적용 이유] AnimationClip.SampleAnimation은 클립 안 시각을 초(float)로만 받습니다. 경과는 정수 ms로 누적하고 넘기는 순간에만 초로 바꿉니다.
            float time = clip.isLooping ? Mathf.Repeat(_twoElapsedMilliseconds / 1000f, clip.length) : Mathf.Min(_twoElapsedMilliseconds / 1000f, clip.length);
            _bind.Restore();
            clip.SampleAnimation(_phaseTwoRig, BossPhaseTwoView.Frame(time, _settings.FramesPerSecond));
        }
    }
}
