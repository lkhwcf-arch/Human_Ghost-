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
        // 높이 던지기(옹귀 1117): 던진 순간 기억한 바닥 자리와 그림자입니다.
        private Vector3 _lobTarget;
        private SpriteRenderer _lobShadow;
        private static UnityEngine.Sprite _shadowSprite;
        private const int _lobArc100 = 600;
        private const int _lobLandingMilliseconds = 100;
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
        public int MonsterId => _runtime.MonsterId;
        public bool IsEngaged { get; private set; }
        public bool IsDead => _monster.IsDead;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string ReadFeedbackQa()
        {
            if (_scope.IsDisposed || _runtime == null || _player == null) return "몬스터 관측 수명이 끝났습니다.";
            return _runtime.name + ": HP " + _monster.Health + "/" + _monster.Definition.HP +
                " | 체세 " + _monster.Poise + " | 어그로 " + _monster.HasAggro +
                " | 대상 유효 " + CanEngageTarget() +
                "\n    가로 거리 " + IntegerPosition.ToData(Math.Abs(_player.Position.X - _runtime.Position.X)) +
                " | 발 높이 차이 " + IntegerPosition.ToData(FootHeightOffset()) +
                " | 정면/후면 " + _runtime.Aggro.FrontRange + "/" + _runtime.Aggro.BackRange +
                "\n    보행 구간 " + IntegerPosition.ToData(_spanMin) + "~" + IntegerPosition.ToData(_spanMax) + "(1/100칸)";
        }
