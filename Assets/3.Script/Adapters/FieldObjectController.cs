using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Effects;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    public class FieldObjectArea
    {
        private readonly bool _acceptsMonsters;
        private readonly IReadOnlyList<EffectDefinition> _effects;
        public int ID { get; }
        public Bounds Bounds { get; }
        public FieldKind Kind { get; }
        public FieldObjectArea(int id, Bounds bounds, FieldObject definition)
        {
            ID = id; Bounds = bounds; _effects = FieldEffectData.Read(definition);
            Kind = FieldEffectData.ReadKind(definition.Kind);
            _acceptsMonsters = definition.MonsterHit || Kind == FieldKind.Ice;
        }
        public void Observe(FieldEffectTargetBinding binding, EffectSession session)
        {
            bool accepts = !binding.IsMonster || _acceptsMonsters;
            Bounds target = binding.Bounds;
            bool overlaps = Bounds.min.x <= target.max.x && Bounds.max.x >= target.min.x
                && Bounds.min.y <= target.max.y && Bounds.max.y >= target.min.y;
            if (accepts && binding.Target.IsAvailable && overlaps)
                session.Enter(ID, _effects, binding.Target);
            else session.Exit(ID, binding.Target);
        }
    }

    // 독립 조립자가 영역·대상·시간만 공급하면 사용할 수 있는 공용 연결 담당입니다.
    public class FieldObjectController : IDisposable
    {
        private readonly List<FieldObjectArea> _areas;
        private readonly List<FieldEffectTargetBinding> _bindings = new List<FieldEffectTargetBinding>();
        private readonly Dictionary<SpatialRuntime, FieldEffectTargetBinding> _targets = new Dictionary<SpatialRuntime, FieldEffectTargetBinding>();
        private readonly EffectSession _session = new EffectSession();
        private bool _disposed;
        private bool _ready;
        public bool NeedsTick => !_disposed && _ready && _session.NeedsTick;
        public event Action<EffectResult> Changed { add { _session.Changed += value; } remove { _session.Changed -= value; } }
        public FieldObjectController(IEnumerable<FieldObjectArea> areas) => _areas = new List<FieldObjectArea>(areas);
        public void Bind(PlayerRuntime runtime, SpriteRenderer body) => Add(runtime, new FieldEffectTargetBinding(runtime, body, Observe));
        public void Bind(MonsterRuntime runtime, SpriteRenderer body) => Add(runtime, new FieldEffectTargetBinding(runtime, body, Observe));
        private void Add(SpatialRuntime runtime, FieldEffectTargetBinding binding)
        {
            if (_disposed || _targets.ContainsKey(runtime))
            { binding.Dispose(); throw new InvalidOperationException("종료되었거나 이미 연결한 필드 효과 대상입니다."); }
            _bindings.Add(binding); _targets.Add(runtime, binding);
        }
        private void Observe(FieldEffectTargetBinding binding)
        {
            if (_disposed || !_ready) return;
            foreach (var area in _areas) area.Observe(binding, _session);
        }
        public void RefreshContacts()
        { if (!_disposed) foreach (var binding in _bindings) binding.Refresh(); }
        public void SetReady(bool ready)
        {
            if (_disposed || _ready == ready) return;
            _ready = ready;
            if (ready) RefreshContacts();
        }
        public void Tick(int milliseconds) { if (!_disposed && _ready) _session.Tick(milliseconds); }
        // 거리 결정 명령에서 한 번 호출합니다. 이동 중에는 다시 계산하지 않습니다.
        public int ResolveKnockbackDistance(SpatialRuntime runtime, int distance)
            => _targets.TryGetValue(runtime, out var binding) ? binding.Target.ResolveKnockbackDistance(distance) : distance;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var cleanup = new SubscriptionScope();
            foreach (var binding in _bindings) cleanup.Add(binding.Dispose);
            cleanup.Add(_session.Dispose);
            _bindings.Clear(); _targets.Clear();
            cleanup.Dispose();
        }
    }
}
