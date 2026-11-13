using System;
using HumanGhost.Gameplay.Effects;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 위치·생존·활성 수명을 효과 대상으로 연결합니다. 표시 오류 처리와 게임 명령을 섞지 않습니다.
    public class FieldEffectTargetBinding : IDisposable
    {
        private readonly SpatialRuntime _runtime;
        private readonly SpriteRenderer _body;
        private readonly Func<bool> _alive;
        private readonly Func<int, bool> _damage;
        private readonly string _immunities;
        private readonly Action<FieldEffectTargetBinding> _changed;
        private readonly SubscriptionScope _scope = new SubscriptionScope();
        private long _generation;
        public EffectTarget Target { get; private set; }
        public bool IsMonster { get; }
        public Bounds Bounds => _body != null ? _body.bounds : default;

        public FieldEffectTargetBinding(PlayerRuntime runtime, SpriteRenderer body, Action<FieldEffectTargetBinding> changed)
            : this(runtime, body, () => !runtime.Actor.IsDead, runtime.Actor.ReceiveFieldDamage, "", false, changed)
        {
            Action<int> health = _scope.Guard<int>(OnHealth);
            _scope.Add(() => runtime.Actor.HealthChanged -= health); runtime.Actor.HealthChanged += health;
        }
        public FieldEffectTargetBinding(MonsterRuntime runtime, SpriteRenderer body, Action<FieldEffectTargetBinding> changed)
            : this(runtime, body, () => !runtime.Actor.IsDead, runtime.Actor.ReceiveFieldDamage,
                runtime.Definition.EffectImmunities, true, changed)
        {
            Action<int> health = _scope.Guard<int>(OnHealth);
            _scope.Add(() => runtime.Actor.HealthChanged -= health); runtime.Actor.HealthChanged += health;
        }
        private FieldEffectTargetBinding(SpatialRuntime runtime, SpriteRenderer body, Func<bool> alive, Func<int, bool> damage,
            string immunities, bool isMonster, Action<FieldEffectTargetBinding> changed)
        {
            _runtime = runtime; _body = body; _alive = alive; _damage = damage; _immunities = immunities;
            IsMonster = isMonster; _changed = changed;
            Action spatial = _scope.Guard(Refresh);
            Action<bool> availability = _scope.Guard<bool>(OnAvailability);
            _scope.Add(() => runtime.SpatialChanged -= spatial); runtime.SpatialChanged += spatial;
            _scope.Add(() => runtime.AvailabilityChanged -= availability); runtime.AvailabilityChanged += availability;
            CreateTarget();
        }
        private void CreateTarget() => Target = new EffectTarget(_runtime.GetInstanceID(), ++_generation,
            () => !_scope.IsDisposed && _runtime != null && _runtime.isActiveAndEnabled && _body != null && _alive(),
            _damage, FieldEffectData.ReadImmunities(_immunities));
        private void OnHealth(int health)
        { try { if (health == 0) Target.Dispose(); } finally { Refresh(); } }
        private void OnAvailability(bool available)
        {
            if (!available) { try { Target.Dispose(); } finally { Refresh(); } }
            else { CreateTarget(); Refresh(); }
        }
        public void Refresh() { if (!_scope.IsDisposed) _changed(this); }
        public void Dispose() => SubscriptionScope.Cleanup(_scope.Dispose, Target.Dispose);
    }
}
