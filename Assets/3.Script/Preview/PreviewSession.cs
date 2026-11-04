namespace HumanGhost.Gameplay.Preview
{
    // 확인 장면 전용 씬 간 임시 상태입니다. 진행 상태 단일 소유자가 정해지면 그쪽으로 옮기고 이 클래스를 지웁니다.
    internal static class PreviewSession
    {
        public static readonly StageProgress Progress = new StageProgress();
        public static string ArrivalAnchor;
    }
}
