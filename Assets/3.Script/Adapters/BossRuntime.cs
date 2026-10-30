using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    public sealed class BossRuntime : MonoBehaviour
    {
        [SerializeField] private int _bossId = 1006;
        [SerializeField] private int _phase = 0;
        private double _remainder;
        public BossPoise Actor { get; private set; }
        public event Action<BossPoise> Initialized;

        public void Initialize(IReadOnlyDataCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (Actor != null) throw new InvalidOperationException("이미 초기화된 보스입니다.");
            Boss definition = catalog.GetTable<Boss>().Get(_bossId);
            BossState phase = null;
            foreach (BossState entry in definition.States) if (entry.Phase == _phase) phase = entry;
            if (phase == null) throw new InvalidOperationException("선택한 보스 단계가 없습니다.");
            Actor = new BossPoise(definition, phase);
            Initialized?.Invoke(Actor);
        }

        private void Update()
        {
            if (Actor == null) return;
            double total = _remainder + Time.deltaTime * 1000.0;
            int milliseconds = (int)Math.Min(int.MaxValue, Math.Floor(total));
            _remainder = total - milliseconds;
            Actor.Tick(milliseconds);
        }
    }
}
