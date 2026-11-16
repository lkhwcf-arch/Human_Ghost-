using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 일반몹 한 개체의 임시 관측·이동·벽·타격 연결입니다. 운영 물리/스폰 정책은 소유하지 않습니다.
    internal sealed class PreviewMonsterDriver : IDisposable
    {
        private readonly MonsterRuntime _runtime;
        private readonly MonsterActor _monster;
        private readonly PlayerRuntime _player;
        private readonly SpriteRenderer _playerBody;
        private readonly SpriteRenderer _attackWindow;
        private readonly SpriteRenderer _body;
        private readonly SpriteRenderer _sprite;
        private readonly Color _color;
        private readonly int _targetId;
        private readonly int _playerTargetId;
        private readonly float _worldUnitsPerTile;
        private readonly long _spawnX;
        private readonly IntegerMotion _motion = new IntegerMotion();
        private readonly SubscriptionScope _scope = new SubscriptionScope();
        private readonly Action<int, StateKey, int> _applyHitEffects;
        private MillisecondClock _clock;
        private PreviewThrownProjectile _projectile;
        private readonly Sprite _projectileSprite;
        private readonly MonsterThrowRigView _throwRig;
        private PreviewGroundFireView _fire;
        private bool _fireChecked;
        private bool _deathShown;
        public bool NeedsStep => !_scope.IsDisposed && _runtime != null && _runtime.isActiveAndEnabled &&
            (!_monster.IsDead || !_deathShown || !_fireChecked || (_fire != null && _fire.IsActive) ||
            (_sprite != null && _sprite.color != Color.gray) || (_body != null && _body.color != Color.gray));
        public SpriteRenderer Sprite => _sprite != null ? _sprite : _body;
        public Vector3 Position => _runtime.transform.position;
        public bool IsEngaged { get; private set; }
        public bool IsDead => _monster.IsDead;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string ReadFeedbackQa()
        {
            if (_scope.IsDisposed || _runtime == null || _player == null) return "몬스터 관측 수명이 끝났습니다.";
            return _runtime.name + ": HP " + _monster.Health + "/" + _monster.Definition.HP +
                " | 체세 " + _monster.Poise + " | 어그로 " + _monster.HasAggro +
                " | 대상 유효 " + CanEngageTarget() +
                " | 가로 거리 " + IntegerPosition.ToData(Math.Abs(_player.Position.X - _runtime.Position.X)) +
                " | 발 높이 차이 " + IntegerPosition.ToData(FootHeightOffset()) +
                " | 정면/후면 " + _runtime.Aggro.FrontRange + "/" + _runtime.Aggro.BackRange +
                " | 보행 구간 " + _spanMin.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "~" +
                _spanMax.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }
#endif
        // 죽으면 불을 남기는 개체를 평소에도 구분합니다. 전용 그림 공급 뒤 지웁니다.
        private static readonly Color _burningTint = new Color(1f, 0.65f, 0.45f);
        private bool _hasSpan;
        private float _spanMin, _spanMax;
        private Func<Vector2?> _protectedLedge;
        private readonly PreviewCollision _collision;
        public void UseLedgeProtection(Func<Vector2?> protectedLedge) => _protectedLedge = protectedLedge;

        // 조립된 바닥에서 걸을 수 있는 구간입니다. 경사 지면을 포함하며 끝에 닿으면 벽처럼 다룹니다.
        public void UseWalkableSpan(float min, float max)
        { _hasSpan = true; _spanMin = min; _spanMax = max; }

        private bool CanEngageTarget()
        {
            if (_scope.IsDisposed || _runtime == null || !_runtime.isActiveAndEnabled || _player == null ||
                !_player.isActiveAndEnabled || _playerBody == null || _body == null) return false;
            // 객체 수명만 확인합니다. 거리·높이·해제·복귀 판단은 데이터 기반 코어가 소유합니다.
            return _player.Actor != null && !_player.Actor.IsDead && !_monster.IsDead;
        }
        private void ObserveTarget()
        {
            if (!CanEngageTarget())
            { _monster.ClearTarget(); IsEngaged = false; _projectile?.Stop(); return; }
            IntegerPosition target = _player.Position, own = _runtime.Position;
            long offset = target.X - own.X;
            long height = FootHeightOffset();
            _monster.Observe(_playerTargetId, IntegerPosition.ToData(Math.Abs(offset)), Math.Sign(offset),
                IntegerPosition.ToData(Math.Abs(height)),
                IntegerPosition.ToData(Math.Abs(own.X - _spawnX)), Math.Sign(_spawnX - own.X), true, Math.Sign(height));
            IsEngaged = !_monster.IsDead && _monster.HasAggro;
        }

        // 발 기준 물리 관측만 Unity 경계에서 정수화합니다. 그림 프레임이나 중심 높이 차이를 층 차이로 쓰지 않습니다.
        private long FootHeightOffset() => _player.ToInteger(_playerBody.bounds.min.y) - _runtime.ToInteger(_body.bounds.min.y);

        // 구간·복귀 제한까지 반영된 실제 이동 거리로 경사 높이를 따라갑니다. 평지·낭떠러지에서는 높이를 바꾸지 않습니다.
        // Unity 물리 관측은 경계에서만 실수로 쓰고, 결과 높이 차이를 정수 좌표로 반영합니다.
        // 요청 거리 그대로 이동했으면 이동 전 조회 결과를 재사용해 프레임당 물리 조회를 한 번으로 유지합니다.
        private void FollowGroundSlope(IntegerPosition before, Vector3 center, long requestedDelta, bool slopeAhead, float slopeHeight)
        {
            IntegerPosition after = _runtime.Position;
            long movedDelta = after.X - before.X;
            if (movedDelta == 0) return;
            float height = slopeHeight;
            bool onSlope = movedDelta == requestedDelta ? slopeAhead
                : _collision.TryFollowGroundSlope(center, _runtime.ToWorld(movedDelta), out height);
            if (!onSlope) return;
            long rise = _runtime.ToInteger(height) - _runtime.ToInteger(center.y);
            if (rise != 0) _runtime.SetPosition(new IntegerPosition(after.X, checked(before.Y + rise)));
        }

        // targetId: 이 몬스터의 장면 내 양수 개체 ID, playerTargetId: 플레이어의 개체 ID(데이터 ID와 별개).
        public PreviewMonsterDriver(MonsterRuntime runtime, PlayerRuntime player, SpriteRenderer playerBody, SpriteRenderer attackWindow,
            int targetId, int playerTargetId, float worldUnitsPerTile, Action<int, StateKey, int> applyHitEffects = null)
        {
            _runtime = runtime != null ? runtime : throw new ArgumentNullException(nameof(runtime));
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _playerBody = playerBody != null ? playerBody : throw new ArgumentNullException(nameof(playerBody));
            _attackWindow = attackWindow != null ? attackWindow : throw new ArgumentNullException(nameof(attackWindow));
            if (targetId <= 0 || playerTargetId <= 0 || targetId == playerTargetId) throw new ArgumentOutOfRangeException(nameof(targetId));
            runtime.ConfigureSpatialScale(worldUnitsPerTile);
            _monster = runtime.Actor ?? throw new InvalidOperationException("일반몹 초기화가 필요합니다.");
            _spawnX = runtime.Position.X;
            _targetId = targetId; _playerTargetId = playerTargetId; _worldUnitsPerTile = worldUnitsPerTile;
            _applyHitEffects = applyHitEffects;
            // 매복 몬스터는 꺼진 채 연결합니다.
            _body = runtime.GetComponentInChildren<SpriteRenderer>(true);
            if (_body == null) throw new InvalidOperationException("일반몹 임시 사각형이 없습니다.");
            _sprite = runtime.transform.Find("PreviewSprite")?.GetComponent<SpriteRenderer>();
            _projectileSprite = runtime.GetComponent<PreviewMonsterSprite>()?.ProjectileSprite;
            _throwRig = runtime.GetComponent<MonsterThrowRigView>();
            _color = _body.color;
            _collision = new PreviewCollision(true, 0f, _body.bounds.extents);
            // 판정 창이 한 프레임 안에 열리고 닫혀도 열리는 순간 한 번은 접촉을 확인합니다.
            Action<bool> attackChanged = _scope.Guard<bool>(OnAttackWindowChanged);
            _scope.Add(() => _monster.AttackWindowChanged -= attackChanged);
            _monster.AttackWindowChanged += attackChanged;
        }

        public void Dispose()
        {
            if (_scope.IsDisposed) return;
            IsEngaged = false;
            SubscriptionScope.Cleanup(_scope.Dispose, () => _projectile?.Destroy(), () => _fire?.Destroy());
        }

        public void Step(float seconds, float minX, float maxX)
        {
            if (!NeedsStep) return;
            if (_deathShown)
            {
                // 마지막 타격의 섬광이 끝난 뒤에도 사망 색이 복구되어야 합니다.
                if (_sprite != null && _sprite.color != Color.gray) _sprite.color = Color.gray;
                if (_body.color != Color.gray) _body.color = Color.gray;
                StepFire(seconds);
                return;
            }
            if (_sprite != null)
            {
                _sprite.flipX = _monster.Facing > 0;
                _sprite.color = _monster.IsDead ? Color.gray : _monster.CurrentState == StateKey.Stun ? new Color(0.75f,0.35f,0.95f) :
                    _monster.IsAttackActive ? new Color(1f,0.3f,0.3f) : _runtime.Patterns.LeavesDeathFire ? _burningTint : Color.white;
            }
            StepFire(seconds);
            if (_monster.IsDead) { IsEngaged = false; _body.color = Color.gray; _deathShown = true; return; }
            ObserveTarget();
            if (_monster.IsExposed || _monster.IsInvulnerable) return;
            _projectile?.Step(seconds);
            PlayerActor actor = _player.Actor;
            int milliseconds = _clock.AdvanceFrame(seconds);
            if (_hasSpan) { minX = _spanMin; maxX = _spanMax; }
            // 턱을 차지한 플레이어가 안전하게 올라올 한 칸을 확보합니다.
            Vector2? ledge = _protectedLedge?.Invoke();
            // 경사를 따라 높이가 바뀌므로 최초 바닥이 아닌 현재 발 높이와 비교합니다.
            if (ledge.HasValue && _hasSpan && Mathf.Abs(ledge.Value.y - _body.bounds.min.y) < .2f * _worldUnitsPerTile)
            {
                float clearance = _worldUnitsPerTile + _body.bounds.extents.x;
                if (_runtime.transform.position.x >= ledge.Value.x)
                    minX = Mathf.Max(minX, Mathf.Min(_runtime.transform.position.x, ledge.Value.x + clearance));
                else maxX = Mathf.Min(maxX, Mathf.Max(_runtime.transform.position.x, ledge.Value.x - clearance));
            }
            long delta = _motion.StepHorizontal(_monster.Velocity, milliseconds);
            float requested = _runtime.ToWorld(delta);
            // 플레이어 이동과 같은 경사 판정을 재사용합니다. 경사 면은 벽으로 막지 않습니다.
            IntegerPosition before = _runtime.Position;
            Vector3 center = _body.bounds.center;
            long requestedDelta = delta;
            bool slopeAhead = _collision.TryFollowGroundSlope(center, requested, out float slopeHeight);
            float allowed = slopeAhead ? requested : _collision.MoveHorizontal(center, requested);
            bool blocked = allowed != requested;
            if (blocked) delta = _runtime.ToInteger(allowed);
            if (_monster.IsReturning)
            {
                long remaining = _spawnX - _runtime.Position.X;
                if (Math.Sign(delta) == Math.Sign(remaining) && Math.Abs(delta) > Math.Abs(remaining)) delta = remaining;
            }
            bool wall = _runtime.MoveHorizontal(delta, minX, maxX) || blocked;
            FollowGroundSlope(before, center, requestedDelta, slopeAhead, slopeHeight);
            if (wall) _motion.ResetHorizontal();
            Vector3 position = _runtime.ToWorld(_runtime.Position);
            if (wall) _monster.NotifyWallHit();

            ObserveTarget();

            CheckMonsterHit();
            _body.color = _monster.IsDead ? Color.gray : _monster.CurrentState == StateKey.Stun ? new Color(0.75f, 0.35f, 0.95f) :
                _monster.IsAttackActive ? new Color(1f, 0.3f, 0.3f) : _color;
        }

        // 플레이어 판정 창과 이 몬스터의 접촉. GameplayPreview가 창 열림 순간과 매 프레임 호출합니다. 중복은 창 등록이 막습니다.
        public event System.Action PlayerDodged;

        public HitOutcome CheckPlayerHit()
        {
            if (_scope.IsDisposed || _player == null || _runtime == null || _body == null || _attackWindow == null ||
                !_player.isActiveAndEnabled) return HitOutcome.None;
            PlayerActor actor = _player.Actor;
            if (!_runtime.isActiveAndEnabled || _monster.IsDead || !actor.IsAttackActive) return HitOutcome.None;
            return _attackWindow.bounds.Intersects(_body.bounds) ? actor.TryHitMonster(_targetId, _monster, _player.HitRandom) : HitOutcome.None;
        }

        private void OnAttackWindowChanged(bool active)
        {
            if (active && CanEngageTarget() && _monster.CurrentPattern != null && _monster.CurrentPattern.IsProjectile) StartProjectile();
            if (!active) _projectile?.Stop();
            if (active) CheckMonsterHit();
        }

        // 짐 투척(C08): 판정 창 동안 던진 순간의 플레이어 위치로 포물선을 그립니다.
        private void StartProjectile()
        {
            if (_projectile == null)
            {
                var view = CreateView("PreviewProjectile", new Color(0.45f, 0.3f, 0.15f), new Vector3(0.5f, 0.5f, 1f));
                _projectile = new PreviewThrownProjectile(view, _worldUnitsPerTile);
                if (_projectileSprite != null)
                {
                    view.sprite = _projectileSprite;
                    view.color = Color.white;
                    view.transform.localScale = Vector3.one * (0.5f / Mathf.Max(_projectileSprite.bounds.size.x, _projectileSprite.bounds.size.y));
                }
            }
            _projectile.Throw(_throwRig != null ? _throwRig.ProjectilePosition : _runtime.transform.position, _player.transform.position, _monster.CurrentPattern.Attack.Active);
        }

        // 불붙은 허수귀(C08): 죽은 자리에 불을 남기고 범위 안 플레이어에게 초당 고정 피해를 줍니다.
        private void StepFire(float seconds)
        {
            if (_monster.IsDead && !_fireChecked)
            {
                _fireChecked = true;
                GroundFire fire = _monster.WasExecuted ? null : _runtime.Patterns.CreateDeathFire();
                if (fire != null)
                {
                    float width = _runtime.Patterns.DeathFireHalfWidth / 50f * _worldUnitsPerTile;
                    // 피해 범위가 눈에 보이도록 몸 아래쪽을 덮는 높이로 그립니다.
                    SpriteRenderer view = CreateView("PreviewGroundFire", new Color(1f, 0.45f, 0.1f, 0.8f), new Vector3(width, 0.6f, 1f));
                    view.transform.position = _runtime.transform.position + Vector3.down * (_body.bounds.extents.y - 0.3f);
                    _fire = new PreviewGroundFireView(fire, view);
                }
            }
            _fire?.Step(seconds, _player.Actor, _playerBody.bounds);
        }

        private SpriteRenderer CreateView(string name, Color color, Vector3 scale)
        {
            var view = new GameObject(name);
            view.SetActive(false);
            view.transform.localScale = scale;
            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = _body.sprite;
            renderer.color = color;
            renderer.sortingOrder = _body.sortingOrder + 1;
            view.SetActive(true);
            return renderer;
        }

        // 기존 접촉 판정의 Unity 경계 변환입니다. 인식 관측은 위의 정수 좌표를 사용합니다.
        private int Distance(float offset) => Mathf.RoundToInt(Mathf.Abs(offset) / _worldUnitsPerTile * 100f);

        // 임시 판정: 근접·잡기는 높이가 겹치고 앞쪽 거리 안, 돌진은 사각형 겹침. 회피/치명은 확률 경로를 씁니다.
        private void CheckMonsterHit()
        {
            PlayerActor actor = _player.Actor;
            if (!_runtime.isActiveAndEnabled || !_monster.IsAttackActive || !CanEngageTarget()) return;
            MonsterPattern pattern = _monster.CurrentPattern;
            float offset = _player.transform.position.x - _runtime.transform.position.x;
            Bounds own = _body.bounds, target = _playerBody.bounds;
            bool sameHeight = target.min.y <= own.max.y && target.max.y >= own.min.y;
            bool inFront = offset * _monster.Facing >= 0f;
            bool touching = pattern.IsProjectile ? _projectile != null && _projectile.IsFlying && _projectile.Bounds.Intersects(target) :
                pattern.IsCharge ? own.Intersects(target) : sameHeight && inFront && Distance(offset) <= pattern.Range;
            int direction = System.Math.Sign(_runtime.transform.position.x - _player.transform.position.x);
            if (!touching) return;
            HitOutcome outcome = _monster.TryHitPlayer(_playerTargetId, actor, _runtime.HitRandom, direction);
            if (outcome == HitOutcome.Dodged) PlayerDodged?.Invoke();
            else if ((outcome == HitOutcome.Hit || outcome == HitOutcome.Critical) && !actor.IsDead)
                _applyHitEffects?.Invoke(_runtime.MonsterId, pattern.Key, direction == 0 ? _monster.Facing : -direction);
        }

    }
}
