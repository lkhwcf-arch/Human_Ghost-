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
        [SerializeField, InspectorName("발 기준 몸체")] private SpriteRenderer _ground = null;
        [SerializeField, InspectorName("원본이 왼쪽을 봄")] private bool _sourceFacesLeft = true;
        [SerializeField] private AnimationClip _airAttack = null;
        [SerializeField, InspectorName("패링 애니메이션")] private AnimationClip _parry = null;
        private AnimationClip _executionAnimation;
        public void SetExecutionAnimation(AnimationClip clip) => _executionAnimation = clip;
        [SerializeField] private AnimationClip _walk = null;
        // 발이 바닥에 닿는 클립 시간(ms). 표시 프레임이 이 시간에 들어서면 발소리를 냅니다(10fps 원화 기준 3·7칸 / 5·11칸).
        [SerializeField, InspectorName("달리기 발 닿는 시간 (ms)")] private int[] _runSteps = { 300, 700 };
        [SerializeField, InspectorName("걷기 발 닿는 시간 (ms)")] private int[] _walkSteps = { 500, 1100 };
        public event Action<bool> Footstep;
        private AnimationClip _stepClip;
        private int _stepFrame = -1;
        private bool _nonCombatArea;
        public void SetNonCombatArea(bool value) { _nonCombatArea = value; RestartAnimation(); }
        [SerializeField] private AnimationClip _climb = null;
        [SerializeField] private AnimationClip _save = null;
        [SerializeField, InspectorName("장부방 입장 애니메이션")] private AnimationClip _walkIn = null;
        [SerializeField] private AnimationClip _ladderClimb = null;
        // [정수화 미적용 이유] 플레이어 원화 Transform 크기·위치·손 기준점 표시 보정값입니다. 판정에 쓰지 않습니다.
        [SerializeField, InspectorName("사다리 표시 배율"), Range(1f, 1.5f)] private float _ladderScale = 1.1f;
        // 오르기 원화는 앞 14칸(0~1.4초)이 매달리기 원화와 같은 작은 비율, 15칸부터 서기 원화 비율입니다.
        // [정수화 미적용 이유] 플레이어 원화 Transform 크기·위치·손 기준점 표시 보정값입니다. 판정에 쓰지 않습니다.
        [SerializeField, InspectorName("오르기 원화 크기 전환 구간 (진행률)")] private Vector2 _climbFullSizeRange = new Vector2(.75f, .78f);
        [SerializeField, Range(1f, 2f)] private float _hangScale = 1.25f;
        [SerializeField, Range(-0.5f, 0f)] private float _hangVerticalOffset = -0.1f;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _basePosition;
        private float _hangTop;
        private float _standingBottom;
        private Vector2 _ledgeHalfSize = new Vector2(.3f, .65f);
        [Serializable] private sealed class TraversalAnchor
        {
            [SerializeField] private Sprite _sprite = null;
            public Sprite Sprite => _sprite;
            // [정수화 미적용 이유] 플레이어 원화 Transform 크기·위치·손 기준점 표시 보정값입니다. 판정에 쓰지 않습니다.
            [SerializeField] private Vector2 _hand = Vector2.zero;
            public Vector2 Hand => _hand;
        }
        [SerializeField] private TraversalAnchor[] _traversalAnchors = Array.Empty<TraversalAnchor>();
        private bool _wasOnLedge;
        private int _ledgeExitRemaining;
        // [정수화 미적용 이유] 플레이어 원화 Transform 크기·위치·손 기준점 표시 보정값입니다. 판정에 쓰지 않습니다.
        private Vector3 _ledgeExitOffset;
        private const int _ledgeExitBlendMilliseconds = 120;
        // [정수화 미적용 이유] 플레이어 원화 Transform 크기·위치·손 기준점 표시 보정값입니다. 판정에 쓰지 않습니다.
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
        // [정수화 미적용 이유] 플레이어 원화 Transform 크기·위치·손 기준점 표시 보정값입니다. 판정에 쓰지 않습니다.
        private Vector3 LedgeHandPosition => _player.transform.position + new Vector3(_player.Actor.Facing * _ledgeHalfSize.x, _ledgeHalfSize.y, 0f);
        protected override bool AlignFeet => false;
        protected override void Awake()
        {
            base.Awake();
            if (Renderer == null) return;
            foreach (var anchor in _traversalAnchors)
                if (anchor != null && anchor.Sprite != null) _handAnchors[anchor.Sprite] = anchor.Hand;
            _baseScale = Renderer.transform.localScale;
            _basePosition = Renderer.transform.localPosition;
            Sprite hang = FindFrame(StateKey.Hang);
            if (hang != null)
                foreach (Vector2 vertex in hang.vertices) _hangTop = Mathf.Max(_hangTop, vertex.y);
            var idle = base.SelectAnimation(StateKey.Idle);
            if (idle == null || Fallback == null) return;
            float feet = _ground != null
                ? Renderer.transform.parent.InverseTransformPoint(new Vector3(Renderer.transform.position.x, _ground.bounds.min.y, Renderer.transform.position.z)).y
                : Renderer.transform.localPosition.y + PreviewPresentation.VisibleBottom(Fallback) * _baseScale.y;
            idle.SampleAnimation(Renderer.gameObject, 0f);
            if (Renderer.sprite == null) return;
            Vector3 position = Renderer.transform.localPosition;
            _standingBottom = ContactBottom(Renderer.sprite);
            position.y = feet - _standingBottom * _baseScale.y;
            Renderer.transform.localPosition = _basePosition = position;
        }
        private bool _onLadder, _wasInAir;
        private int _ladderDirection;
        // [정수화 미적용 이유] Unity 프레임 초를 넘기는 경계이며 받는 쪽이 MillisecondClock으로 정수 ms로 바꿉니다.
        protected override float AnimationDelta => _onLadder ? Time.deltaTime * _ladderDirection : base.AnimationDelta;
        private AnimationClip _action;
        private int _actionElapsedMilliseconds;
        private MillisecondClock _actionClock;
        private bool _praying, _saveRising;
        // 장부방 입장 그림은 끝나도 마지막 프레임을 유지합니다. 로딩 화면이 덮을 때까지 대기 자세로 돌아가지 않게 합니다.
        private bool _walkingIn;
        // 저장 뒤 일어서기 그림의 최대 길이(ms)입니다. 클립이 더 짧으면 클립 길이를 씁니다.
        private const int _saveRiseMaxMilliseconds = 600;
        private int SaveRiseMilliseconds => _save == null ? 0 : Math.Min(_saveRiseMaxMilliseconds, MillisecondClock.ToMilliseconds(_save.length));
        // 일어서기 그림 길이(정수 ms)를 돌려줍니다. 조립자가 입력 잠금 시간으로 씁니다.
        public int BeginSaveRise()
        {
            _praying = false; _saveRising = _save != null; _action = _save; _actionElapsedMilliseconds = 0; _actionClock = default;
            RestartAnimation();
            return SaveRiseMilliseconds;
        }
        // 장부방 입장 그림 길이(정수 ms)를 돌려줍니다. 클립이 없으면 0이며 조립자는 바로 이동합니다.
        public int BeginWalkIn()
        {
            if (_walkIn == null) return 0;
            _praying = _saveRising = false; _walkingIn = true; _action = _walkIn; _actionElapsedMilliseconds = 0; _actionClock = default;
            RestartAnimation();
            return MillisecondClock.ToMilliseconds(_walkIn.length);
        }
        // 이동이 취소되면(사망 등) 입장 그림을 끝내고 원래 상태 그림으로 돌아갑니다.
        public void EndWalkIn()
        {
            if (!_walkingIn) return;
            _walkingIn = false; _action = null;
            RestartAnimation();
        }
        // 아래 점프는 도약 준비를 건너뛰고 공중 자세를 유지합니다. 접지 뒤 착지 그림은 별도 클립으로 연결합니다.
        // [정수화 미적용 이유] Drop 클립의 샘플링 시각(초, AnimationClip API)입니다. 경과 시간은 정수 ms로 누적합니다.
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
            _wasOnLedge = false; _ledgeExitRemaining = 0;
            _action = null; _praying = _saveRising = _walkingIn = false; _onLadder = _wasInAir = false; _ladderDirection = 0; _nonCombatArea = false; _dropElapsedMilliseconds = -1;
            if (Renderer != null)
            {
                Renderer.transform.localScale = _baseScale;
                Renderer.transform.localPosition = _basePosition;
            }
        }
        private void OnPresentationCue(string cue)
        {
            _walkingIn = false;
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
            Vector3 previousPosition = Renderer != null ? Renderer.transform.localPosition : Vector3.zero;
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
            // 원화별 크기를 구분하고 오르기 중 배율은 고정합니다. 높이·손의 이동만 부드럽게 연결합니다.
            // 표현 경계: 정수 진행률(분모 10000)을 화면 보간용 0~1 실수로 바꿉니다. -1(미사용)은 0으로 고정됩니다.
            float climbProgress = _player.ClimbProgress / 10000f;
            float progress = hanging ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(climbProgress)) : 0f;
            // 오르기 앞부분 원화는 매달리기와 같이 작게 그려져 있어 같은 배율을 유지하고, 서기 비율 원화로 바뀌는 칸에서 1로 돌아옵니다.
            float scale = hanging && _player.ClimbProgress >= 0
                ? Mathf.Lerp(_hangScale, 1f, Mathf.InverseLerp(_climbFullSizeRange.x, _climbFullSizeRange.y, climbProgress))
                : hanging ? (_onLadder ? _ladderScale : _hangScale) : 1f;
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
            UpdateFootstep(actor);
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
                bool climbing = _player.ClimbProgress >= 0;
                float linear = climbing ? Mathf.Clamp01(_player.ClimbProgress / 10000f) : 0f;
                float grip = climbing ? 1f - Mathf.SmoothStep(0f, 1f, linear / .5f) : 1f;
                Vector3 correction = (climbing ? _climbHandAnchor : LedgeHandPosition) - handWorld;
                Vector3 position = Renderer.transform.position;
                position.x += correction.x * grip; position.y += correction.y * grip;
                if (climbing)
                {
                    float contact = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((linear - .55f) / .2f));
                    float localY = _basePosition.y + _standingBottom * _baseScale.y - ContactBottom(Renderer.sprite) * Renderer.transform.localScale.y;
                    float groundedY = Renderer.transform.parent.TransformPoint(new Vector3(0f, localY, 0f)).y;
                    position.y = Mathf.Lerp(position.y, groundedY, contact);
                }
                Renderer.transform.position = position;
            }
            bool onLedge = hanging && !_onLadder;
            if (Renderer != null)
            {
                bool canBlendExit = actor != null && (actor.CurrentState == StateKey.Jump || actor.CurrentState == StateKey.Idle || actor.CurrentState == StateKey.Walk);
                if (_wasOnLedge && !onLedge && canBlendExit)
                {
                    _ledgeExitOffset = previousPosition - Renderer.transform.localPosition;
                    _ledgeExitRemaining = _ledgeExitBlendMilliseconds;
                }
                if (onLedge || !canBlendExit) _ledgeExitRemaining = 0;
                if (_ledgeExitRemaining > 0)
                {
                    _ledgeExitRemaining = Math.Max(0, _ledgeExitRemaining - milliseconds);
                    float blend = Mathf.SmoothStep(0f, 1f, _ledgeExitRemaining / (float)_ledgeExitBlendMilliseconds);
                    Renderer.transform.localPosition += _ledgeExitOffset * blend;
                }
            }
            _wasOnLedge = onLedge;
            if (_action != null && !_praying && !_walkingIn && (_actionElapsedMilliseconds += Math.Min(milliseconds, int.MaxValue - _actionElapsedMilliseconds)) >= (_saveRising ? SaveRiseMilliseconds : MillisecondClock.ToMilliseconds(_action.length)))
            { _action = null; _saveRising = false; }
            if (_dropElapsedMilliseconds >= 0) _dropElapsedMilliseconds = _player != null && _player.Actor != null && _player.Actor.CurrentState == StateKey.Jump ? _dropElapsedMilliseconds + Math.Min(milliseconds, int.MaxValue - _dropElapsedMilliseconds) : -1;
        }

        protected override AnimationClip SelectAnimation(StateKey state)
        {
            if (state == StateKey.Stun && _player.KnockdownAnimation != null) return _player.KnockdownAnimation;
            if (state == StateKey.Execute && _executionAnimation != null) return _executionAnimation;
            if (_player.Actor.IsParrying) return _parry;
            if (state == StateKey.Hang && _player.ClimbProgress >= 0 && _climb != null) return _climb;
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
            if (state == StateKey.Hang && _player.ClimbProgress >= 0 && animation == _climb)
            {
                // 표현 경계: 정수 진행률(분모 10000)을 클립 샘플링 초로 바꿉니다.
                time = _player.ClimbProgress / 10000f * animation.length;
                return true;
            }
            if (animation == _save && (_praying || _saveRising))
            {
                // 표현 경계: 정수 ms 남은 시간을 클립 샘플링 초로 바꿉니다(되감기 재생).
                time = (_praying ? SaveRiseMilliseconds : Math.Max(0, SaveRiseMilliseconds - _actionElapsedMilliseconds)) / 1000f;
                return true;
            }
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
        protected override int DurationMilliseconds(StateKey state)
        {
            if (_action != null) return 0;
            var actor = _player.Actor;
            if ((actor.IsAttacking || state == StateKey.Skill1) && actor.CurrentAttack != null) return actor.CurrentAttack.TotalTime;
            switch (state)
            {
                case StateKey.Stun: return actor.DownDuration;
                case StateKey.Hit: return actor.Definition.HitStunTime;
                case StateKey.CritHit: return actor.Definition.CritHitStunTime;
                case StateKey.Roll: return actor.Definition.RollTime;
                case StateKey.Land: return actor.Definition.LandTime;
                default: return 0;
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

        // 걷기 상태로 땅 위에서 반복 클립이 재생될 때만 발 닿는 프레임 진입을 알립니다. true = 달리기.
        private void UpdateFootstep(PlayerActor actor)
        {
            AnimationClip clip = CurrentAnimation;
            bool stepping = actor != null && actor.CurrentState == StateKey.Walk && actor.Locomotion.IsGrounded && !_onLadder && _action == null && !actor.IsParrying && clip != null;
            if (!stepping || clip != _stepClip) { _stepClip = stepping ? clip : null; _stepFrame = -1; if (!stepping) return; }
            int frame = Mathf.FloorToInt(AnimationTime * clip.frameRate);
            int previous = _stepFrame; _stepFrame = frame;
            bool run = clip != _walk;
            if (BossPhaseTwoView.EntersStep(previous, frame, run ? _runSteps : _walkSteps, clip.frameRate)) Footstep?.Invoke(run);
        }

        protected override void UpdateFacing()
        {
            int facing = _player.Actor.Facing;
            bool flip = !_onLadder && (_sourceFacesLeft ? facing > 0 : facing < 0);
            if (Renderer.flipX != flip) Renderer.flipX = flip;
        }
    }
}
