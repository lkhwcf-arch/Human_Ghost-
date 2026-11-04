using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 일반몹 패턴 하나의 읽기 전용 실행 인수. 전투 R2 C08: 패턴 추가 데미지·타이밍은 개발 값(설정 에셋)입니다.
    public sealed class MonsterPattern
    {
        public StateKey Key { get; }
        // 이 패턴을 고를 수 있는 최대 목표 거리(tile_x100).
        public int Range { get; }
        public AttackSpec Attack { get; }
        // BindPresses가 0보다 크면 잡기: 판정 성공 시 비치명 피해와 구속을 적용합니다. 공격 연타 BindPresses회로 해제.
        // BindTime은 구속 상한(ms, 만료 시 자동 해제). 0이면 연타로만 해제. 기획/배포 값 확정 시 값만 교체합니다.
        public int BindTime { get; }
        public int BindPresses { get; }
        // 0보다 크면 돌진: 판정 창 동안 이 속도(tile_x100/초)로 이동하고 벽 충돌 시 WallStun(ms) 허점.
        public int ChargeSpeed { get; }
        public int WallStun { get; }
        // 이 거리보다 가까우면 고르지 않습니다(tile_x100). C08 짐 투척은 300 이내면 쓰지 않습니다.
        public int MinRange { get; }
        // 투척: 판정 창 시간이 비행 시간입니다. 위치·접촉은 외부 판정자가 공급합니다.
        public bool IsProjectile { get; }
        public bool IsGrab => BindPresses > 0;
        public bool IsCharge => ChargeSpeed > 0;

        public MonsterPattern(StateKey key, int range, AttackSpec attack, int bindTime = 0, int bindPresses = 0, int chargeSpeed = 0, int wallStun = 0, int minRange = 0, bool isProjectile = false)
        {
            if (key != StateKey.Attack1 && key != StateKey.Attack2 && key != StateKey.Skill1) throw new ArgumentOutOfRangeException(nameof(key));
            if (range < 0 || bindTime < 0 || bindPresses < 0 || chargeSpeed < 0 || wallStun < 0 || (bindTime > 0 && bindPresses == 0) ||
                (bindPresses > 0 && chargeSpeed > 0) || minRange < 0 || minRange > range || (isProjectile && (bindPresses > 0 || chargeSpeed > 0)))
                throw new ArgumentOutOfRangeException(nameof(range), "패턴 거리·구속·돌진 설정 범위를 확인하세요.");
            Key = key; Range = range;
            Attack = attack ?? throw new ArgumentNullException(nameof(attack));
            BindTime = bindTime; BindPresses = bindPresses; ChargeSpeed = chargeSpeed; WallStun = wallStun;
            MinRange = minRange; IsProjectile = isProjectile;
        }
    }
}
