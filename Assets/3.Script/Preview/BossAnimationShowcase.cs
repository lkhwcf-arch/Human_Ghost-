using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 애니메이션 확인 씬 전용: 쇄문아비 1페이즈 스프라이트 상태와 2페이즈 리그 클립을 차례로 반복합니다.
    // 프레임감·재생 배율은 게임 프리팹의 BossPhaseTwoView 값을 그대로 읽습니다. 판정·데이터와 무관합니다.
    public sealed class BossAnimationShowcase : MonoBehaviour
    {
        [Serializable] private sealed class PhaseOneStep { public string State = ""; public float Seconds = 2f; }
        [Serializable] private sealed class PhaseTwoStep { public string Label = ""; public AnimationClip Clip = null; public float Seconds = 2f; }
        [SerializeField, InspectorName("게임 2페이즈 표시 설정")] private BossPhaseTwoView _settings = null;
        [SerializeField, InspectorName("1페이즈 Animator")] private Animator _phaseOne = null;
        [SerializeField, InspectorName("1페이즈 이름표")] private TextMesh _phaseOneLabel = null;
        [SerializeField] private PhaseOneStep[] _phaseOneSteps = Array.Empty<PhaseOneStep>();
        [SerializeField, InspectorName("2페이즈 리그")] private GameObject _phaseTwoRig = null;
        [SerializeField, InspectorName("2페이즈 이름표")] private TextMesh _phaseTwoLabel = null;
        [SerializeField] private PhaseTwoStep[] _phaseTwoSteps = Array.Empty<PhaseTwoStep>();
        private int _oneIndex = -1, _twoIndex = -1;
        private float _oneRemaining, _twoElapsed, _twoRemaining;
        private RigBindPose _bind;

        private void Awake()
        {
            if (_settings == null || _phaseOne == null || _phaseTwoRig == null || _phaseOneSteps.Length == 0 || _phaseTwoSteps.Length == 0)
                throw new InvalidOperationException("보스 애니메이션 확인 씬의 참조와 재생 목록을 연결해 주세요.");
            _bind = new RigBindPose(_phaseTwoRig);
        }

        private void Update()
        {
            _oneRemaining -= Time.deltaTime;
            if (_oneRemaining <= 0f)
            {
                _oneIndex = (_oneIndex + 1) % _phaseOneSteps.Length;
                PhaseOneStep step = _phaseOneSteps[_oneIndex];
                _phaseOne.Play(step.State, 0, 0f);
                _oneRemaining = step.Seconds;
                if (_phaseOneLabel != null) _phaseOneLabel.text = "쇄문아비 1페이즈\n" + step.State;
            }
            _twoRemaining -= Time.deltaTime;
            if (_twoRemaining <= 0f)
            {
                _twoIndex = (_twoIndex + 1) % _phaseTwoSteps.Length;
                _twoElapsed = 0f;
                _twoRemaining = _phaseTwoSteps[_twoIndex].Seconds;
                if (_phaseTwoLabel != null) _phaseTwoLabel.text = "쇄문아비 2페이즈\n" + _phaseTwoSteps[_twoIndex].Label;
            }
            AnimationClip clip = _phaseTwoSteps[_twoIndex].Clip;
            // 게임과 같이 대기·걷기만 재생 배율을 적용합니다. 공격 클립은 실제 게임에서 판정 시간에 맞춰 늘거나 줄어듭니다.
            _twoElapsed += Time.deltaTime * (clip.isLooping ? _settings.LoopSpeed : 1f);
            float time = clip.isLooping ? Mathf.Repeat(_twoElapsed, clip.length) : Mathf.Min(_twoElapsed, clip.length);
            _bind.Restore();
            clip.SampleAnimation(_phaseTwoRig, BossPhaseTwoView.Frame(time, _settings.FramesPerSecond));
        }
    }
}
