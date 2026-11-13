// 시트에서 자동 생성합니다. 매 배포 갱신하므로 직접 수정하지 마세요.
namespace HumanGhost.Data.Definitions
{
    public enum EffectOperation
    {
        Damage = 0,
        KnockbackRate = 1,
    }
    public static class EffectOperationKeys
    {
        public static EffectOperation Parse(string key)
        {
            switch (key)
            {
                case "DAMAGE": return EffectOperation.Damage;
                case "KNOCKBACK_RATE": return EffectOperation.KnockbackRate;
                default: throw new System.InvalidOperationException("열거형 입력 키를 해석하지 못했습니다: " + key);
            }
        }
    }
}
