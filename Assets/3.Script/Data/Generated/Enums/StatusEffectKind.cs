// 시트에서 자동 생성합니다. 매 배포 갱신하므로 직접 수정하지 마세요.
namespace HumanGhost.Data.Definitions
{
    public enum StatusEffectKind
    {
        Knockback = 0,
    }
    public static class StatusEffectKindKeys
    {
        public static StatusEffectKind Parse(string key)
        {
            switch (key)
            {
                case "Knockback": return StatusEffectKind.Knockback;
                default: throw new System.InvalidOperationException("열거형 입력 키를 해석하지 못했습니다: " + key);
            }
        }
    }
}
