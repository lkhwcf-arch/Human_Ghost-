namespace HumanGhost.Gameplay
{
    public enum SkipScope { Cutscene, Video, Dialogue, Presentation }

    // 입력과 재생 구현을 분리합니다. 대상이 자기 종료와 최종 상태를 책임집니다.
    public interface ISkipTarget
    {
        event System.Action SkipAvailabilityChanged;
        bool CanSkip(SkipScope scope);
        bool TrySkip(SkipScope scope);
    }
}
