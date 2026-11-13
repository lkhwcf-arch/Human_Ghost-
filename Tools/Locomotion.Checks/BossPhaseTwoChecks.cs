using System;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;
internal static class BossPhaseTwoChecks
{
    public static void Run(Player player, Action<bool,string> check)
    {
        var common=new UnitStat(1006,0,"Boss",null,500,34,0,0,0,0,120,250,0);
        var first=new BossState(4001,1006,1000,1000,2000,15,2000,"DAMAGE",1800,false,0,"Idle",10000);
        var second=new BossState(4002,1006,600,600,2000,15,2000,"DAMAGE",1800,false,1,"Rage",5000);
        BossPoise Boss(bool phaseTwo=false)=>new BossPoise(new Boss(common,new[]{first,second}),phaseTwo?second:first,true);
        AttackSpec Hit(int w,int a,int r,int d=0)=>new AttackSpec(w,a,r,0,d,0,0,immediate:w==0);
        BossPattern Pattern(BossPatternKind kind, AttackSpec[] hits, int group=0)=>new BossPattern(StateKey.Attack1,hits,1,
            kind==BossPatternKind.CrawlCharge?1200:0,kind==BossPatternKind.CrawlCharge?30:0,
            kind:kind,maxPhase:1,areas:hits.Select(_=>new BossHitArea(200,100,true,group)).ToArray());
        var charge=Pattern(BossPatternKind.CrawlCharge,new[]{Hit(1000,2500,800)});
        var ground=Pattern(BossPatternKind.GroundSlam,new[]{Hit(900,150,0,14),Hit(0,300,1000,-6)},1);
        var leap=Pattern(BossPatternKind.DesperationLeap,new[]{Hit(1700,150,0,20),Hit(0,200,50,-6),Hit(900,150,0,20),Hit(0,200,1500,-6)});
        var triple=Pattern(BossPatternKind.TripleSlam,new[]{Hit(700,120,200),Hit(500,120,200),Hit(500,120,1200)});
        var pillar=Pattern(BossPatternKind.Pillar,new[]{Hit(700,120,600)});
        var sweep=Pattern(BossPatternKind.Sweep,new[]{Hit(500,100,0),Hit(250,100,600)});
        var all=new[]{charge,ground,leap,triple,pillar,sweep};
        BossCombat Combat(BossPoise b)=>new BossCombat(b,all,(min,max)=>min);
        PlayerActor Player()=>new PlayerActor(player,Enumerable.Range(0,5).Select(_=>Hit(1,1,0)).ToArray(),true,100);
        var boss=Boss();var combat=Combat(boss);
        check(!combat.TryStart(0),"2페이즈 패턴 1페이즈 거부");
        boss.ReceiveHit(250,0);
        check(boss.IsTransitioning&&boss.Phase.ID==4002&&boss.IsInvulnerable&&!boss.ReceiveHit(1,1)&&!combat.TryStart(0),"HP50 전환·무적·패턴 차단");
        check(boss.CompletePhaseTransition()&&!boss.CompletePhaseTransition()&&boss.Poise==120,"전환 완료 1회·체세 전량");
        boss=Boss();boss.ReceiveHit(250,120);
        check(boss.IsExposed&&boss.Phase.ID==4001&&!boss.IsTransitioning,"급소 노출과 동시 문턱이면 전환 보류");
        boss.Tick(1999);check(!boss.IsTransitioning,"노출 종료 전 전환 없음");boss.Tick(1);
        check(boss.IsTransitioning&&boss.Phase.ID==4002,"노출 종료 직후 전환");
        boss=Boss();boss.ReceiveHit(200,120);boss.TryBeginExecution();boss.CompleteExecution(true);
        check(boss.Health==210&&boss.IsTransitioning,"처형 피해는 시작 페이즈·완료 뒤 전환");
        boss=Boss(true);combat=Combat(boss);combat.TryStart(0);combat.Tick(1000);combat.Tick(10000);
        check(combat.IsCharging&&combat.Velocity==1200,"기어 돌진 시간 초과에도 실제 벽까지 유지");
        check(combat.NotifyWallHit()&&boss.Poise==90&&boss.CurrentState==StateKey.Stun&&!combat.IsAttackActive,"벽 충돌 체세30·경직");
        combat.Tick(799);check(combat.IsAttacking,"벽 경직 800ms 이전");combat.Tick(1);
        check(!combat.IsAttacking&&combat.IsWaiting,"벽 경직 뒤 600ms 대기");combat.Tick(599);check(!combat.TryStart(4),"600ms 대기 이전");combat.Tick(1);check(combat.TryStart(4),"600ms 대기 경계");
        boss=Boss(true);combat=Combat(boss);combat.TryStart(1);var target=Player();combat.Tick(900);
        check(combat.TryHitPlayer(1,target,false,false),"땅찍기 직격");combat.Tick(150);
        check(combat.HitIndex==1&&combat.IsAttackActive,"직격 바로 다음 같은 ms에 울림 활성");
        target.Tick(1000);check(!combat.TryHitPlayer(1,target,false,false),"피격 무적 만료해도 같은 시전 직격/울림 중복 없음");
        check(combat.TryHitPlayer(2,Player(),false,false),"직격 안 맞은 대상은 울림 가능");
        boss=Boss(true);combat=Combat(boss);check(!combat.TryStart(2),"HP25 이전 발악 직접 요청도 거부");boss.ReceiveHit(375,0);combat.TryStart(2);int jumps=0,lands=0;combat.AirborneChanged+=air=>{if(air)jumps++;else lands++;};
        combat.Tick(799);check(!combat.IsAirborne,"뛰기 예고800ms 전 지상");combat.Tick(1);
        check(combat.IsAirborne&&boss.IsInvulnerable&&!boss.ReceiveHit(1,1)&&!combat.IsAttackActive,"도약 공중 무적·무판정");combat.Tick(899);check(combat.IsAirborne,"공중900ms 이전");combat.Tick(1);
        check(!combat.IsAirborne&&!boss.IsInvulnerable&&combat.IsAttackActive,"공중900ms 경계 착지");combat.Tick(400);
        check(combat.HitIndex==2&&combat.IsAirborne,"첫 착지400ms 뒤 두 번째 도약");combat.Tick(900+150+200);
        check(!combat.IsAttackActive&&combat.IsAttacking&&jumps==2&&lands==2,"두 번 도약·착지 후 회복");combat.Tick(1499);check(combat.IsAttacking,"최종 회복1500ms 이전");combat.Tick(1);check(!combat.IsAttacking,"최종 회복1500ms 경계");
        boss=Boss(true);combat=Combat(boss);combat.TryStart(3);combat.Face(-1);combat.Tick(1020);
        check(combat.HitIndex==1&&combat.Facing==-1,"연타 새 타격 시작에만 추적 방향 변경");
        foreach(int height in new[]{200,400})check(BossHitGeometry.Contains(new BossHitArea(300,600,false,0),300,height,height+100,1),"기둥 +2/+4 발판 도달");
        foreach(int height in new[]{200,400})check(!BossHitGeometry.Contains(new BossHitArea(400,200,false,0),100,height,height+100,1),"휩쓸기 +2/+4 안전");
        check(BossHitGeometry.Contains(new BossHitArea(200,300,true,0),100,200,300,1)&&!BossHitGeometry.Contains(new BossHitArea(200,300,true,0),100,400,500,1),"착지직격 +2 위험·+4 안전");
        check(!BossHitGeometry.Contains(new BossHitArea(600,100,true,0),0,100,200,1),"점프로 땅 울림 회피");
        boss=Boss(true);combat=Combat(boss);var select=new BossPhaseTwoSelector(combat);
        check(all[select.Choose(100,0)].Kind==BossPatternKind.CrawlCharge,"전환 첫 패턴 기어돌진");select.Started(charge);
        check(select.Choose(700,0)==-1,"돌진 재사용 전 멀리서는 접근");select.Started(pillar);select.Started(sweep);
        check(all[select.Choose(700,0)].Kind==BossPatternKind.GroundSlam,"다른3패턴 뒤 광역 우선");select.Started(ground);
        check(all[select.Choose(700,0)].Kind==BossPatternKind.CrawlCharge,"다른2패턴 뒤 돌진 재사용");
        boss.ReceiveHit(375,0);check(all[select.Choose(100,0)].Kind==BossPatternKind.DesperationLeap,"HP25 문턱 발악 즉시 우선");
        select.Started(leap);select.Started(pillar);select.Started(triple);select.Started(sweep);
        check(all[select.Choose(100,0)].Kind==BossPatternKind.GroundSlam,"발악 후 다른3패턴 뒤 광역과 교대");
        int healed=0;target=Player();target.ConfigureHerbs(5,100,3);target.HerbRecovered+=()=>healed++;target.ReceiveHit(10,false);target.Tick(1000);
        check(target.TryUseHerb(),"회복 섭취 시작");target.Tick(99);check(healed==0,"섭취 완료 전 VFX 알림 없음");target.Tick(1);check(healed==1,"회복 완료 알림 1회");
        target.Tick(1000);target.TryUseHerb();target.ReceiveHit(1,false);target.Tick(1000);check(healed==1,"피격으로 취소한 섭취는 VFX 알림 없음");
    }
}
