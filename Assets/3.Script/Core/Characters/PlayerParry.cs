using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 패링 수치·판정 창·후딜만 소유합니다. 입력·상태 ID·보상·표시와 독립적입니다.
    public sealed class PlayerParry
    {
        private readonly PlayerAction _definition;
        private int _remaining, _window;
        public bool IsActive => _remaining > 0;
        public int PlayerId => _definition.PlayerID;
        public int StaminaCost => _definition.ParryStamina;
        public int StunTime => _definition.ParryStunTime;
        public int PoiseDamage => _definition.ParryPoiseDamage;
        public int HitStop => _definition.ParryHitStop;

        public PlayerParry(PlayerAction definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.ParryWindow <= 0 || definition.ParryStamina < 0 || definition.ParryStunTime <= 0 ||
                definition.ParryRecovery < 0 || definition.ParryPoiseDamage < 0 || definition.ParryHitStop < 0 ||
                (long)definition.ParryWindow + definition.ParryRecovery > int.MaxValue)
                throw new ArgumentException("패링 정의를 확인하세요.", nameof(definition));
            _definition = definition;
        }

        public bool TryBegin()
        {
            if (IsActive) return false;
            _window = _definition.ParryWindow;
            _remaining = _window + _definition.ParryRecovery;
            return true;
        }

        // 소비한 시간만 반환해 호출자가 나머지 시간을 다른 행동에 전달합니다.
        public int Tick(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            int consumed = Math.Min(milliseconds, _remaining);
            _remaining -= consumed; _window = Math.Max(0, _window - consumed);
            return consumed;
        }

        internal bool TryDeflect(AttackWindow incoming, int targetId, int direction, int facing)
        {
            if (incoming == null) throw new ArgumentNullException(nameof(incoming));
            if (direction < -1 || direction > 1) throw new ArgumentOutOfRangeException(nameof(direction));
            if (facing != -1 && facing != 1) throw new ArgumentOutOfRangeException(nameof(facing));
            if (!IsActive || _window <= 0 || (direction != 0 && direction != facing) || !incoming.TryRegisterTarget(targetId)) return false;
            _window = 0; _remaining = _definition.ParryRecovery;
            return true;
        }

        public void Cancel() { _remaining = _window = 0; }
    }
}
