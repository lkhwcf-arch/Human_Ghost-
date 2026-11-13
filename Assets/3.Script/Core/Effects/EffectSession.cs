using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay.Effects
{
    // 스테이지 또는 독립 기능 하나가 소유합니다. 전역 게시기와 엔진 객체를 소유하지 않습니다.
    public class EffectSession : IDisposable
    {
        private readonly Dictionary<EffectExecution, EffectStateFactory> _factories = new Dictionary<EffectExecution, EffectStateFactory>();
        private readonly Dictionary<EffectOperation, IEffectAction> _actions;
        private readonly Dictionary<(int, EffectTarget), List<EffectState>> _contacts = new Dictionary<(int, EffectTarget), List<EffectState>>();
        private readonly List<EffectState> _timed = new List<EffectState>();
        private bool _disposed;
        private bool _ticking;
        public bool NeedsTick => !_disposed && _timed.Count > 0;
        public event Action<EffectResult> Changed;

        public EffectSession(IEnumerable<EffectStateFactory> factories = null,
            IDictionary<EffectOperation, IEffectAction> actions = null)
        {
            foreach (var factory in factories ?? new EffectStateFactory[] {
                new ImmediateEffectStateFactory(), new PeriodicEffectStateFactory(), new MaintainedEffectStateFactory() })
                _factories.Add(factory.Execution, factory);
            _actions = actions == null ? new Dictionary<EffectOperation, IEffectAction> {
                [EffectOperation.Damage] = new DamageEffectAction(), [EffectOperation.KnockbackRate] = new KnockbackEffectAction()
            } : new Dictionary<EffectOperation, IEffectAction>(actions);
        }
        public void Enter(int sourceID, IReadOnlyList<EffectDefinition> definitions, EffectTarget target)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EffectSession));
            if (!target.IsAvailable || _contacts.ContainsKey((sourceID, target))) return;
            var states = new List<EffectState>(definitions.Count);
            _contacts.Add((sourceID, target), states);
            try
            {
                foreach (var definition in definitions)
                    if (target.CanReceive(definition))
                        states.Add(_factories[definition.Execution].Create(sourceID, definition, target,
                            _actions[definition.Operation], Publish));
                foreach (var state in states)
                {
                    if (_disposed) break;
                    state.Start();
                    if (state.NeedsTick && !_disposed) _timed.Add(state);
                }
            }
            catch (Exception error)
            {
                try { Exit(sourceID, target); }
                catch (Exception cleanupError) { throw new AggregateException("효과 시작과 정리에 실패했습니다.", error, cleanupError); }
                throw;
            }
        }
        private void Publish(EffectResult result) => Changed?.Invoke(result);
        public void Exit(int sourceID, EffectTarget target)
        {
            if (!_contacts.TryGetValue((sourceID, target), out var states)) return;
            _contacts.Remove((sourceID, target));
            var cleanup = new SubscriptionScope();
            foreach (var state in states) cleanup.Add(state.Dispose);
            try { cleanup.Dispose(); }
            finally { if (!_ticking) _timed.RemoveAll(static state => state.IsEnded); }
        }
        public void Tick(int elapsedMs)
        {
            if (elapsedMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMs));
            if (_disposed) return;
            if (_ticking) throw new InvalidOperationException("효과 시간 진행을 중첩 호출할 수 없습니다.");
            int count = _timed.Count;
            _ticking = true;
            try { for (int i = 0; i < count && i < _timed.Count && !_disposed; i++) _timed[i].Tick(elapsedMs); }
            finally { _ticking = false; _timed.RemoveAll(static state => state.IsEnded); }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var cleanup = new SubscriptionScope();
            foreach (var states in _contacts.Values)
                foreach (var state in states) cleanup.Add(state.Dispose);
            _contacts.Clear(); _timed.Clear();
            try { cleanup.Dispose(); }
            finally { Changed = null; }
        }
    }
}