#endif
        // 죽으면 불을 남기는 개체를 평소에도 구분합니다. 전용 그림 공급 뒤 지웁니다.
        private static readonly Color _burningTint = new Color(1f, 0.65f, 0.45f);
        private bool _hasSpan;
        private long _spanMin, _spanMax;
        private Func<Vector2?> _protectedLedge;
        private readonly PreviewFootCollision _collision;
        // 턱 보호 판정 정수 값입니다(기존 0.2칸 높이 차·1칸 여유).
        private const long LedgeHeightTolerance = IntegerPosition.UnitsPerTile / 5;
        private const long LedgeClearance = IntegerPosition.UnitsPerTile;
        public void UseLedgeProtection(Func<Vector2?> protectedLedge) => _protectedLedge = protectedLedge;

        // 조립된 바닥에서 걸을 수 있는 구간(정수 X)입니다. 경사 지면을 포함하며 끝에 닿으면 벽처럼 다룹니다.
        public void UseWalkableSpan(long min, long max)
        {
            if (min > max) throw new ArgumentException("보행 구간의 최소값이 최대값보다 큽니다.");
            _hasSpan = true; _spanMin = min; _spanMax = max;
        }

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
            _monster.CanRetreat = CanRetreatFrom(-Math.Sign(offset));
            _monster.Observe(_playerTargetId, IntegerPosition.ToData(Math.Abs(offset)), Math.Sign(offset),
                IntegerPosition.ToData(Math.Abs(height)),
                IntegerPosition.ToData(Math.Abs(own.X - _spawnX)), Math.Sign(_spawnX - own.X), true, Math.Sign(height));
            IsEngaged = !_monster.IsDead && _monster.HasAggro;
        }

        // 발 기준 물리 관측만 Unity 경계에서 정수화합니다. 그림 프레임이나 중심 높이 차이를 층 차이로 쓰지 않습니다.
        private long FootHeightOffset() => _player.ToInteger(_playerBody.bounds.min.y) - _runtime.ToInteger(_body.bounds.min.y);

        // 구간·복귀 제한까지 반영된 실제 수평 이동량(정수)의 비탈 높이 변화를 정수로 돌려줍니다. 평지·낭떠러지는 0입니다.
        // 요청 거리 그대로면 이동 전 조회 결과를 재사용하고, 비탈에서 제한으로 줄었을 때만 다시 조회해 프레임당 조회를 최소로 둡니다.
        private long SlopeRise(long centerX, long centerY, long moved, long requestedDelta, bool slopeAhead, long slopeRise)
        {
            if (moved == 0 || !slopeAhead) return 0;
            if (moved == requestedDelta) return slopeRise;
            return _collision.TryFollowFootSlope(centerX, centerY, moved, out long rise) ? rise : 0;
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
            // 비탈 높이는 중앙 발로 잽니다. 피격·공격·어그로에 쓰는 Body 크기는 그대로입니다(비탈길 설계 4.2).
            _collision = new PreviewFootCollision(_body.bounds.extents, worldUnitsPerTile);
            // 판정 창이 한 프레임 안에 열리고 닫혀도 열리는 순간 한 번은 접촉을 확인합니다.
            Action<bool> attackChanged = _scope.Guard<bool>(OnAttackWindowChanged);
            _scope.Add(() => _monster.AttackWindowChanged -= attackChanged);
            _monster.AttackWindowChanged += attackChanged;
        }

        public void Dispose()
        {
            if (_scope.IsDisposed) return;
            IsEngaged = false;
            SubscriptionScope.Cleanup(_scope.Dispose, () => _projectile?.Destroy(), () => _fire?.Destroy(),
                () => { if (_lobShadow != null) UnityEngine.Object.Destroy(_lobShadow.gameObject); });
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
                    _monster.IsAttackActive && !_monster.IsRetreating ? new Color(1f,0.3f,0.3f) : _runtime.Patterns.LeavesDeathFire ? _burningTint : Color.white;
            }
            StepFire(seconds);
            if (_monster.IsDead) { IsEngaged = false; _body.color = Color.gray; _deathShown = true; return; }
            ObserveTarget();
            if (_monster.IsExposed || _monster.IsInvulnerable) return;
            _projectile?.Step(seconds);
            if (_lobShadow != null)
            {
                bool falling = _projectile != null && _projectile.IsFlying && _monster.CurrentPattern != null && _monster.CurrentPattern.IsLob;
                _lobShadow.enabled = falling;
                if (falling) _lobShadow.transform.localScale = new Vector3(1f, .3f, 1f) * _worldUnitsPerTile * Mathf.Lerp(.5f, 1f, _projectile.Progress);
            }
            PlayerActor actor = _player.Actor;
            int milliseconds = _clock.AdvanceFrame(seconds);
            IntegerPosition before = _runtime.Position;
            // [정수화 미적용 이유] minX·maxX는 GameplayPreview가 넘기는 맵 경계(float) 계약입니다. 들어온 즉시 정수로 바꿉니다.
            long minimum = _hasSpan ? _spanMin : _runtime.ToInteger(minX), maximum = _hasSpan ? _spanMax : _runtime.ToInteger(maxX);
            // Unity 경계: 몸통(Body) bounds는 읽는 즉시 정수로 바꿉니다. 이후 이동 계산은 모두 정수입니다.
            Bounds bodyBounds = _body.bounds;
            long centerX = _runtime.ToInteger(bodyBounds.center.x), centerY = _runtime.ToInteger(bodyBounds.center.y);
            long bodyBottom = _runtime.ToInteger(bodyBounds.min.y), bodyHalfWidth = _runtime.ToInteger(bodyBounds.extents.x);
            // 턱을 차지한 플레이어가 안전하게 올라올 한 칸을 확보합니다. 턱 위치(Vector2)는 받는 즉시 정수로 바꿉니다.
            Vector2? ledge = _protectedLedge?.Invoke();
            if (ledge.HasValue && _hasSpan)
            {
                long ledgeX = _runtime.ToInteger(ledge.Value.x), ledgeY = _runtime.ToInteger(ledge.Value.y);
                // 경사를 따라 높이가 바뀌므로 최초 바닥이 아닌 현재 발 높이와 비교합니다.
                if (Math.Abs(checked(ledgeY - bodyBottom)) < LedgeHeightTolerance)
                {
                    long clearance = checked(LedgeClearance + bodyHalfWidth);
                    if (before.X >= ledgeX) minimum = Math.Max(minimum, Math.Min(before.X, checked(ledgeX + clearance)));
                    else maximum = Math.Min(maximum, Math.Max(before.X, checked(ledgeX - clearance)));
                }
            }
            long delta = _motion.StepHorizontal(_monster.Velocity, milliseconds);
            // 공통 물리 어댑터의 중앙 발 비탈 조회입니다. 이동량·높이 변화는 정수이고 경사 판정은 코어 GroundSlopeRule입니다.
            long requestedDelta = delta;
            bool slopeAhead = _collision.TryFollowFootSlope(centerX, centerY, delta, out long slopeRise);
            bool blocked = false;
            if (!slopeAhead)
            {
                // 평지의 기존 벽 판정입니다(정수 API).
                long allowed = _collision.MoveHorizontal(centerX, centerY, delta);
                blocked = allowed != delta;
                delta = allowed;
            }
            if (_monster.IsReturning)
            {
                long remaining = _spawnX - before.X;
                if (Math.Sign(delta) == Math.Sign(remaining) && Math.Abs(delta) > Math.Abs(remaining)) delta = remaining;
            }
            long x = _runtime.ClampHorizontal(delta, minimum, maximum, out bool edge);
            bool wall = edge || blocked;
            // X와 비탈 높이를 모두 정한 뒤 한 번만 확정합니다. SpatialChanged 구독자가 높이가 맞지 않은 중간 위치를 보지 않습니다.
            _runtime.SetPosition(new IntegerPosition(x, checked(before.Y + SlopeRise(centerX, centerY, x - before.X, requestedDelta, slopeAhead, slopeRise))));
            if (wall) _motion.ResetHorizontal();
            Vector3 position = _runtime.ToWorld(_runtime.Position);
            if (wall) _monster.NotifyWallHit();

            ObserveTarget();

            CheckMonsterHit();
            _body.color = _monster.IsDead ? Color.gray : _monster.CurrentState == StateKey.Stun ? new Color(0.75f, 0.35f, 0.95f) :
                _monster.IsAttackActive && !_monster.IsRetreating ? new Color(1f, 0.3f, 0.3f) : _color;
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

        // 뒤로 깡총(옹귀 1117): 물러설 쪽 RetreatDistance 안에 벽·보행 구간 끝·바닥 없음이 있으면 뛰지 않습니다.
        private bool CanRetreatFrom(int direction)
        {
            if (direction == 0) return false;
            int distance = 0;
            foreach (MonsterPattern pattern in _runtime.Actor.Patterns) if (pattern.IsRetreat) distance = Math.Max(distance, pattern.RetreatDistance);
            if (distance == 0) return false;
            float delta = direction * distance / 100f * _worldUnitsPerTile;
            Vector3 center = _body.bounds.center;
            float x = _runtime.transform.position.x + delta;
            if (_hasSpan && (x < _spanMin || x > _spanMax)) return false;
            if (Mathf.Abs(_collision.MoveHorizontal(center, delta) - delta) > .01f) return false;
            // 보행 구간이 없는 곳(직접 배치)은 착지점 아래 바닥을 확인합니다.
            return _hasSpan || _collision.FindFloor(new Vector3(center.x + delta, center.y, center.z), _body.bounds.extents.y + .3f * _worldUnitsPerTile, out _);
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
            Vector3 from = _throwRig != null ? _throwRig.ProjectilePosition : _runtime.transform.position;
            if (!_monster.CurrentPattern.IsLob)
            {
                _projectile.Throw(from, _player.transform.position, _monster.CurrentPattern.Attack.Active);
                return;
            }
            // 높이 던지기: 던진 순간 막손이 서 있던 자리의 바닥(땅·발판 윗면)을 기억하고 그 자리에 그림자를 둡니다.
            Vector3 feet = new Vector3(_player.transform.position.x, _playerBody.bounds.min.y, _player.transform.position.z);
            Vector3 probe = new Vector3(feet.x, _playerBody.bounds.center.y, feet.z);
            _lobTarget = _collision.FindFloor(probe, 30f * _worldUnitsPerTile, out float gap)
                ? new Vector3(feet.x, probe.y - gap - _body.bounds.extents.y, feet.z) : feet;
            if (_lobShadow == null)
            {
                _lobShadow = CreateView("PreviewLobShadow", new Color(0f, 0f, 0f, .45f), Vector3.one);
                _lobShadow.sprite = ShadowSprite();
                _lobShadow.sortingOrder = _body.sortingOrder - 1;
            }
            _lobShadow.transform.position = _lobTarget;
            _lobShadow.enabled = true;
            _projectile.Throw(from, _lobTarget + Vector3.up * (.25f * _worldUnitsPerTile), _monster.CurrentPattern.Attack.Active, _lobArc100);
        }

        // 기존 원형 이펙트가 없어 런타임에 원 하나를 만듭니다(아트 공급 시 교체).
        private static UnityEngine.Sprite ShadowSprite()
        {
            if (_shadowSprite != null) return _shadowSprite;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + .5f - size / 2f, y + .5f - size / 2f).magnitude / (size / 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3f)));
                }
            texture.Apply();
            return _shadowSprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
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
            // 높이 던지기는 떨어지는 마지막 순간, 기억한 자리 가로 1칸에만 맞습니다.
            float half = .5f * _worldUnitsPerTile;
            bool touching = pattern.IsRetreat ? false :
                pattern.IsLob ? _projectile != null && _projectile.IsFlying &&
                    _projectile.Progress >= 1f - _lobLandingMilliseconds / (float)pattern.Attack.Active &&
                    target.max.x >= _lobTarget.x - half && target.min.x <= _lobTarget.x + half &&
                    target.min.y <= _lobTarget.y + _worldUnitsPerTile && target.max.y >= _lobTarget.y :
                pattern.IsProjectile ? _projectile != null && _projectile.IsFlying && _projectile.Bounds.Intersects(target) :
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
