using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    [CreateAssetMenu(menuName = "인귀/플레이어 전투 설정")]
    public sealed class PlayerCombatSettings : ScriptableObject
    {
        [Serializable]
        private sealed class AttackSetup
        {
            [SerializeField] private int _windup;
            [SerializeField] private int _active;
            [SerializeField] private int _recovery;
            [SerializeField] private int _comboWindow;
            [SerializeField] private int _extraDamage;
            [SerializeField] private int _poiseDamage;
            [SerializeField] private int _staminaCost;

            public AttackSetup(int windup, int active, int recovery, int comboWindow, int extraDamage, int poiseDamage, int staminaCost)
            {
                _windup = windup; _active = active; _recovery = recovery; _comboWindow = comboWindow;
                _extraDamage = extraDamage; _poiseDamage = poiseDamage; _staminaCost = staminaCost;
            }

            public AttackSpec Create() => new AttackSpec(_windup, _active, _recovery, _comboWindow, _extraDamage, _poiseDamage, _staminaCost);
        }

        // 전투 R2 C02의 시작값. 기획은 설정 에셋에서 수정하고 다음 런타임 초기화에 반영합니다.
        [SerializeField] private AttackSetup[] _attacks =
        {
            new AttackSetup(120, 80, 250, 200, 0, 10, 12),
            new AttackSetup(120, 80, 250, 200, 0, 10, 12),
            new AttackSetup(200, 100, 400, 0, 10, 20, 12),
            new AttackSetup(100, 80, 200, 0, -2, 10, 12)
        };

        public AttackSpec[] CreateAttacks()
        {
            if (_attacks == null || _attacks.Length != 4) throw new InvalidOperationException("지상 3타·공중 1타 설정이 필요합니다.");
            var result = new AttackSpec[4];
            for (int i = 0; i < result.Length; i++)
                result[i] = (_attacks[i] ?? throw new InvalidOperationException("공격 설정이 비어 있습니다.")).Create();
            return result;
        }
    }
}
