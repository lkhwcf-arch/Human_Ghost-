using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class PlayerAttackChecks
{
    public static void Run(Player definition, Action<bool, string> check)
    {
        var specs = new AttackSpec[5];
        for (int i = 0; i < specs.Length; i++) specs[i] = new AttackSpec(10, 10, 20, i < 3 ? 20 : 0, 0, 0, 0);
        PlayerActor Create(bool grounded = true)
        {
            var actor = new PlayerActor(definition, specs, grounded, definition.StaminaMax);
            actor.ConfigureActions(new PlayerAction(2101, definition.ID, 300, 30, 1000, 200, 30, 100, 300));
            return actor;
        }
        var states = new[] { StateKey.Attack1, StateKey.Attack2, StateKey.Attack3, StateKey.Attack4 };
        var ground = Create(); ground.SetMove(1);
        check(ground.TryAttack(), "지상 공격 시작");
        for (int hit = 0; hit < 4; hit++)
        {
            check(ground.CurrentState == states[hit], "현재 타수만 실행");
            ground.SetMove(-1);
            check(ground.Facing == 1 && ground.HorizontalMoveSpeed == 0, "지상 타격 중 방향 고정·정지");
            if (hit < 3)
            {
                ground.Tick(20); check(ground.TryAttack(), "후딜의 다음 타 입력 수용"); ground.Tick(20);
            }
            else ground.Tick(40);
        }
        check(!ground.IsAttacking && ground.Facing == -1 && ground.HorizontalMoveSpeed == -definition.MoveSpeed,
            "콤보 종료 뒤 유지 입력의 이동·방향 복구");

        var air = Create(false); air.SetMove(1);
        check(air.TryAttack() && air.IsAirAttacking && air.HorizontalMoveSpeed == definition.MoveSpeed, "공중 평타 시작해도 수평 이동 유지");
        check(!air.Locomotion.CanMove && !air.TryJump() && !air.TryHang() && !air.TryAttack(), "공중 이동 허용이 점프·매달림·추가타를 풀지 않음");
        for (int phase = 0; phase < 3; phase++)
        {
            air.SetMove(-1);
            check(air.Facing == 1 && air.HorizontalMoveSpeed == -definition.MoveSpeed, "공중 공격 단계별 이동 조작·바라보기 분리");
            air.Tick(10);
        }
        air.Tick(10);
        check(!air.IsAttacking && air.Facing == -1 && air.CurrentState == StateKey.Jump, "공중 공격 완료 후 정상 공중 이동 복구");

        var landing = Create(false); landing.SetMove(1); landing.TryAttack(); landing.Tick(10); landing.SetMove(-1); landing.SetGrounded(true);
        check(!landing.IsAirAttacking && !landing.IsAttackActive && landing.CurrentAttack == null, "착지 즉시 공중 공격·잔여 판정 종료");
        check(landing.HorizontalMoveSpeed == -definition.MoveSpeed && landing.Facing == -1 && landing.Locomotion.CanMove, "착지 같은 호출에서 이동·방향 복구");
        landing.Tick(40); check(landing.HorizontalMoveSpeed == -definition.MoveSpeed, "이전 후딜이 착지 후 다시 이동을 잠그지 않음");
        var groundedAttack = Create(); groundedAttack.TryAttack(); groundedAttack.SetGrounded(true);
        check(groundedAttack.IsAttacking && groundedAttack.HorizontalMoveSpeed == 0, "중복 접지 관측은 지상 공격을 취소하지 않음");
        var interrupted = Create(false); interrupted.SetMove(1); interrupted.TryAttack(); interrupted.ReceiveHit(1);
        check(!interrupted.IsAirAttacking && interrupted.HorizontalMoveSpeed == 0, "피격 중단 시 공중공격 이동 허용 해제");
        interrupted.ReceiveHit(int.MaxValue);
        check(interrupted.HorizontalMoveSpeed == 0, "피격·사망 상태는 공중 공격 이동 허용을 상속하지 않음");

        var parry = Create(); parry.SetMove(1); check(parry.TryParry(), "패링 시작"); parry.SetMove(-1);
        parry.Tick(300); check(parry.IsParrying && parry.Facing == 1 && parry.HorizontalMoveSpeed == 0, "패링 판정 종료 뒤 후딜에도 방향·이동 잠금");
        parry.Tick(199); check(parry.Facing == 1 && !parry.TryAttack(), "패링 후딜 마지막까지 공격·방향 잠금");
        parry.Tick(1); check(!parry.IsParrying && parry.Facing == -1 && parry.HorizontalMoveSpeed == -definition.MoveSpeed, "패링 완료 후 입력 방향 복구");
    }
}
