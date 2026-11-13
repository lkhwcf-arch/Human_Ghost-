using System;
namespace HumanGhost.Gameplay
{
    // 보스 개발 설정의 피격 반응입니다. 생성 enum/미배포 StatusEffect 행을 임의 등록하지 않습니다.
    public sealed class BossHitReaction
    {
        public int KnockbackDistance { get; }
        public int KnockbackTime { get; }
        public int DownTime { get; }
        public int SlowRate { get; }
        public int SlowTime { get; }
        public bool Parryable { get; }
        public BossHitReaction(int distance=0, int knockbackTime=0, int downTime=0, int slowRate=10000, int slowTime=0, bool parryable=true)
        {
            if(distance<0 || knockbackTime<0 || (distance==0)!=(knockbackTime==0) || downTime<0 || slowRate<=0 || slowRate>10000 || slowTime<0)
                throw new ArgumentOutOfRangeException(nameof(distance));
            KnockbackDistance=distance;KnockbackTime=knockbackTime;DownTime=downTime;SlowRate=slowRate;SlowTime=slowTime;Parryable=parryable;
        }
    }
}
