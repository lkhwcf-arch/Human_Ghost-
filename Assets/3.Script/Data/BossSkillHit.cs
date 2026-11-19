// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class BossSkillHit
    {
        public int ID { get; }
        public int BossSkillID { get; }
        public int Windup { get; }
        public int Active { get; }
        public int Recovery { get; }
        public int ExtraDamage { get; }
        public int Range { get; }
        public int Height { get; }
        public int DamageGroup { get; }
        public bool Radial { get; }
        public int KnockbackDistance { get; }
        public int KnockbackTime { get; }
        public int DownTime { get; }
        public int SlowTime { get; }
        public int SlowRate { get; }
        public bool Parryable { get; }
        public BossSkillHit(int id, int bossSkillID, int windup, int active, int recovery, int extraDamage, int range, int height, int damageGroup, bool radial, int knockbackDistance, int knockbackTime, int downTime, int slowTime, int slowRate, bool parryable)
        {
            ID = id;
            BossSkillID = bossSkillID;
            Windup = windup;
            Active = active;
            Recovery = recovery;
            ExtraDamage = extraDamage;
            Range = range;
            Height = height;
            DamageGroup = damageGroup;
            Radial = radial;
            KnockbackDistance = knockbackDistance;
            KnockbackTime = knockbackTime;
            DownTime = downTime;
            SlowTime = slowTime;
            SlowRate = slowRate;
            Parryable = parryable;
        }
    }
}
