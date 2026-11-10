using System;
using HumanGhost.Gameplay.Adapters;

namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        private GalimgilArrival[] _galimgilArrivals = Array.Empty<GalimgilArrival>();
        private void ConnectGalimgilArrivals()
        {
            DisconnectGalimgilArrivals();
            if (_stage == null) return;
            _galimgilArrivals = _stage.MapRoot.GetComponentsInChildren<GalimgilArrival>(true);
            foreach (var arrival in _galimgilArrivals) arrival.Connect(_player, GrantGalimgilReward);
        }
        private void GrantGalimgilReward()
        {
            if (!_subscribed || _saveLocked || _leaving || _actor == null || _actor.IsDead) return;
            GalimgilGrantResult result = _progress.Galimgil.TryGrantAtArrival(_progress.DefeatedMonsters, out _);
            if (result != GalimgilGrantResult.Granted && result != GalimgilGrantResult.AlreadyClaimed)
                throw new InvalidOperationException("갈림길 보상을 지급하지 못했습니다. " + (result switch
                {
                    GalimgilGrantResult.Busy => "다른 재화 변경을 처리하고 있습니다.",
                    GalimgilGrantResult.CurrencyUnavailable => "보상 재화가 비활성 상태이거나 연결되지 않았습니다.",
                    _ => "보상 또는 보유량이 정수 처리 범위를 넘습니다."
                }));
        }
        private void DisconnectGalimgilArrivals()
        {
            foreach (var arrival in _galimgilArrivals) if (arrival != null) arrival.Disconnect();
            _galimgilArrivals = Array.Empty<GalimgilArrival>();
        }
    }
}
