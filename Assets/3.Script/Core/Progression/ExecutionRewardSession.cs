using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;
namespace HumanGhost.Gameplay
{
    // 추가 보상 계획·오염·스폰별 확정을 소유하며 재화/HP의 적용은 MonsterRewards가 조립합니다.
    // 준비 -> 소비자의 원자적 적용 안에서 Commit -> 소비자가 완료 알림 순서입니다.
    public sealed class ExecutionRewardPlan
    {
        internal ExecutionRewardSession Owner { get; }
        internal int Revision { get; }
        public MonsterSpawnId Spawn { get; }
        public ExecuteReward Reward { get; }
        internal ExecutionRewardPlan(ExecutionRewardSession owner, int revision, MonsterSpawnId spawn, ExecuteReward reward)
        { Owner = owner; Revision = revision; Spawn = spawn; Reward = reward; }
    }
    public sealed class ExecutionRewardSnapshot
    {
        public int Corruption { get; }
        public IReadOnlyList<MonsterSpawnId> Claimed { get; }
        public ExecutionRewardSnapshot(int corruption = 0, IEnumerable<MonsterSpawnId> claimed = null)
        {
            if (corruption < 0) throw new ArgumentOutOfRangeException(nameof(corruption));
            var copy = (claimed ?? Array.Empty<MonsterSpawnId>()).ToArray();
            if (copy.Any(id => id.StageId <= 0 || id.PlacementId <= 0 || id.Sequence <= 0) || copy.Distinct().Count() != copy.Length)
                throw new ArgumentException("처형 보상 저장 식별자가 유효하지 않습니다.", nameof(claimed));
            Corruption = corruption; Claimed = Array.AsReadOnly(copy);
        }
    }
    public sealed class ExecutionRewardSession
    {
        private readonly Dictionary<int, ExecuteReward> _rewards;
        private readonly CorruptionStep[] _steps;
        private readonly HashSet<MonsterSpawnId> _claimed = new HashSet<MonsterSpawnId>();
        private int _revision;
        public int Corruption { get; private set; }
        public CorruptionStep CurrentStep => _steps.Last(step => step.MinCorruption <= Corruption);
        // 0~10000. UI는 단계/먹 번짐 표시만 소비하며 전투 수치를 바꾸지 않습니다.
        public int InkAlpha => CurrentStep.InkAlpha;
        public ExecutionRewardSession(IEnumerable<ExecuteReward> rewards, IEnumerable<CorruptionStep> steps, ExecutionRewardSnapshot snapshot = null)
        {
            if (rewards == null || steps == null) throw new ArgumentNullException(nameof(rewards));
            var rows = rewards.ToArray();
            if (rows.Any(row => row == null || row.ID <= 0 || row.MonsterID <= 0 || row.CoinDrop < 0 || row.HealHP < 0 || row.Corruption < 0 || row.DropItemID < 0))
                throw new ArgumentException("처형 보상 수량·식별자가 유효하지 않습니다.");
            _rewards = rows.ToDictionary(row => row.MonsterID);
            _steps = steps.OrderBy(row => row?.MinCorruption).ToArray();
            if (_steps.Length == 0 || _steps[0] == null || _steps[0].MinCorruption != 0 || _steps[0].Step != 0)
                throw new ArgumentException("오염 0단계가 필요합니다.");
            for (int i=0;i<_steps.Length;i++)
            {
                var step=_steps[i];
                if (step == null || step.ID <= 0 || step.Step != i || step.MinCorruption < 0 || step.InkAlpha < 0 || step.InkAlpha > 10000 ||
                    (i > 0 && (step.MinCorruption <= _steps[i-1].MinCorruption || step.InkAlpha < _steps[i-1].InkAlpha)))
                    throw new ArgumentException("오염 단계·임계값·표시 투명도를 확인하세요.");
            }
            Restore(snapshot ?? new ExecutionRewardSnapshot());
        }
        // 성공 처형이 확정된 소비자가 호출합니다. 취소/일반 사망/보스/테스트 허수아비는 공급하지 않습니다.
        public bool TryPrepare(MonsterSpawnId spawn, int monsterId, out ExecutionRewardPlan plan)
        {
            plan = null;
            if (spawn.StageId <= 0 || spawn.PlacementId <= 0 || spawn.Sequence <= 0) throw new ArgumentException("처형 대상 스폰 식별자가 필요합니다.", nameof(spawn));
            if (_claimed.Contains(spawn) || !_rewards.TryGetValue(monsterId, out var row) || (long)Corruption + row.Corruption > int.MaxValue) return false;
            plan = new ExecutionRewardPlan(this, _revision, spawn, row); return true;
        }
        public bool CanCommit(ExecutionRewardPlan plan) => plan != null && plan.Owner == this && plan.Revision == _revision && !_claimed.Contains(plan.Spawn);
        // 외부 알림·HP·재화 명령을 호출하지 않습니다. 준비한 재화/회복 적용이 실패하면 호출하지 않습니다.
        public bool TryCommit(ExecutionRewardPlan plan)
        {
            if (!CanCommit(plan)) return false;
            Corruption = checked(Corruption + plan.Reward.Corruption); _claimed.Add(plan.Spawn); _revision++;
            return true;
        }
        public ExecutionRewardSnapshot Capture() => new ExecutionRewardSnapshot(Corruption, _claimed.OrderBy(id => id.StageId).ThenBy(id => id.PlacementId).ThenBy(id => id.Sequence));

        // 게임 진입·사망 복원에서만 호출합니다. 이전 계획은 복원 뒤 재사용할 수 없습니다.
        public void Restore(ExecutionRewardSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Corruption = snapshot.Corruption;
            _claimed.Clear(); _claimed.UnionWith(snapshot.Claimed); _revision++;
        }
    }
}
