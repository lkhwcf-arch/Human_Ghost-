using System;
using System.Collections.Generic;
using HumanGhost.Telemetry;

namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        private GameplayTelemetryBinding _telemetry;
        private void ConnectTelemetry()
        {
            if (_telemetry != null || TelemetryHost.Current?.Recorder == null) return;
            try
            {
                _telemetry = new GameplayTelemetryBinding(TelemetryHost.Current.Recorder, _actor, _progress.Rewards, () =>
                    new Dictionary<string, object> { ["stage_id"] = _stageId,
                        ["position_x"] = _player.Position.X, ["position_y"] = _player.Position.Y });
            }
            catch (Exception) { _telemetry?.Dispose(); _telemetry = null; }
        }
        // 갈림길 번호는 출구의 출발 StageID, 경로 번호는 StageExit.RouteID입니다. 0은 통계 대상이 아닙니다.
        // 통계 기록은 자체 경계에서 실패를 흡수하므로 이동 성공을 되돌리지 않습니다.
        private void RecordRouteChoice(HumanGhost.Data.Definitions.StageExit route)
        {
            if (route.RouteID > 0) _telemetry?.RecordRouteChoice(route.StageID, route.RouteID, route.TargetStageID);
        }
        private void DisconnectTelemetry()
        {
            _telemetry?.Dispose(); _telemetry = null;
            TelemetryHost.Current?.Flush();
        }
    }
}
