// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class BossSkill
    {
        public int ID { get; }
        public int BossID { get; }
        public int StateID { get; }
        public string Kind { get; }
        public int MinPhase { get; }
        public int MaxPhase { get; }
        public bool HitReactions { get; }
        public int ChargeSpeed { get; }
        public int WallPoiseDamage { get; }
        public int ChainRange { get; }
        public int BindTime { get; }
        public int BindPresses { get; }
        public int PullSpeed { get; }
        public string SoundKey { get; }
        public BossSkill(int id, int bossID, int stateID, string kind, int minPhase, int maxPhase, bool hitReactions, int chargeSpeed, int wallPoiseDamage, int chainRange, int bindTime, int bindPresses, int pullSpeed, string soundKey)
        {
            ID = id;
            BossID = bossID;
            StateID = stateID;
            Kind = kind;
            MinPhase = minPhase;
            MaxPhase = maxPhase;
            HitReactions = hitReactions;
            ChargeSpeed = chargeSpeed;
            WallPoiseDamage = wallPoiseDamage;
            ChainRange = chainRange;
            BindTime = bindTime;
            BindPresses = bindPresses;
            PullSpeed = pullSpeed;
            SoundKey = soundKey;
        }
    }
}
