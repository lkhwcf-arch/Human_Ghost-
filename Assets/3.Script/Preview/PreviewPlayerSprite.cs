using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    [DefaultExecutionOrder(10)]
    public class PreviewPlayerSprite : PreviewStateSprite
    {
        [SerializeField, InspectorName("플레이어 런타임")] private PlayerRuntime _player = null;
        [SerializeField, InspectorName("원본이 왼쪽을 봄")] private bool _sourceFacesLeft = true;
        [SerializeField] private AnimationClip _airAttack = null;
        [SerializeField] private AnimationClip _walk = null;
        private bool _nonCombatArea;
        public void SetNonCombatArea(bool value) { _nonCombatArea = value; RestartAnimation(); }
        [SerializeField] private AnimationClip _climb = null;
        [SerializeField] private AnimationClip _save = null;
        [SerializeField] private AnimationClip _ladderClimb = null;
        [SerializeField, InspectorName("사다리 표시 배율"), Range(1f, 1.5f)] private float _ladderScale = 1.1f;
        [SerializeField, Range(0.5f, 1.5f)] private float _climbStartScale = 0.95f;
        [SerializeField, Range(1f, 2f)] private float _hangScale = 1.4f;
        [SerializeField, Range(-0.5f, 0f)] private float _hangVerticalOffset = -0.1f;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _basePosition;
        private float _hangTop;
        private float _standingBottom;
        protected override bool AlignFeet => false;
        protected override void Awake()
        {
            base.Awake();
            if (Renderer == null) return;
            _baseScale = Renderer.transform.localScale;
            _basePosition = Renderer.transform.localPosition;
            Sprite hang = FindFrame(StateKey.Hang);
            if (hang != null)
                foreach (Vector2 vertex in hang.vertices) _hangTop = Mathf.Max(_hangTop, vertex.y);
            var idle = base.SelectAnimation(StateKey.Idle);
            if (idle == null || Fallback == null) return;
            float feet = Renderer.transform.localPosition.y + PreviewPresentation.VisibleBottom(Fallback) * _baseScale.y;
            idle.SampleAnimation(Renderer.gameObject, 0f);
            if (Renderer.sprite == null) return;
            Vector3 position = Renderer.transform.localPosition;
            _standingBottom = PreviewPresentation.VisibleBottom(Renderer.sprite);
            position.y = feet - _standingBottom * _baseScale.y;
            Renderer.transform.localPosition = _basePosition = position;
        }
        private bool _onLadder;
        private int _ladderDirection;
        protected override float AnimationDelta => _onLadder ? Time.deltaTime * _ladderDirection : base.AnimationDelta;
        private AnimationClip _action;
        private float _actionElapsed;
        // 아래 점프는 도약 웅크림(0~2)을 건너뛰고 공중 몸말기(3~5)에서 낙하 프레임(6)으로 넘어가 착지까지 유지합니다.
        private const float _dropStartTime = 0.3f, _dropFallTime = 0.6f, _dropBlendSeconds = 0.2f;
        private float _dropElapsed = -1f;
        protected override void OnEnable()
        {
            base.OnEnable();
            if (_player != null) _player.PresentationCue += OnPresentationCue;
        }
        private void OnDisable()
        {
            if (_player != null) _player.PresentationCue -= OnPresentationCue;
            _action = null; _onLadder = false; _ladderDirection = 0; _nonCombatArea = false; _dropElapsed = -1f;
            if (Renderer != null)
            {
                Renderer.transform.localScale = _baseScale;
                Renderer.transform.localPosition = _basePosition;
            }
        }
        private void OnPresentationCue(string cue)
        {
            if (cue == "Parry" || cue == "ActionExit" || cue == "ClimbExit") { _action = null; RestartAnimation(); return; }
            if (cue == "LadderExit") { _onLadder = false; _ladderDirection = 0; return; }
            if (cue == "DropThrough") { _dropElapsed = 0f; _action = null; RestartAnimation(); return; }
            if (cue == "LadderUp" || cue == "LadderDown" || cue == "LadderHold")
            {
                if (!_onLadder) RestartAnimation();
                _onLadder = true; _ladderDirection = cue == "LadderUp" ? 1 : cue == "LadderDown" ? -1 : 0;
                _action = null;
                return;
            }
            _action = cue == "Save" ? _save : null;
            _actionElapsed = 0f;
            if (_action != null) RestartAnimation();
        }
        protected override void LateUpdate()
        {
            bool hanging = _player != null && _player.Actor != null && _player.Actor.IsHanging;
            // 크기·높이 보정은 이동과 같이 양 끝을 감속합니다. 프레임 선택은 TryGetAnimationTime의 선형 진행률을 그대로 씁니다.
            float progress = hanging ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_player.ClimbProgress)) : 0f;
            float scale = hanging && _player.ClimbProgress >= 0f ? Mathf.Lerp(_climbStartScale * _hangScale, 1f, progress) :
                hanging ? (_onLadder ? _ladderScale : _hangScale) : 1f;
            if (Renderer != null)
            {
                Renderer.transform.localScale = _baseScale * scale;
                Vector3 position = _basePosition;
                // 기준 발 높이를 고정하며 전체 사다리 주기에 같은 배율을 씁니다.
                if (hanging && _onLadder) position.y += (1f - scale) * _standingBottom * _baseScale.y;
                // 손의 기존 높이를 기준으로 확대하고 절벽 자세만 조금 내립니다.
                if (hanging && !_onLadder)
                    position.y += (1f - progress) * (_hangVerticalOffset - (_hangScale - 1f) * _hangTop * _baseScale.y);
                Renderer.transform.localPosition = position;
            }
            base.LateUpdate();
            if (_action != null && (_actionElapsed += Time.deltaTime) >= _action.length) _action = null;
            if (_dropElapsed >= 0f) _dropElapsed = _player != null && _player.Actor != null && _player.Actor.CurrentState == StateKey.Jump ? _dropElapsed + Time.deltaTime : -1f;
        }

        protected override AnimationClip SelectAnimation(StateKey state)
        {
            if (_player.Actor.IsParrying) return null;
            if (state == StateKey.Hang && _player.ClimbProgress >= 0f && _climb != null) return _climb;
            if (_onLadder && state == StateKey.Hang && _ladderClimb != null) return _ladderClimb;
            if (_action != null)
            {
                if (state == StateKey.Idle || state == StateKey.Walk) return _action;
                _action = null;
            }
            if (_nonCombatArea && state == StateKey.Walk && _walk != null) return _walk;
            return _player.Actor.IsAirAttacking && _airAttack != null ? _airAttack : base.SelectAnimation(state);
        }
        protected override bool TryGetAnimationTime(StateKey state, AnimationClip animation, out float time)
        {
            if (state == StateKey.Hang && _player.ClimbProgress >= 0f && animation == _climb)
            {
                time = _player.ClimbProgress * animation.length;
                return true;
            }
            var actor = _player.Actor;
            if (actor.IsAttacking || (state == StateKey.Skill1 && actor.CurrentAttack != null))
            {
                if (!actor.IsAirAttacking && TryMapAttackTime(state, animation, 0, actor.CurrentAttack, actor.AttackElapsed, false, out time)) return true;
                time = actor.AttackElapsed / (float)actor.CurrentAttack.TotalTime * animation.length;
                return true;
            }
            if (state == StateKey.Jump && _dropElapsed >= 0f)
            {
                time = Mathf.Lerp(_dropStartTime, _dropFallTime, _dropElapsed / _dropBlendSeconds);
                return true;
            }
            time = 0f; return false;
        }
        protected override float Duration(StateKey state)
        {
            if (_action != null) return 0f;
            var actor = _player.Actor;
            if ((actor.IsAttacking || state == StateKey.Skill1) && actor.CurrentAttack != null) return actor.CurrentAttack.TotalTime / 1000f;
            switch (state)
            {
                case StateKey.Hit: return actor.Definition.HitStunTime / 1000f;
                case StateKey.CritHit: return actor.Definition.CritHitStunTime / 1000f;
                case StateKey.Roll: return actor.Definition.RollTime / 1000f;
                case StateKey.Land: return actor.Definition.LandTime / 1000f;
                default: return 0f;
            }
        }

        protected override bool TryGetState(out StateKey state)
        {
            state = _player != null && _player.Actor != null ? _player.Actor.CurrentState : StateKey.Idle;
            return _player != null && _player.Actor != null;
        }

        protected override Sprite SelectFrame(StateKey state)
        {
            if (_player != null && _player.Actor != null && _player.Actor.IsParrying)
                return FindFrame(StateKey.Attack1) ?? Fallback;
            Sprite sprite = FindFrame(state);
            // 4타 원본이 없을 때만 이전 마무리 프레임을 재사용합니다.
            if (sprite == null && state == StateKey.Attack4) sprite = FindFrame(StateKey.Attack3);
            return sprite ?? Fallback;
        }

        protected override void UpdateFacing()
        {
            int facing = _player.Actor.Facing;
            bool flip = _sourceFacesLeft ? facing > 0 : facing < 0;
            if (Renderer.flipX != flip) Renderer.flipX = flip;
        }
    }
}
