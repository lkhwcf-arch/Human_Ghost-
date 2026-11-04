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

        // 전투 C02_1의 4타 시작값. 공중 체세는 최신 전달 메모의 8을 사용합니다. 기획은 설정 에셋에서 수정하고 다음 런타임 초기화에 반영합니다.
        [SerializeField] private AttackSetup[] _attacks =
        {
            new AttackSetup(120, 80, 250, 200, 0, 8, 12),
            new AttackSetup(120, 80, 250, 200, 0, 8, 12),
            new AttackSetup(120, 80, 250, 200, 0, 8, 12),
            new AttackSetup(200, 100, 400, 0, 10, 16, 12),
            new AttackSetup(100, 80, 200, 0, -2, 8, 12)
        };

        [Header("결박 가르기 C07 개발 값 (피해·체세·쿨타임은 Skill 시트)")]
        [SerializeField, Min(1)] private int _skillWindup = 150;
        [SerializeField, Min(1)] private int _skillActive = 120;
        [SerializeField, Min(0)] private int _skillRecovery = 350;
        [SerializeField, Min(0)] private int _skillDistance = 200;

        public int SkillDistance => _skillDistance;
        public AttackSpec CreateSkill(HumanGhost.Data.Definitions.Skill definition)
            => new AttackSpec(_skillWindup, _skillActive, _skillRecovery, 0, definition.Damage, definition.PoiseDamage, 0);

        public AttackSpec[] CreateAttacks()
        {
            if (_attacks == null || _attacks.Length != 5) throw new InvalidOperationException("지상 4타·공중 1타 설정이 필요합니다.");
            var result = new AttackSpec[_attacks.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = (_attacks[i] ?? throw new InvalidOperationException("공격 설정이 비어 있습니다.")).Create();
            return result;
        }
    }
}
