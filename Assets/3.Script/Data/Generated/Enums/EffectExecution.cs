// 시트에서 자동 생성합니다. 매 배포 갱신하므로 직접 수정하지 마세요.
namespace HumanGhost.Data.Definitions
{
    public enum EffectExecution
    {
        Immediate = 0,
        Periodic = 1,
        Maintained = 2,
    }
    public static class EffectExecutionKeys
    {
        public static EffectExecution Parse(string key)
        {
            switch (key)
            {
                case "IMMEDIATE": return EffectExecution.Immediate;
                case "PERIODIC": return EffectExecution.Periodic;
                case "MAINTAINED": return EffectExecution.Maintained;
                default: throw new System.InvalidOperationException("열거형 입력 키를 해석하지 못했습니다: " + key);
            }
        }
    }
}
