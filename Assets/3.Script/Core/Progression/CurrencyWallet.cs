using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 세션별 보유량입니다. 재화 정의·저장 파일·화면을 변경하거나 소유하지 않습니다.
    public class CurrencyWallet
    {
        private readonly Dictionary<int, Currency> _definitions;
        private readonly Dictionary<int, int> _balances;
        private bool _changing;
        internal bool IsChanging => _changing;
        public event Action<int, int> Changed;

        public CurrencyWallet(IEnumerable<Currency> definitions, IReadOnlyDictionary<int, int> balances = null)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            _definitions = definitions.ToDictionary(definition => definition.ID);
            _balances = _definitions.Keys.ToDictionary(id => id, _ => 0);
            if (balances == null) return;
            foreach (var pair in balances)
            {
                if (!_balances.ContainsKey(pair.Key) || pair.Value < 0)
                    throw new ArgumentException("재화 보유량의 ID 또는 수량이 유효하지 않습니다.", nameof(balances));
                _balances[pair.Key] = pair.Value;
            }
        }

        public int GetBalance(int currencyId) => _balances[currencyId];
        public bool IsEnabled(int currencyId)
            => _definitions.TryGetValue(currencyId, out var definition) && definition.ContentEnabled;
        public IReadOnlyDictionary<int, int> CaptureBalances()
            => new ReadOnlyDictionary<int, int>(new Dictionary<int, int>(_balances));

        public bool TryAdd(int currencyId, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            return TryChange(currencyId, amount, null, null);
        }
        public bool TrySpend(int currencyId, int amount) => TrySpend(currencyId, amount, null, null);

        // 같은 도메인의 구매 담당만 상태 확정을 전달합니다. 콜백은 표시·이벤트·외부 명령을 실행하지 않습니다.
        internal bool TrySpend(int currencyId, int amount, Action commit, Action completed)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            return TryChange(currencyId, -amount, commit, completed);
        }

        private bool TryChange(int currencyId, int delta, Action commit, Action completed)
        {
            if (_changing || !IsEnabled(currencyId)) return false;
            int before = _balances[currencyId];
            long next = (long)before + delta;
            if (next < 0 || next > int.MaxValue) return false;
            _changing = true;
            try
            {
                // 구매 상태를 먼저 확정하고 재화를 반영한 뒤 완료 통지합니다.
                commit?.Invoke();
                _balances[currencyId] = (int)next;
                if (next != before) Changed?.Invoke(currencyId, (int)next);
                completed?.Invoke();
                return true;
            }
            finally { _changing = false; }
        }
    }
}
