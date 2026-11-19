// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class MonsterSkill
    {
        public int ID { get; }
        public int MonsterID { get; }
        public int StateID { get; }
        public int EffectID { get; }
        // 공격 패턴 값(11/19 패턴 설정 파일에서 이전). 거리 tile_x100, 시간 ms, 돌진 속도 tile_x100/초입니다.
        public int Range { get; }
        public int MinRange { get; }
        public int Windup { get; }
        public int Active { get; }
        public int Recovery { get; }
        public int ExtraDamage { get; }
        public int BindTime { get; }
        public int BindPresses { get; }
        public int ChargeSpeed { get; }
        public int WallStun { get; }
        public bool Projectile { get; }
        public bool Lob { get; }
        public int RetreatDistance { get; }
        public int Cooldown { get; }
        public MonsterSkill(int id, int monsterID, int stateID, int effectID, int range = 0, int minRange = 0, int windup = 0, int active = 0, int recovery = 0, int extraDamage = 0,
            int bindTime = 0, int bindPresses = 0, int chargeSpeed = 0, int wallStun = 0, bool projectile = false, bool lob = false, int retreatDistance = 0, int cooldown = 0)
        {
            ID = id;
            MonsterID = monsterID;
            StateID = stateID;
            EffectID = effectID;
            Range = range;
            MinRange = minRange;
            Windup = windup;
            Active = active;
            Recovery = recovery;
            ExtraDamage = extraDamage;
            BindTime = bindTime;
            BindPresses = bindPresses;
            ChargeSpeed = chargeSpeed;
            WallStun = wallStun;
            Projectile = projectile;
            Lob = lob;
            RetreatDistance = retreatDistance;
            Cooldown = cooldown;
        }
    }
}
