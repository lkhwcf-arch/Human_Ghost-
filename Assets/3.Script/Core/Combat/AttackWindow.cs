using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // 개체별 판정 창과 양수 대상 ID 중복만 관리합니다. 피해·상태 전환은 Actor 책임입니다.
    internal sealed class AttackWindow
    {
        private readonly HashSet<int> _targets = new HashSet<int>();
        public bool IsActive { get; private set; }
        public event Action<bool> Changed;

        public bool TryRegisterTarget(int targetId)
        {
            if (targetId <= 0) throw new ArgumentOutOfRangeException(nameof(targetId));
            return IsActive && _targets.Add(targetId);
        }

        public void SetActive(bool active)
        {
            if (IsActive == active) return;
            IsActive = active;
            Changed?.Invoke(active);
        }

        public void ClearTargets() => _targets.Clear();
    }
}
