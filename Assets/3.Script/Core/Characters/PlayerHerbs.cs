using System;

namespace HumanGhost.Gameplay
{
    // 약초(C04) 개수와 회복 값만 소유합니다. Buff 14001 Value·UseTime·Charges를 받습니다.
    public sealed class PlayerHerbs
    {
        private int _charges;
        public int Capacity => _charges;
        public int Count { get; private set; }
        public int Heal { get; private set; }
        public int UseTime { get; }
        public event Action Changed;

        public PlayerHerbs(int heal, int useTime, int charges)
        {
            if (heal <= 0 || useTime < 0 || charges < 0) throw new ArgumentOutOfRangeException(nameof(heal));
            Heal = heal; UseTime = useTime; _charges = charges; Count = charges;
        }

        // 사용을 시작하면 개수가 줄어듭니다. 맞아서 취소돼도 돌려주지 않습니다.
        public bool TryConsume()
        {
            if (Count == 0) return false;
            Count--;
            Changed?.Invoke();
            return true;
        }

        public void Refill() => SetCount(_charges);
        public void Configure(int heal, int charges)
        {
            if (heal <= 0 || charges < 0) throw new ArgumentOutOfRangeException(nameof(heal));
            if (Heal == heal && _charges == charges) return;
            Heal = heal; _charges = charges; Count = Math.Min(Count, charges);
            Changed?.Invoke();
        }

        public void Restore(int count)
        {
            if (count < 0 || count > _charges) throw new ArgumentOutOfRangeException(nameof(count));
            SetCount(count);
        }
        private void SetCount(int count)
        {
            if (Count == count) return;
            Count = count;
            Changed?.Invoke();
        }
    }
}
