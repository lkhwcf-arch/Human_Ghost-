using System;
using PlayerDefinition = HumanGhost.Data.Definitions.Player;

namespace HumanGhost.Gameplay
{
    // 개체별 스태미나 자원입니다. 행동 비용은 호출자가 전달하며 공격 비용은 소유하지 않습니다.
    public sealed class PlayerStamina
    {
        private const int _millisecondsPerSecond = 1000;

        private int _current;
        private int _sinceConsume;
        // StaminaRegen(초당) × ms 중 아직 1 미만으로 남은 회복량입니다. 0~999.
        private int _regenRemainder;

        public PlayerDefinition Definition { get; }
        public int Current => _current;
        public int Max => Definition.StaminaMax;
        // 값은 변경 후 현재량입니다.
        public event Action<int> Changed;

        // 초기 현재량은 호출자가 정합니다. 생성 직후는 소모 이력이 없으므로 회복 딜레이 없이 회복합니다.
        public PlayerStamina(PlayerDefinition definition, int current)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (definition.StaminaMax < 0 || definition.StaminaRegen < 0 || definition.StaminaRegenDelay < 0 || definition.RollStamina < 0)
                throw new ArgumentException("스태미나 정의 값은 음수일 수 없습니다.", nameof(definition));
            if (current < 0 || current > definition.StaminaMax) throw new ArgumentOutOfRangeException(nameof(current));
            _current = current;
            _sinceConsume = definition.StaminaRegenDelay;
        }

        // 부족하면 false이며 현재량·회복 딜레이를 바꾸지 않습니다. 비용 0은 변경 없이 true입니다.
        public bool TryConsume(int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (cost > _current) return false;
            if (cost == 0) return true;
            _current -= cost;
            _sinceConsume = 0;
            _regenRemainder = 0;
            Changed?.Invoke(_current);
            return true;
        }

        public bool TryConsumeRoll() => TryConsume(Definition.RollStamina);

        // 마지막 소모 후 StaminaRegenDelay(ms)를 넘긴 구간만 회복합니다.
        public void Tick(int elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            int delayed = Math.Min(elapsedMilliseconds, Definition.StaminaRegenDelay - _sinceConsume);
            _sinceConsume += delayed;
            int regenMilliseconds = elapsedMilliseconds - delayed;
            if (regenMilliseconds == 0) return;
            if (_current >= Max) { _regenRemainder = 0; return; }

            long amount = (long)Definition.StaminaRegen * regenMilliseconds + _regenRemainder;
            long next = _current + amount / _millisecondsPerSecond;
            _regenRemainder = (int)(amount % _millisecondsPerSecond);
            if (next >= Max) { next = Max; _regenRemainder = 0; }
            if (next == _current) return;
            _current = (int)next;
            Changed?.Invoke(_current);
        }
    }
}
