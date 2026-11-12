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
        private void DisconnectTelemetry()
        {
            _telemetry?.Dispose(); _telemetry = null;
            TelemetryHost.Current?.Flush();
        }
    }
}
