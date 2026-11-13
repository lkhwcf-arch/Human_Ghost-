// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class FieldObject
    {
        public int ID { get; }
        public string Kind { get; }
        public int WarnMs { get; }
        public int ActiveMs { get; }
        public int OffMs { get; }
        public int Damage { get; }
        public int TickMs { get; }
        public int SpreadSpeed { get; }
        public bool MonsterHit { get; }
        public int HealHP { get; }
        public int SlideDistance { get; }
        public int TurnMs { get; }
        public int KnockbackRate { get; }
        public int TextID { get; }
        public bool ContentEnabled { get; }
        public FieldObject(int id, string kind, int warnMs, int activeMs, int offMs, int damage, int tickMs, int spreadSpeed, bool monsterHit, int healHP, int slideDistance, int turnMs, int knockbackRate, int textID, bool contentEnabled)
        {
            ID = id;
            Kind = kind;
            WarnMs = warnMs;
            ActiveMs = activeMs;
            OffMs = offMs;
            Damage = damage;
            TickMs = tickMs;
            SpreadSpeed = spreadSpeed;
            MonsterHit = monsterHit;
            HealHP = healHP;
            SlideDistance = slideDistance;
            TurnMs = turnMs;
            KnockbackRate = knockbackRate;
            TextID = textID;
            ContentEnabled = contentEnabled;
        }
    }
}
