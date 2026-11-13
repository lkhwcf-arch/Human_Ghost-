using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;
internal static class ExecutionRewardChecks
{
    public static void Run(Action<bool,string> check)
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
    }
}
