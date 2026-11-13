// 시트에서 자동 생성합니다. 매 배포 갱신하므로 직접 수정하지 마세요.
namespace HumanGhost.Data.Definitions
{
    public enum EffectPhase
    {
        Started = 0,
        Applied = 1,
        Ended = 2,
    }
    public static class EffectPhaseKeys
    {
        public static EffectPhase Parse(string key)
        {
            switch (key)
            {
                case "STARTED": return EffectPhase.Started;
                case "APPLIED": return EffectPhase.Applied;
                case "ENDED": return EffectPhase.Ended;
                default: throw new System.InvalidOperationException("열거형 입력 키를 해석하지 못했습니다: " + key);
            }
        }
    }
}
