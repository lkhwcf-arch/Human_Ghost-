// 시트에서 자동 생성합니다. 매 배포 갱신하므로 직접 수정하지 마세요.
namespace HumanGhost.Data.Definitions
{
    public enum FieldKind
    {
        FireStatic = 0,
        FireCycle = 1,
        FireSpread = 2,
        GuideFire = 3,
        Rest = 4,
        Ice = 5,
    }
    public static class FieldKindKeys
    {
        public static FieldKind Parse(string key)
        {
            switch (key)
            {
                case "FIRE_STATIC": return FieldKind.FireStatic;
                case "FIRE_CYCLE": return FieldKind.FireCycle;
                case "FIRE_SPREAD": return FieldKind.FireSpread;
                case "GUIDE_FIRE": return FieldKind.GuideFire;
                case "REST": return FieldKind.Rest;
                case "ICE": return FieldKind.Ice;
                default: throw new System.InvalidOperationException("열거형 입력 키를 해석하지 못했습니다: " + key);
            }
        }
    }
}
