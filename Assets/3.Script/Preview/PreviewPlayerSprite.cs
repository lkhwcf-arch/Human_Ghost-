using System;
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
        private AnimationClip _executionAnimation;
        public void SetExecutionAnimation(AnimationClip clip) => _executionAnimation = clip;
        [SerializeField] private AnimationClip _walk = null;
        private bool _nonCombatArea;
        public void SetNonCombatArea(bool value) { _nonCombatArea = value; RestartAnimation(); }
        [SerializeField] private AnimationClip _climb = null;
        [SerializeField] private AnimationClip _save = null;
        [SerializeField] private AnimationClip _ladderClimb = null;
        [SerializeField, InspectorName("사다리 표시 배율"), Range(1f, 1.5f)] private float _ladderScale = 1.1f;
        [SerializeField, Range(0.5f, 1.5f)] private float _climbStartScale = 1f;
        [SerializeField, Range(1f, 2f)] private float _hangScale = 1f;
        [SerializeField, Range(-0.5f, 0f)] private float _hangVerticalOffset = -0.1f;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _basePosition;
        private float _hangTop;
        private float _standingBottom;
        private Vector2 _ledgeHalfSize = new Vector2(.3f, .65f);
        private Vector3 _climbHandAnchor;
        private readonly System.Collections.Generic.Dictionary<Sprite, Vector2> _handAnchors = new System.Collections.Generic.Dictionary<Sprite, Vector2>();
        public void SetLedgeBody(Vector2 halfSize) => _ledgeHalfSize = halfSize;
        private Vector2 HandAnchor(Sprite sprite)
        {
            if (_handAnchors.TryGetValue(sprite, out Vector2 anchor)) return anchor;
            Vector2[] vertices = sprite.vertices; float top = float.NegativeInfinity;
            foreach (Vector2 vertex in vertices) top = Mathf.Max(top, vertex.y);
            float x = 0f; int count = 0;
            foreach (Vector2 vertex in vertices) if (vertex.y >= top - .02f) { x += vertex.x; count++; }
            anchor = new Vector2(count > 0 ? x / count : 0f, top); _handAnchors.Add(sprite, anchor); return anchor;
        }
        private Vector3 LedgeHandPosition => _player.transform.position + new Vector3(_player.Actor.Facing * _ledgeHalfSize.x, _ledgeHalfSize.y, 0f);
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
        private bool _onLadder, _wasInAir;
        private int _ladderDirection;
        protected override float AnimationDelta => _onLadder ? Time.deltaTime * _ladderDirection : base.AnimationDelta;
        private AnimationClip _action;
        private int _actionElapsedMilliseconds;
        private MillisecondClock _actionClock;
        private bool _praying, _saveRising;
        private float SaveRiseDuration => _save == null ? 0f : Mathf.Min(.6f, _save.length);
        public float BeginSaveRise()
        {
            _praying = false; _saveRising = _save != null; _action = _save; _actionElapsedMilliseconds = 0; _actionClock = default;
            RestartAnimation();
            return SaveRiseDuration;
        }
        // 아래 점프는 도약 준비를 건너뛰고 공중 자세를 유지합니다. 접지 뒤 착지 그림은 별도 클립으로 연결합니다.
        private const float _dropStartTime = 0.3f, _dropFallTime = 0.6f;
        private const int _dropBlendMilliseconds = 200;
        private int _dropElapsedMilliseconds = -1;
        private SubscriptionScope _cueScope;
        protected override void OnEnable()
        {
            base.OnEnable();
            if (_player != null)
            {
                var player = _player;
                var scope = _cueScope = new SubscriptionScope();
                System.Action<string> cue = scope.Guard<string>(OnPresentationCue);
                scope.Add(() => player.PresentationCue -= cue); player.PresentationCue += cue;
            }
        }
        private void OnDisable()
        {
            var scope = _cueScope; _cueScope = null; scope?.Dispose();
            _executionAnimation = null;
            _action = null; _praying = _saveRising = false; _onLadder = _wasInAir = false; _ladderDirection = 0; _nonCombatArea = false; _dropElapsedMilliseconds = -1;
            if (Renderer != null)
            {
                Renderer.transform.localScale = _baseScale;
                Renderer.transform.localPosition = _basePosition;
            }
        }
        private void OnPresentationCue(string cue)
        {
            if (cue == "Parry" || cue == "ActionExit" || cue == "ClimbExit") { _action = null; _praying = _saveRising = false; RestartAnimation(); return; }
            if (cue == "Pray") { _action = _save; _praying = true; _saveRising = false; _actionElapsedMilliseconds = 0; _actionClock = default; RestartAnimation(); return; }
            if (cue == "LadderExit") { _onLadder = false; _ladderDirection = 0; return; }
            if (cue == "DropThrough") { _dropElapsedMilliseconds = 0; _action = null; RestartAnimation(); return; }
            if (cue == "Climb") { _climbHandAnchor = LedgeHandPosition; _action = null; RestartAnimation(); return; }
            if (cue == "LadderUp" || cue == "LadderDown" || cue == "LadderHold")
            {
                if (!_onLadder) RestartAnimation();
                _onLadder = true; _ladderDirection = cue == "LadderUp" ? 1 : cue == "LadderDown" ? -1 : 0;
                _action = null;
                return;
            }
            _praying = _saveRising = false;
            _action = cue == "Save" ? _save : null;
            _actionElapsedMilliseconds = 0; _actionClock = default;
            if (_action != null) RestartAnimation();
        }
        protected override void LateUpdate()
        {
            int milliseconds = _actionClock.AdvanceFrame(Time.deltaTime);
            var actor = _player != null ? _player.Actor : null;
            bool inAir = actor != null && !actor.Locomotion.IsGrounded && !actor.IsHanging && !actor.IsDead;
            if (_wasInAir && actor != null && actor.Locomotion.IsGrounded && _action == null && !actor.IsParrying &&
                (actor.CurrentState == StateKey.Idle || actor.CurrentState == StateKey.Walk))
            {
                // 짧은 점프는 코어의 큰 낙하 경직을 쓰지 않습니다. 착지 그림만 연결해 입력/물리는 유지합니다.
                _action = base.SelectAnimation(StateKey.Land);
                _actionElapsedMilliseconds = 0; _actionClock = default;
                if (_action != null) RestartAnimation();
            }
            _wasInAir = inAir;
            bool hanging = actor != null && actor.IsHanging;
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
            if (hanging && _onLadder && Renderer != null)
            {
                // 사다리 축과 몸통을 맞춥니다. 원본 셀 내부 손발 동작은 유지합니다.
                Vector3 ladderPosition = Renderer.transform.localPosition;
                ladderPosition.x = _basePosition.x - .12f * _baseScale.x;
                Renderer.transform.localPosition = ladderPosition;
            }
            if (hanging && !_onLadder && Renderer != null && Renderer.sprite != null)
            {
                Vector2 hand = HandAnchor(Renderer.sprite);
                Vector3 handWorld = Renderer.transform.TransformPoint(new Vector3(Renderer.flipX ? -hand.x : hand.x, hand.y, 0f));
                bool climbing = _player.ClimbProgress >= 0f;
                float linear = climbing ? Mathf.Clamp01(_player.ClimbProgress) : 0f;
                float grip = climbing ? 1f - Mathf.SmoothStep(0f, 1f, linear / .5f) : 1f;
                Vector3 correction = (climbing ? _climbHandAnchor : LedgeHandPosition) - handWorld;
                Vector3 position = Renderer.transform.position;
                position.x += correction.x * grip; position.y += correction.y * grip;
                if (climbing)
                {
                    float contact = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((linear - .55f) / .2f));
                    float localY = _basePosition.y + (_standingBottom - PreviewPresentation.VisibleBottom(Renderer.sprite)) * _baseScale.y;
                    float groundedY = Renderer.transform.parent.TransformPoint(new Vector3(0f, localY, 0f)).y;
                    position.y = Mathf.Lerp(position.y, groundedY, contact);
                }
                Renderer.transform.position = position;
            }
            if (_action != null && !_praying && (_actionElapsedMilliseconds += Math.Min(milliseconds, int.MaxValue - _actionElapsedMilliseconds)) >= MillisecondClock.ToMilliseconds(_saveRising ? SaveRiseDuration : _action.length))
            { _action = null; _saveRising = false; }
            if (_dropElapsedMilliseconds >= 0) _dropElapsedMilliseconds = _player != null && _player.Actor != null && _player.Actor.CurrentState == StateKey.Jump ? _dropElapsedMilliseconds + Math.Min(milliseconds, int.MaxValue - _dropElapsedMilliseconds) : -1;
        }

        protected override AnimationClip SelectAnimation(StateKey state)
        {
            if (state == StateKey.Stun && _player.KnockdownAnimation != null) return _player.KnockdownAnimation;
            if (state == StateKey.Execute && _executionAnimation != null) return _executionAnimation;
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
            if (animation == _save && (_praying || _saveRising))
            { time = _praying ? SaveRiseDuration : Mathf.Max(0f, SaveRiseDuration - _actionElapsedMilliseconds / 1000f); return true; }
            var actor = _player.Actor;
            if (actor.IsAttacking || (state == StateKey.Skill1 && actor.CurrentAttack != null))
            {
                if (!actor.IsAirAttacking && TryMapAttackTime(state, animation, 0, actor.CurrentAttack, actor.AttackElapsed, false, out time)) return true;
                time = actor.AttackElapsed / (float)actor.CurrentAttack.TotalTime * animation.length;
                return true;
            }
            if (state == StateKey.Jump && _dropElapsedMilliseconds >= 0)
            {
                time = Mathf.Lerp(_dropStartTime, _dropFallTime, _dropElapsedMilliseconds / (float)_dropBlendMilliseconds);
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
                case StateKey.Stun: return actor.DownDuration / 1000f;
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
            bool flip = !_onLadder && (_sourceFacesLeft ? facing > 0 : facing < 0);
            if (Renderer.flipX != flip) Renderer.flipX = flip;
        }
    }
}
