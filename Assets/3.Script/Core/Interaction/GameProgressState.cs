using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // 새 게임마다 생성하며 씬은 같은 인스턴스를 전달받습니다. 조회 결과로 내부 목록을 변경할 수 없습니다.
    public class GameProgressState
    {
        private readonly HashSet<int> _openedGates = new HashSet<int>();
        private readonly HashSet<int> _activeShrines = new HashSet<int>();
        private readonly HashSet<int> _viewedDialogues = new HashSet<int>();
        public bool HasViewedDialogue(int id) => _viewedDialogues.Contains(id);
        public void MarkDialogueViewed(int id)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            _viewedDialogues.Add(id);
        }
        private readonly HashSet<int> _viewedCutscenes = new HashSet<int>();
        private readonly HashSet<MonsterSpawnId> _defeatedMonsters = new HashSet<MonsterSpawnId>();
        public StageProgress Progress { get; private set; } = new StageProgress();
        public MonsterRewards Rewards { get; private set; }
        public UpgradeSession Upgrades { get; private set; }
        public GalimgilRewards Galimgil { get; private set; }
        public ExecutionRewardSession Executions { get; private set; }
        public void ConnectExecutions(ExecutionRewardSession executions)
        {
            if (Executions != null) throw new InvalidOperationException("처형 보상은 이미 연결됐습니다.");
            Executions = executions ?? throw new ArgumentNullException(nameof(executions));
        }
        public void ConnectGalimgil(GalimgilRewards galimgil)
        {
            if (Galimgil != null) throw new InvalidOperationException("갈림길 보상은 이미 연결됐습니다.");
            Galimgil = galimgil ?? throw new ArgumentNullException(nameof(galimgil));
        }
        public void ConnectUpgrades(UpgradeSession upgrades)
        {
            if (Upgrades != null) throw new InvalidOperationException("세션의 강화는 이미 연결됐습니다.");
            Upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
        }
        public void ConnectRewards(MonsterRewards rewards)
        {
            if (Rewards != null) throw new InvalidOperationException("세션의 자동 보상은 이미 연결됐습니다.");
            Rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        }
        public int CurrentStage { get; private set; } = StageExitRules.VillageStage;
        public int ShrineId { get; private set; }
        public bool CheckpointPassed => ShrineId != 0;
        public string ArrivalAnchor { get; private set; }
        public int? CarriedHealth { get; private set; }
        public int CarriedHerbs { get; private set; }
        public int[] OpenedGates => Copy(_openedGates);
        public int[] ActiveShrines => Copy(_activeShrines);
        public int[] ViewedCutscenes => Copy(_viewedCutscenes);
        public MonsterSpawnId[] DefeatedMonsters => Copy(_defeatedMonsters);
        public bool HasOpenedGate(int id) => _openedGates.Contains(id);
        public bool HasDefeatedBoss(int id) => Progress.IsCleared(id);
        public bool HasActiveShrine(int id) => _activeShrines.Contains(id);
        public bool HasDefeatedMonster(MonsterSpawnId id) => _defeatedMonsters.Contains(id);
        public void OpenGate(int id) => _openedGates.Add(id);
        public void MarkBossDefeated(int id) => Progress.Clear(id);
        public void MarkMonsterDefeated(MonsterSpawnId id) => _defeatedMonsters.Add(id);
        public void MarkCutsceneViewed(int id) => _viewedCutscenes.Add(id);
        public void ActivateShrine(int id) { ShrineId = id; _activeShrines.Add(id); }
        public void Travel(int stageId, string anchor, int health, int herbs)
        {
            CurrentStage = stageId; ArrivalAnchor = anchor; CarriedHealth = health; CarriedHerbs = herbs;
        }
        public string TakeArrival() { string anchor = ArrivalAnchor; ArrivalAnchor = null; return anchor; }
        public void ClearCarriedVitals() { CarriedHealth = null; CarriedHerbs = 0; }
        public void Restore(SaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Rewards?.Restore(snapshot.Rewards);
            Upgrades?.Restore(snapshot.Upgrades);
            Galimgil?.Restore(snapshot.Galimgil);
            Executions?.Restore(snapshot.Executions);
            Progress = new StageProgress();
            foreach (int id in snapshot.ClearedStages) Progress.Clear(id);
            Replace(_openedGates, snapshot.OpenedGates);
            Replace(_activeShrines, snapshot.ActiveShrines);
            Replace(_viewedCutscenes, snapshot.ViewedCutscenes);
            Replace(_defeatedMonsters, snapshot.DefeatedMonsters);
            CurrentStage = snapshot.StageId; ShrineId = snapshot.ShrineId;
            ArrivalAnchor = null; ClearCarriedVitals();
        }
        public void RestartAfterDeath(SaveSnapshot lastSaved, int? stageAfterLedger = null)
        {
            if (stageAfterLedger.HasValue && stageAfterLedger.Value <= 0) throw new ArgumentOutOfRangeException(nameof(stageAfterLedger));
            if (Rewards != null && !Rewards.ApplyDeathLoss()) throw new InvalidOperationException("사망 재화 손실을 적용하지 못했습니다.");
            Executions?.Restore(lastSaved?.Executions ?? new ExecutionRewardSnapshot());
            _defeatedMonsters.Clear();
            if (lastSaved != null) _defeatedMonsters.UnionWith(lastSaved.DefeatedMonsters);
            ClearCarriedVitals();
            CurrentStage = CheckpointPassed ? ShrineId : StageExitRules.VillageStage;
            ArrivalAnchor = CheckpointPassed ? "Respawn_Seonangdang" : null;
            // 조사 완료 뒤의 초반 사망은 장부방을 반복하지 않습니다. 후반 서낭당은 우선 유지합니다.
            if (stageAfterLedger.HasValue && Progress.IsCleared(StageExitRules.LedgerStage)
                && (CurrentStage == StageExitRules.VillageStage || CurrentStage == StageExitRules.LedgerStage))
            {
                CurrentStage = stageAfterLedger.Value;
                ArrivalAnchor = null;
            }
        }
        private static T[] Copy<T>(HashSet<T> source) { var result = new T[source.Count]; source.CopyTo(result); return result; }
        private static void Replace<T>(HashSet<T> target, IEnumerable<T> source) { target.Clear(); target.UnionWith(source); }
    }
}
