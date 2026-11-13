using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay.Effects
{
    // 대상 연결 한 번의 수명을 표현합니다. 재사용할 때는 새 인스턴스를 만듭니다.
    public class EffectTarget : IDisposable
    {
        private readonly Func<bool> _isAlive;
        private readonly Func<int, bool> _damage;
        private readonly Dictionary<object, int> _knockbackRates = new Dictionary<object, int>();
        private bool _disposed;
        private readonly HashSet<EffectCategory> _immunities;
        public int ID { get; }
        public long Generation { get; }
        public bool IsAvailable => !_disposed && _isAlive();
        public event Action Released;

        public EffectTarget(int id, long generation, Func<bool> isAlive, Func<int, bool> damage, IEnumerable<EffectCategory> immunities = null)
        {
            ID = id; Generation = generation;
            _isAlive = isAlive ?? throw new ArgumentNullException(nameof(isAlive));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _immunities = new HashSet<EffectCategory>(immunities ?? Array.Empty<EffectCategory>());
        }
        public bool CanReceive(EffectDefinition definition) => IsAvailable && !_immunities.Contains(definition.Category);
        public bool ReceiveDamage(int value) => IsAvailable && _damage(value);
        public void SetKnockbackRate(object token, int value)
        { if (IsAvailable) _knockbackRates[token] = value; }
        public void RemoveKnockbackRate(object token) => _knockbackRates.Remove(token);

        // 같은 종류의 영역이 겹쳐도 곱하지 않고 가장 강한 배율 하나를 사용합니다.
        public int ResolveKnockbackDistance(int distance)
        {
            if (distance < 0) throw new ArgumentOutOfRangeException(nameof(distance));
            int rate = !IsAvailable || _knockbackRates.Count == 0 ? 10000 : 0;
            if (IsAvailable) foreach (int value in _knockbackRates.Values) rate = Math.Max(rate, value);
            return checked((int)((long)distance * rate / 10000));
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _knockbackRates.Clear();
            Action released = Released;
            Released = null;
            var cleanup = new SubscriptionScope();
            if (released != null) foreach (Action release in released.GetInvocationList()) cleanup.Add(release);
            cleanup.Dispose();
        }
    }
}
