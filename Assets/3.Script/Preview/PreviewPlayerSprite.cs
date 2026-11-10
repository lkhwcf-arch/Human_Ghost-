using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    public class PreviewPlayerSprite : PreviewStateSprite
    {
        [SerializeField, InspectorName("플레이어 런타임")] private PlayerRuntime _player = null;
        [SerializeField, InspectorName("원본이 왼쪽을 봄")] private bool _sourceFacesLeft = true;
        [SerializeField] private AnimationClip _airAttack = null;
        [SerializeField] private AnimationClip _climb = null;
        [SerializeField] private AnimationClip _save = null;
        [SerializeField] private AnimationClip _ladderClimb = null;
        private bool _onLadder;
        private int _ladderDirection;
        protected override float AnimationDelta => _player != null && _player.Actor != null && _player.Actor.IsParrying ? 0f : _onLadder ? Time.deltaTime * _ladderDirection : base.AnimationDelta;
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
        }
        private void OnPresentationCue(string cue)
        {
            if (cue == "Parry") { _action = null; RestartAnimation(); return; }
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
            base.LateUpdate();
            if (_action != null && (_actionElapsed += Time.deltaTime) >= _action.length) _action = null;
        }

        protected override AnimationClip SelectAnimation(StateKey state)
        {
            if (_player.Actor.IsParrying) return base.SelectAnimation(StateKey.Attack1);
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
            _facing = _player.Actor.Facing;
            Renderer.flipX = _sourceFacesLeft ? _facing > 0 : _facing < 0;
        }
    }
}
