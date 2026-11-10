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
        [SerializeField] private AnimationClip _climb = null;
        [SerializeField] private AnimationClip _save = null;
        [SerializeField] private AnimationClip _ladderClimb = null;
        [SerializeField, Range(0.5f, 1.5f)] private float _climbStartScale = 0.95f;
        [SerializeField, Range(1f, 2f)] private float _hangScale = 1.4f;
        [SerializeField, Range(-0.5f, 0f)] private float _hangVerticalOffset = -0.1f;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _basePosition;
        private float _hangTop;
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
            position.y = feet - PreviewPresentation.VisibleBottom(Renderer.sprite) * _baseScale.y;
            Renderer.transform.localPosition = _basePosition = position;
        }
        private bool _onLadder;
        private int _ladderDirection;
        protected override float AnimationDelta => _onLadder ? Time.deltaTime * _ladderDirection : base.AnimationDelta;
        private AnimationClip _action;
        private float _actionElapsed;
        private int _facing = 1;
        protected override void OnEnable()
        {
            base.OnEnable();
            if (_player != null) _player.PresentationCue += OnPresentationCue;
        }
        private void OnDisable()
        {
            if (_player != null) _player.PresentationCue -= OnPresentationCue;
            _action = null; _onLadder = false; _ladderDirection = 0;
            if (Renderer != null)
            {
                Renderer.transform.localScale = _baseScale;
                Renderer.transform.localPosition = _basePosition;
            }
        }
        private void OnPresentationCue(string cue)
        {
            if (cue == "LadderExit") { _onLadder = false; _ladderDirection = 0; return; }
            if (cue == "LadderUp" || cue == "LadderDown" || cue == "LadderHold")
            {
                if (!_onLadder) RestartAnimation();
                _onLadder = true; _ladderDirection = cue == "LadderUp" ? 1 : cue == "LadderDown" ? -1 : 0;
                _action = null;
                return;
            }
            _action = cue == "Climb" ? _climb : cue == "Save" ? _save : null;
            _actionElapsed = 0f;
            if (_action != null) RestartAnimation();
        }
        protected override void LateUpdate()
        {
            bool hanging = _player != null && _player.Actor != null && _player.Actor.IsHanging;
            float progress = hanging ? Mathf.Clamp01(_player.ClimbProgress) : 0f;
            float scale = hanging && _player.ClimbProgress >= 0f ? Mathf.Lerp(_climbStartScale * _hangScale, 1f, progress) :
                hanging ? (_onLadder ? _climbStartScale : _hangScale) : 1f;
            if (Renderer != null)
            {
                Renderer.transform.localScale = _baseScale * scale;
                Vector3 position = _basePosition;
                // 손의 기존 높이를 기준으로 확대하고 절벽 자세만 조금 내립니다.
                if (hanging && !_onLadder)
                    position.y += (1f - progress) * (_hangVerticalOffset - (_hangScale - 1f) * _hangTop * _baseScale.y);
                Renderer.transform.localPosition = position;
            }
            base.LateUpdate();
            if (_action != null && (_actionElapsed += Time.deltaTime) >= _action.length) _action = null;
        }

        protected override AnimationClip SelectAnimation(StateKey state)
        {
            if (_onLadder && state == StateKey.Hang && _ladderClimb != null) return _ladderClimb;
            if (_action != null)
            {
                if (state == StateKey.Idle || state == StateKey.Walk) return _action;
                _action = null;
            }
            return state == StateKey.Attack1 && _airAttack != null &&
                (!_player.Actor.Locomotion.IsGrounded || CurrentAnimation == _airAttack)
                ? _airAttack : base.SelectAnimation(state);
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
            Sprite sprite = FindFrame(state);
            // 4타 원본이 없을 때만 이전 마무리 프레임을 재사용합니다.
            if (sprite == null && state == StateKey.Attack4) sprite = FindFrame(StateKey.Attack3);
            return sprite ?? Fallback;
        }

        protected override void UpdateFacing()
        {
            if (_player.Actor.Locomotion.Direction != 0) _facing = _player.Actor.Locomotion.Direction;
            Renderer.flipX = _sourceFacesLeft ? _facing > 0 : _facing < 0;
        }
    }
}
