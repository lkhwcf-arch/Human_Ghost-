// 시트에서 자동 생성합니다. 매 배포 갱신하므로 직접 수정하지 마세요.
namespace HumanGhost.Data.Definitions
{
    public enum EffectCategory
    {
        None = 0,
        Fire = 1,
        Ice = 2,
    }
    public static class EffectCategoryKeys
    {
        public static EffectCategory Parse(string key)
        {
            switch (key)
            {
                case "NONE": return EffectCategory.None;
                case "FIRE": return EffectCategory.Fire;
                case "ICE": return EffectCategory.Ice;
                default: throw new System.InvalidOperationException("열거형 입력 키를 해석하지 못했습니다: " + key);
            }
        }
    }
}
