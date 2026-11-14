using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;
internal static class ExecutionRewardChecks
{
    public static void Run(Player definition, Action<bool,string> check)
    {
        var rows=new[]{new ExecuteReward(3301,3001,1,5,1,0),new ExecuteReward(3302,3002,1,5,1,0)};
        var steps=new[]{new CorruptionStep(23001,0,0,0),new CorruptionStep(23002,1,10,2500),new CorruptionStep(23003,2,20,5000),new CorruptionStep(23004,3,30,7500)};
        var session=new ExecutionRewardSession(rows,steps);
        check(session.Corruption==0&&session.InkAlpha==0,"독립 세션 오염0단계");
        var id=new MonsterSpawnId(8002,101,1);ExecutionRewardPlan plan;
        check(session.TryPrepare(id,3001,out plan)&&plan.Reward.CoinDrop==1&&plan.Reward.HealHP==5&&session.Corruption==0,"보상 준비만으로 재화/HP/오염 변경 없음");
        check(session.TryCommit(plan)&&session.Corruption==1&&!session.TryCommit(plan)&&!session.TryPrepare(id,3001,out _),"같은 스폰 처형 보상 1회");
        check(!session.TryPrepare(new MonsterSpawnId(8002,101,2),1006,out _),"미등록 보스 보상 생성 없음");
        ExecutionRewardPlan first,stale;session.TryPrepare(new MonsterSpawnId(8002,101,2),3001,out first);session.TryPrepare(new MonsterSpawnId(8002,101,3),3002,out stale);
        check(session.TryCommit(first)&&!session.TryCommit(stale),"동시에 준비한 오래된 계획은 재준비 필요");
        for(int i=3;i<=31;i++){
            session.TryPrepare(new MonsterSpawnId(8002,101,i),3001,out plan);check(session.TryCommit(plan),"각 스폰 독립 처형 확정");
            int n=session.Corruption;check(session.CurrentStep.Step==(n<10?0:n<20?1:n<30?2:3),"10/20/30 오염 문턱");
        }
        var snapshot=session.Capture();var restored=new ExecutionRewardSession(rows,steps,snapshot);
        check(restored.Corruption==31&&restored.InkAlpha==7500&&!restored.TryPrepare(id,3001,out _),"독립 스냅샷 복원은 오염·중복 기록 함께");
        check(!new ExecutionRewardSession(rows,steps).CanCommit(plan),"다른 세션 계획 재사용 거부");
        bool rejected=false;try{new ExecutionRewardSnapshot(-1);}catch(ArgumentOutOfRangeException){rejected=true;}check(rejected,"음수 저장 오염 거부");
        rejected=false;try{new ExecutionRewardSession(rows,new[]{new CorruptionStep(1,0,0,10001)});}catch(ArgumentException){rejected=true;}check(rejected,"잘못된 먹 투명도 거부");
        var currencies = new[] { new Currency(1, 1, 5000, true), new Currency(2, 2, 5000, true) };
        var rewards = new MonsterRewards(currencies, new[] { new MonsterReward(3101,3001,2,3,2) },
            new[] { new PlayerLevel(17001,1,100,0,0,0,0,true), new PlayerLevel(17002,2,100,0,0,0,0,true) });
        var progress = new GameProgressState(); progress.ConnectRewards(rewards);
        session = new ExecutionRewardSession(rows,steps); progress.ConnectExecutions(session);
        var player = new PlayerActor(definition,Enumerable.Range(0,5).Select(_=>new AttackSpec(1,1,0,0,0,0,0)).ToArray(),true,definition.StaminaMax);
        player.ConfigureHerbs(10,800,3); player.RestoreVitals(player.MaxHealth-10,3);
        int recovered=0,granted=0; player.ExecutionRecovered += ()=>recovered++;
        rewards.Wallet.Changed += (currency,balance) => check(progress.HasDefeatedMonster(id) && session.Corruption==1 &&
            player.Health==player.MaxHealth-5 && rewards.Experience==2,"재화 알림 전 일반·추가 보상·HP·오염 모두 확정");
        rewards.ExecutionGranted += _=>granted++;
        check(rewards.TryGrant(id,3001,progress,session,player) && rewards.Wallet.GetBalance(2)==3 &&
            rewards.Wallet.GetBalance(1)==3 && recovered==1 && granted==1,"처형은 기본 보상과 추가 엽전·회복을 한 번 지급");
        check(!rewards.TryGrant(id,3001,progress,session,player) && recovered==1 && granted==1,"사망 알림 중복에도 추가 보상 한 번");
        var saved = new SaveSnapshot(8002,8002,100,0,player.Health,3,Array.Empty<int>(),new[]{8002},Array.Empty<int>(),Array.Empty<int>(),Array.Empty<int>(),
            progress.DefeatedMonsters,rewards.Capture(),executions:session.Capture());
        check(saved.WithOpenedGates(new[]{1}).Executions.Corruption==1 && saved.WithSavePoints(new[]{1}).Executions.Claimed.Single().Equals(id),"저장 사본 복사에 처형 보상 기록 보존");
        session.TryPrepare(new MonsterSpawnId(8002,101,99),3001,out plan);
        progress.RestartAfterDeath(saved);
        check(session.Corruption==1 && !session.CanCommit(plan) && !session.TryPrepare(id,3001,out _),"사망은 저장된 오염·처형 기록을 함께 복원하고 이전 계획 무효화");
        var overflowRewards = new MonsterRewards(currencies,new[]{new MonsterReward(3101,3001,2,3,2)},new[]{new PlayerLevel(17001,1,100,0,0,0,0,true),new PlayerLevel(17002,2,100,0,0,0,0,true)});
        var overflowProgress = new GameProgressState(); overflowProgress.ConnectRewards(overflowRewards);
        var overflowSession = new ExecutionRewardSession(rows,steps);
        overflowRewards.Restore(new RewardSnapshot(0,new Dictionary<int,int>{{2,int.MaxValue-2}}));
        int beforeHP=player.Health;
        check(!overflowRewards.TryGrant(id,3001,overflowProgress,overflowSession,player) && player.Health==beforeHP &&
            overflowSession.Corruption==0 && !overflowProgress.HasDefeatedMonster(id) && overflowRewards.Experience==0,"추가 엽전 수용 실패는 HP·오염·처치·경험치도 미확정");
        var itemSession = new ExecutionRewardSession(new[]{new ExecuteReward(3301,3001,1,5,1,6001)},steps);
        rejected=false;try{overflowRewards.TryGrant(id,3001,overflowProgress,itemSession,player);}catch(InvalidOperationException){rejected=true;}
        check(rejected && itemSession.Corruption==0 && !overflowProgress.HasDefeatedMonster(id),"미연결 아이템을 지급 완료로 처리하지 않음");
    }
}
