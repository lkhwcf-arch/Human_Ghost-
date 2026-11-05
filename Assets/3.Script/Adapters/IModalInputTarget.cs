namespace HumanGhost.Gameplay.Adapters
{
    // 오류 모달의 잠금은 기존 대화·일시정지 잠금과 별도로 소유합니다.
    public interface IModalInputTarget
    {
        void SetModalBlocked(bool blocked);
    }
}
