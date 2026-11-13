using System;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;
internal static class BossHitReactionChecks
{
    public static void Run(Player player, Action<bool,string> check)
    {
        PlayerActor Target()=>new PlayerActor(player,Enumerable.Range(0,5).Select(_=>new AttackSpec(1,1,0,0,0,0,0)).ToArray(),true,100);
        var down=new BossHitReaction(300,300,600,parryable:false);var slow=new BossHitReaction(slowRate:5000,slowTime:1000,parryable:false);
        var target=Target();target.ConfigureHerbs(5,100,3);
        check(target.ReceiveBossHit(5,false,down)&&target.CurrentState==StateKey.Stun&&target.DownDuration==600,"보스 다운은 사망이 아닌 600ms Stun");
        check(!target.TryUseHerb()&&!target.TryRoll()&&!target.TryAttack()&&!target.TryJump(),"다운 중 약초·구르기·공격·점프 거부");
        target.Tick(599);check(target.CurrentState==StateKey.Stun,"다운600ms 이전");target.Tick(1);
        check(target.Locomotion.CanMove&&target.IsInvulnerable,"일어나면 이동 복구·피격 무적 시작");target.Tick(599);check(target.IsInvulnerable,"기상 뒤600ms 이전 무적");target.Tick(1);check(!target.IsInvulnerable,"기상 뒤600ms 끝 무적 해제");
        target=Target();target.ReceiveBossHit(1,false,new BossHitReaction(100,200));target.Tick(300);check(target.Locomotion.CanMove&&target.IsInvulnerable,"보스 일반 피격300ms 뒤 무적600ms");target.Tick(600);check(!target.IsInvulnerable,"보스 일반 피격 무적 끝");
        target=Target();target.SetMove(1);target.ReceiveBossHit(1,false,slow);target.Tick(300);
        check(target.HorizontalMoveSpeed==player.MoveSpeed/2,"울림 감속은 이동 속도50%");target.Tick(600);check(target.ReceiveBossHit(1,false,slow),"무적 종료 뒤 감속 갱신");target.Tick(999);check(target.HorizontalMoveSpeed==player.MoveSpeed/2,"같은 감속 중첩 없이 시간만 갱신");target.Tick(1);check(target.HorizontalMoveSpeed==player.MoveSpeed,"1000ms 뒤 감속 완전 해제");
        target=Target();target.TryRoll();int hp=target.Health;check(!target.ReceiveBossHit(5,false,down)&&target.Health==hp&&target.DownDuration==0,"구르기 무적이면 피해·다운 모두 없음");
        var common=new UnitStat(1006,0,"Boss",null,500,34,0,0,0,0,120,250,0);var phase=new BossState(4002,1006,600,600,2000,15,2000,"DAMAGE",1800,false,1,"Rage",5000);
        foreach(bool parryable in new[]{true,false}){
            var boss=new BossPoise(new Boss(common,new[]{phase}),phase);var reaction=new BossHitReaction(100,200,parryable:parryable);var pattern=new BossPattern(StateKey.Attack1,new[]{new AttackSpec(1,100,0,0,0,0,0)},reactions:new[]{reaction});var combat=new BossCombat(boss,new[]{pattern},(min,max)=>min);
            target=Target();target.ConfigureActions(new PlayerAction(2101,player.ID,300,30,1000,200,30,100,300));target.SetMove(-1);target.SetMove(0);check(target.TryParry(),"패링 준비");combat.TryStart(0);combat.Tick(1);hp=target.Health;var outcome=combat.TryHitPlayer(1,target,(min,max)=>max-1,-1);
            check(parryable ? outcome==HitOutcome.Parried&&target.Health==hp&&boss.Poise==90 : outcome==HitOutcome.Hit&&target.Health<hp&&boss.Poise==120,"패턴별 패링 허용/불가·성공 체세30");
        }
        target=Target();check(target.TryChainBind(900,3,1),"보스 사슬 결박");target.Tick(900);check(target.Locomotion.CanMove&&target.IsInvulnerable,"사슬 자동 해제 뒤 무적600ms");target.Tick(600);check(!target.IsInvulnerable,"사슬 해제 무적 종료");
    }
}
