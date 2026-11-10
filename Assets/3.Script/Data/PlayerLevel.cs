// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class PlayerLevel
    {
        public int ID { get; }
        public int Level { get; }
        public int NeedExp { get; }
        public int HPAdd { get; }
        public int StaminaAdd { get; }
        public int ATKAdd { get; }
        public int DEFAdd { get; }
        public bool ContentEnabled { get; }
        public PlayerLevel(int id, int level, int needExp, int hPAdd, int staminaAdd, int aTKAdd, int dEFAdd, bool contentEnabled)
        {
            ID = id;
            Level = level;
            NeedExp = needExp;
            HPAdd = hPAdd;
            StaminaAdd = staminaAdd;
            ATKAdd = aTKAdd;
            DEFAdd = dEFAdd;
            ContentEnabled = contentEnabled;
        }
    }
}
