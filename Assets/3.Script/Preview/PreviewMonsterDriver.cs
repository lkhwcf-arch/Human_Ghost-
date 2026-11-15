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
        // 죽으면 불을 남기는 개체를 평소에도 구분합니다. 전용 그림 공급 뒤 지웁니다.
        private static readonly Color _burningTint = new Color(1f, 0.65f, 0.45f);
        private bool _hasSpan;
        private float _spanMin, _spanMax, _spanY;
        private Camera _camera;
        private bool _playerOnSpan;

        // 조립된 바닥에서 걸을 수 있는 구간입니다. 끝에 닿으면 벽처럼 다룹니다.
        public void UseWalkableSpan(float min, float max, float floorY)
        { _hasSpan = true; _spanMin = min; _spanMax = max; _spanY = floorY; _playerOnSpan = false; }
        public void UsePerception(Camera camera) => _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));

        private bool CanEngageTarget()
        {
            if (_scope.IsDisposed || _runtime == null || !_runtime.isActiveAndEnabled || _player == null ||
                !_player.isActiveAndEnabled || _playerBody == null || _body == null) return false;
            PlayerActor actor = _player.Actor;
            if (actor.IsDead || actor.IsHanging || _player.ClimbProgress >= 0f)
            { _playerOnSpan = false; return false; }
            Bounds target = _playerBody.bounds;
            float floor = _hasSpan ? _spanY : _body.bounds.min.y;
            bool within = !_hasSpan || target.center.x >= _spanMin && target.center.x <= _spanMax;
            if (actor.Locomotion.IsGrounded)
                _playerOnSpan = within && Mathf.Abs(target.min.y - floor) <= .12f * _worldUnitsPerTile;
            else if (!within || target.min.y < floor - .12f * _worldUnitsPerTile)
                _playerOnSpan = false;
            // 같은 발판에서 제자리 점프는 유지하지만, 위층은 그곳에 실제 착지하기 전에는 인식하지 않습니다.
            return _playerOnSpan && InView(_body.bounds.center) && InView(target.center);
        }
        private bool InView(Vector3 point)
        {
            if (_camera == null || !_camera.isActiveAndEnabled) return false;
            Vector3 viewport = _camera.WorldToViewportPoint(point);
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }
        private void ObserveTarget()
        {
            if (_player.Actor.IsDead) { _monster.ClearTarget(); IsEngaged = false; return; }
            float offset = _player.transform.position.x - _runtime.transform.position.x;
            bool eligible = CanEngageTarget();
            _monster.Observe(_playerTargetId, Distance(offset), Math.Sign(offset),
                Distance(_player.transform.position.y - _runtime.transform.position.y),
                IntegerPosition.ToData(Math.Abs(_runtime.Position.X - _spawnX)), Math.Sign(_spawnX - _runtime.Position.X), eligible);
            if (!eligible) _projectile?.Stop();
            IsEngaged = !_monster.IsDead && _monster.HasAggro;
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
            _color = _body.color;
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
            int milliseconds = _clock.Advance(seconds);
            if (_hasSpan) { minX = _spanMin; maxX = _spanMax; }
            long delta = _motion.StepHorizontal(_monster.Velocity, milliseconds);
            if (_monster.IsReturning)
            {
                long remaining = _spawnX - _runtime.Position.X;
                if (Math.Sign(delta) == Math.Sign(remaining) && Math.Abs(delta) > Math.Abs(remaining)) delta = remaining;
            }
            bool wall = _runtime.MoveHorizontal(delta, minX, maxX);
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
                _projectile = new PreviewThrownProjectile(CreateView("PreviewProjectile", new Color(0.45f, 0.3f, 0.15f), new Vector3(0.5f, 0.5f, 1f)), _worldUnitsPerTile);
            _projectile.Throw(_runtime.transform.position, _player.transform.position, _monster.CurrentPattern.Attack.Active);
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

        private int Distance(float offset) => Mathf.RoundToInt(Mathf.Abs(offset) / _worldUnitsPerTile * 100f);
    }
}
