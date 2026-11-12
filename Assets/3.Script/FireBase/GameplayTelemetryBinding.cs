using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

namespace HumanGhost.Telemetry
{
    // 기존 완료 이벤트만 구독합니다. 게임 명령·저장·정의 수정은 수행하지 않습니다.
    public class GameplayTelemetryBinding : IDisposable
    {
        private readonly TelemetryRecorder _recorder;
        private readonly PlayerActor _player;
        private readonly MonsterRewards _rewards;
        private readonly Func<IDictionary<string, object>> _context;
        private readonly Dictionary<int, int> _balances;
        private bool _disposed, _dead;
        public GameplayTelemetryBinding(TelemetryRecorder recorder, PlayerActor player, MonsterRewards rewards,
            Func<IDictionary<string, object>> context)
        {
            _recorder = recorder; _player = player; _rewards = rewards; _context = context;
            _balances = new Dictionary<int, int>();
            foreach (var pair in rewards.Wallet.CaptureBalances()) _balances.Add(pair.Key, pair.Value);
            _dead = player.IsDead;
            if (recorder.IsEnabled("player_death") || recorder.IsEnabled("stage_leave")) player.HealthChanged += OnHealth;
            if (recorder.IsEnabled("currency_changed")) rewards.Wallet.Changed += OnCurrency;
            if (recorder.IsEnabled("level_up")) rewards.LeveledUp += OnLevel;
            if (recorder.IsEnabled("monster_defeated")) rewards.Granted += OnMonster;
            Record("stage_enter", fields => { fields["level"] = rewards.CurrentLevel.Level; fields["experience"] = rewards.LevelExperience; });
            foreach (var pair in _balances)
                Record("currency_snapshot", fields => { fields["currency_id"] = pair.Key; fields["balance"] = pair.Value; });
        }
        private void Record(string name, Action<IDictionary<string, object>> fill)
        {
            if (_disposed) return;
            try { var fields = _context(); fill(fields); _recorder.Record(name, fields); }
            catch (Exception) { ContextFailureCount++; }
        }
        public int ContextFailureCount { get; private set; }
        private void OnHealth(int health)
        {
            bool died = health == 0 && !_dead; _dead = health == 0;
            if (died) Record("player_death", fields => fields["level"] = _rewards.CurrentLevel.Level);
        }
        private void OnCurrency(int id, int balance)
        {
            if (_disposed) return;
            int previous = _balances.TryGetValue(id, out int value) ? value : 0;
            _balances[id] = balance;
            Record("currency_changed", fields => { fields["currency_id"] = id; fields["delta"] = (long)balance - previous; fields["balance"] = balance; });
        }
        private void OnLevel(int before, int after) => Record("level_up", fields => { fields["before_level"] = before; fields["level"] = after; fields["experience"] = _rewards.LevelExperience; });
        private void OnMonster(MonsterSpawnId spawn, MonsterReward reward) => Record("monster_defeated", fields =>
        {
            fields["stage_id"] = spawn.StageId; fields["placement_id"] = spawn.PlacementId;
            fields["sequence"] = spawn.Sequence; fields["monster_id"] = reward.MonsterID;
        });
        // 경로 선택 확정 이벤트의 소비자가 호출합니다. 이동 요청·도착 보상을 선택으로 추정하지 않습니다.
        public void RecordRouteChoice(int junctionId, int routeId, int destinationStageId)
        {
            if (junctionId <= 0 || routeId <= 0 || destinationStageId <= 0) return;
            Record("route_selected", fields =>
            {
                fields["junction_id"] = junctionId; fields["route_id"] = routeId;
                fields["destination_stage_id"] = destinationStageId; fields["level"] = _rewards.CurrentLevel.Level;
            });
        }

        // 아이템은 공급 기능의 완료 이벤트가 준비될 때 연결합니다.
        public void RecordItem(bool consumed, int itemId, int quantity)
        {
            if (itemId <= 0 || quantity <= 0) return;
            Record(consumed ? "item_consumed" : "item_acquired",
                fields => { fields["item_id"] = itemId; fields["quantity"] = quantity; });
        }
        public void Dispose()
        {
            if (_disposed) return;
            Record("stage_leave", fields => fields["dead"] = _dead);
            _disposed = true;
            _player.HealthChanged -= OnHealth; _rewards.Wallet.Changed -= OnCurrency;
            _rewards.LeveledUp -= OnLevel; _rewards.Granted -= OnMonster;
        }
    }
}
