using System;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class PriorityChecks
{
    public static void Run(Player definition, Action<bool, string> check)
    {
        AttackSpec[] specs = Enumerable.Range(0, 5).Select(i => new AttackSpec(120, 80, 130, i < 3 ? 130 : 0, 0, 8, 10)).ToArray();
        PlayerActor Actor(int stamina = 100, bool grounded = true)
        {
            var actor = new PlayerActor(definition, specs, grounded, stamina);
            actor.ConfigureActions(new PlayerAction(2101, definition.ID, 300, 30, 1000, 200, 30, 100, 300));
            return actor;
        }
        var player = Actor(); player.SetMove(1);
        check(player.TryParry() && player.Stamina.Current == 70 && !player.Locomotion.CanMove, "패링 시작 소모30·이동 잠금");
        check(!player.TryParry() && !player.TryAttack() && !player.TryRoll() && !player.TryJump(), "패링 중 중복 행동 금지");
        player.SetMove(-1);
        check(player.Facing == 1, "패링 중 방향 고정");
        var incoming = new AttackWindow(); incoming.SetActive(true);
        int feedback = 0; player.ParrySucceeded += stop => { check(stop == 100, "공급 패링 히트스톱"); feedback++; };
        check(!player.TryParryContact(incoming, 1, -1), "후방 공격 패링 거부");
        player.Tick(299);
        check(player.TryParryContact(incoming, 1, 1) && player.Health == definition.HP, "299ms 유효 패링·피해0");
        check(!player.TryParryContact(incoming, 1, 1) && feedback == 1, "성공/피드백 한 번");
        player.Tick(199); check(!player.Locomotion.CanMove, "성공 후딜199ms 잠금");
        player.Tick(1); check(player.Locomotion.CanMove && !player.IsActionBusy, "성공 후딜200ms 종료");
        var expired = Actor(); expired.TryParry(); expired.Tick(300);
        check(!expired.TryParryContact(incoming, 2, 0) && expired.IsActionBusy, "300ms 판정 끝·후딜은 남음");
        expired.Tick(200); check(!expired.IsActionBusy, "실패 전체500ms 종료");
        var poor = Actor(29); int refused = 0; poor.StaminaRefused += () => refused++;
        check(!poor.TryParry() && poor.Stamina.Current == 29 && refused == 1, "기력 부족 시 입력 무시·게이지 이벤트");
        check(!Actor(100, false).TryParry(), "공중 패링 거부");
        var interrupted = Actor(); interrupted.TryParry(); interrupted.ReceiveHit(1);
        check(!interrupted.IsActionBusy && interrupted.CurrentState == StateKey.Hit, "경직 시 패링 즉시 정리");
        var cancel = Actor(); cancel.TryAttack(); cancel.Tick(199);
        check(!cancel.TryParry(), "평타 판정 중 캔슬 금지"); cancel.Tick(1);
        check(cancel.TryParry() && !cancel.IsAttackActive, "1~3타 입력 창 패링 캔슬");

        var standalone = new PlayerParry(new PlayerAction(2101,definition.ID,300,30,1000,200,30,100,0));
        check(standalone.TryBegin() && !standalone.TryBegin() && standalone.Tick(700) == 500 && !standalone.IsActive,
            "독립 모듈·교체 설정 없이 시간 초과분 반환");
        var noParry = new PlayerActor(definition,specs,true,100);
        check(!noParry.TryParry() && noParry.TryAttack(), "패링 미연결 시 다른 전투 독립 작동");
        var other = Actor(); other.TryParry(); other.CancelCombatAction();
        check(!other.IsActionBusy && other.TryJump(), "패링 해제 시 이동 잠금 정리");
        var cinematic = Actor(); cinematic.SetCutsceneDamageBlocked(true);
        check(!cinematic.TryParry() && cinematic.Stamina.Current == 100, "컷씬 패링 차단·소모 없음");
        var heal = Actor(); heal.ConfigureHerbs(10,1000,3); heal.ReceiveHit(1); heal.Tick(definition.HitIFrame);
        check(heal.TryUseHerb() && !heal.TryParry(), "약초 중 패링 차단");

        var common = new UnitStat(1002,0,"ParryFixture",null,100,15,0,0,0,0,0,200,0);
        var monster = new Monster(common,3002,1002,0,0,0,0,1000,"DAMAGE",0,true,"JIGEGWI",800,600,true);
        MonsterActor Enemy(bool charge = false, bool projectile = false, bool grab = false)
            => new MonsterActor(monster,new[]{new MonsterPattern(StateKey.Attack2,600,
                new AttackSpec(1,500,100,0,0,0,0),bindTime:grab?1000:0,bindPresses:grab?3:0,
                chargeSpeed:charge?800:0,wallStun:charge?800:0,isProjectile:projectile)},300,500,(min,max)=>min);
        int NoRoll(int min,int max) => throw new Exception("패링 성공 시 난수 소비 금지");
        foreach (var enemy in new[]{Enemy(),Enemy(charge:true),Enemy(projectile:true)})
        {
            var target = Actor(); target.TryParry(); enemy.Observe(2001,100,1); enemy.Tick(1);
            check(enemy.TryHitPlayer(2001,target,NoRoll,1) == HitOutcome.Parried && target.Health == definition.HP &&
                enemy.CurrentState == StateKey.Stun && !enemy.IsAttackActive && enemy.Velocity == 0,
                "근접·돌진·투척 패링 피해0·창 닫힘·정지·스턴");
            enemy.Tick(999); check(enemy.CurrentState == StateKey.Stun,"스턴999ms 유지");
            enemy.Tick(1); check(enemy.CurrentState != StateKey.Stun,"스턴1000ms 종료");
        }
        var grabber = Enemy(grab:true); var grabbed = Actor(); grabbed.TryParry(); grabber.Observe(2001,100,1); grabber.Tick(1);
        check(grabber.TryHitPlayer(2001,grabbed,(min,max)=>max-1,1) == HitOutcome.Hit && grabbed.BoundBy == grabber && !grabbed.IsActionBusy,
            "잡기는 패링 불가·패링 수명 정리");
        var phase = new BossState(4001,1006,1500,1500,2000,15,2000,"DAMAGE",1800,false,0,"Idle",10000);
        var boss = new BossPoise(new Boss(new UnitStat(1006,0,"Boss",null,420,28,4,0,0,0,120,250,0),new[]{phase}),phase);
        var combat = new BossCombat(boss,new[]{new BossPattern(StateKey.Attack1,new[]{new AttackSpec(1,500,100,0,0,0,0)})},(min,max)=>min);
        try
        {
            var target = Actor(); target.TryParry(); combat.TryStart(0); combat.Tick(1);
            check(combat.TryHitPlayer(2001,target,NoRoll,1) == HitOutcome.Parried && boss.Poise == 90 && boss.Health == 420 &&
                boss.CurrentState == StateKey.Attack1 && target.Health == definition.HP,"보스 스턴 없음·체세30·HP무피해");
            check(combat.TryHitPlayer(2001,target,NoRoll,1) == HitOutcome.None && boss.Poise == 90,"같은 보스 공격 중복 패링 없음");
        }
        finally { combat.Release(); }
        var hanger = Actor(100,false); hanger.SetMove(1); hanger.TryHang(); hanger.SetMove(-1);
        check(hanger.Facing == 1 && hanger.Locomotion.HorizontalSpeed == 0 && !hanger.TryAttack() && !hanger.TryParry(),
            "매달린 동안 방향·이동·전투 고정");
        hanger.Locomotion.SetGrounded(true); hanger.EndHang();
        check(hanger.Facing == -1 && hanger.Locomotion.CanMove,"매달리기 종료 후 이동·방향 복구");
        var falling = Actor(); falling.TryParry(); falling.ApplyFall(int.MaxValue);
        check(falling.IsDead && !falling.IsActionBusy,"낙사 즉사·패링 잠금 해제");
    }
}
