using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class MonsterAwarenessChecks
{
    public static void Run(Action<bool,string> check)
    {
        var rules = new MonsterAggro(3401,3001,300,1100,600,2000,1200,1000);
        var awareness = new MonsterAwareness(800,rules);
        awareness.Observe(500,400,0); check(!awareness.IsEngaged,"높이 4칸은 시야 밖");
        awareness.Observe(800,300,0); check(awareness.IsEngaged,"가로8·높이3칸 경계 인식");
        awareness.Observe(1101,0,600); check(!awareness.Advance(1999,false) && awareness.IsEngaged,"해제1999ms 유지");
        awareness.Observe(1000,0,600); awareness.Advance(1,false);
        awareness.Observe(1101,0,600); check(!awareness.Advance(1999,false),"다시 들어오면 해제 시간 초기화");
        check(awareness.Advance(1,false) && awareness.IsReturning && !awareness.IsEngaged,"2초 밖이면 복귀");
        awareness.Observe(500,0,500); awareness.Advance(999,false); check(!awareness.IsEngaged,"복귀 후999ms 재인식 금지");
        awareness.Advance(1,false); check(awareness.IsEngaged && !awareness.IsReturning,"1000ms 뒤 재인식");
        awareness.Observe(500,0,1201); check(!awareness.Advance(1,true) && awareness.IsEngaged,"공격 중 leash 해제 보류");
        check(awareness.Advance(0,false) && awareness.IsReturning,"공격 종료 뒤12칸 leash 즉시 복귀");
        awareness.NotifyHit(); check(awareness.IsEngaged,"피격은 재인식 대기 무시");
        check(awareness.LoseTarget() && !awareness.LoseTarget() && !awareness.IsEngaged,"대상 사망 해제는 반복 초기화하지 않음");
        awareness.NotifyHit(); check(!awareness.IsEngaged,"사망 대상을 피격으로 재인식하지 않음");
        awareness.Reset(); awareness.Observe(500,0,100); awareness.Observe(500,601,100);
        check(awareness.Advance(2000,false) && awareness.IsReturning,"높이6칸 밖에서도2초 해제");

        awareness.Reset(); awareness.Observe(50,0,500,false);
        check(!awareness.IsEngaged, "다른 플랫폼/화면 밖은 가까워도 최초 인식하지 않음");
        awareness.NotifyHit(); awareness.Advance(10000,false);
        check(!awareness.IsEngaged, "도달 불가 대상은 피격·시간 경과로 재인식하지 않음");
        awareness.Observe(50,0,500,true);
        check(awareness.IsEngaged, "실제 도달 가능 관측부터 인식");
        awareness.Observe(50,0,500,false);
        check(!awareness.IsEngaged && awareness.IsReturning, "거리 유지 상태도 공간 이탈 즉시 해제");
        awareness.Advance(999,true); awareness.Observe(50,0,500,true);
        check(!awareness.IsEngaged, "공간 이탈 후 재인식 대기 999ms");
        awareness.Advance(1,true);
        check(awareness.IsEngaged, "실제 화면/같은 플랫폼과 재인식 대기 1000ms 모두 충족");

        var common = new UnitStat(1002,0,"AwarenessFixture",null,100,15,0,0,0,0,24,200,0);
        var definition = new Monster(common,3001,1002,0,0,0,0,1000,"KILL",0,false,"HUSUGWI",800,100,true);
        MonsterActor Enemy() => new MonsterActor(definition,new[]{new MonsterPattern(StateKey.Attack1,100,new AttackSpec(100,100,100,0,0,0,0))},300,500,(min,max)=>min,keepHealth:false,aggro:rules);
        var enemy = Enemy(); enemy.Observe(2001,500,1,400,0,0);
        check(enemy.CurrentState==StateKey.Idle && enemy.Velocity==0,"일반몹 높이 관측을 AI에 연결");
        enemy.Observe(2001,500,1,0,600,-1); check(enemy.HasAggro && enemy.Velocity==200,"시야 안이면 추적");
        enemy.ReceiveHit(4,poiseDamage:5); enemy.Tick(300);
        enemy.Observe(2001,1500,1,0,600,-1); enemy.Tick(2000);
        check(enemy.IsReturning && enemy.Facing==-1 && enemy.Velocity==-200 && enemy.Health==96 && enemy.Poise==24,"복귀는 스폰 방향 걷기·HP 보존·체세 회복");
        enemy.Observe(2001,1500,1,0,0,0); check(!enemy.IsReturning && enemy.Velocity==0,"스폰 위치에서 대기");
        enemy = Enemy(); enemy.Observe(2001,50,1,0,500,-1); enemy.Observe(2001,1500,1,0,1300,-1); enemy.Tick(50);
        check(enemy.CurrentPattern!=null && !enemy.IsReturning,"공격 선딜·판정·후딜 중 강제 복귀 없음");
        enemy.Tick(250); check(enemy.IsReturning && enemy.CurrentPattern==null,"공격 완료 뒤 복귀");
        enemy = Enemy(); enemy.Observe(2001,500,1,0,600,-1); enemy.ReceiveHit(0,poiseDamage:24);
        enemy.Observe(2001,1500,1,700,1300,-1); enemy.Tick(999); check(enemy.IsExposed && !enemy.IsReturning,"그로기 중 해제 보류");
        enemy.Tick(1); check(enemy.IsReturning && enemy.Poise==24,"그로기 종료 뒤 복귀");
        enemy = Enemy(); enemy.Observe(2001,50,1,0,600,-1); enemy.ClearTarget();
        check(enemy.IsReturning && enemy.CurrentPattern==null && !enemy.IsAttackActive,"플레이어 사망은 공격을 정리하고 복귀");
        foreach (int elapsed in new[] {0,100,199})
        {
            enemy=Enemy(); enemy.Observe(2001,50,1,0,500,-1); enemy.Tick(elapsed);
            enemy.Observe(2001,50,1,0,500,-1,false);
            check(!enemy.HasAggro && enemy.IsReturning && enemy.CurrentPattern==null && !enemy.IsAttackActive,
                "공간 이탈은 선딜/판정창 중에도 공격 해제: "+elapsed);
        }
        enemy=Enemy(); enemy.Observe(2001,500,1,0,500,-1); enemy.ReceiveHit(4,poiseDamage:5);
        enemy.Observe(2001,50,1,0,500,-1,false);
        check(enemy.Health==96 && enemy.Poise==19 && enemy.CurrentState==StateKey.Hit && !enemy.HasAggro,
            "공간 이탈로 HP·체세·피격 시간을 초기화하지 않음");
        enemy=Enemy(); enemy.Observe(2001,500,1,0,500,-1); enemy.ReceiveHit(0,poiseDamage:24);
        enemy.Observe(2001,50,1,0,500,-1,false);
        check(enemy.IsExposed && enemy.Poise==0 && !enemy.HasAggro, "공간 이탈로 처형 기회를 취소하지 않음");
        enemy=Enemy(); enemy.Observe(2001,50,1,0,0,0,false);
        enemy.ReceiveHit(4); enemy.Tick(300);
        check(!enemy.HasAggro && enemy.CurrentPattern==null, "다른 플랫폼 피격 뒤에도 공격을 새로 시작하지 않음");
    }
}
