using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 도착 영역만 관측합니다. 정의·재화·지급 기록은 조립자가 주입한 명령이 소유합니다.
    // 플레이어는 물리 몸체 없이 자체 충돌로 움직이므로 트리거 이벤트 대신 위치로 영역 진입을 확인합니다.
    [RequireComponent(typeof(BoxCollider2D))]
    public class GalimgilArrival : MonoBehaviour
    {
        private bool _inside;
        private PlayerRuntime _player;
        private BoxCollider2D _area;
        private Action _arrived;
        private bool _ready;
        private SubscriptionScope _scope;
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        public void Connect(PlayerRuntime player, Action arrived)
        {
            Disconnect();
            _area = GetComponent<BoxCollider2D>();
            if (!_area.isTrigger) throw new InvalidOperationException("갈림길 도착 영역은 Is Trigger를 켜야 합니다.");
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _arrived = arrived ?? throw new ArgumentNullException(nameof(arrived));
            _scope = new SubscriptionScope();
            Action destroyed = _scope.Guard(Disconnect);
            _scope.Add(() => player.Destroyed -= destroyed); player.Destroyed += destroyed;
        }
        public void SetReady(bool ready)
        {
            if (_ready == ready) return;
            _ready = ready;
            _inside = IsPlayerInside();
            if (ready && _inside) Arrive();
        }
        private void Update()
        {
            bool inside = IsPlayerInside();
            if (inside && !_inside && _ready) Arrive();
            _inside = inside;
        }
        private bool IsPlayerInside()
        {
            if (_player == null || _area == null || !_area.enabled) return false;
            Bounds bounds = _area.bounds;
            Vector3 position = _player.transform.position;
            return bounds.Contains(new Vector3(position.x, position.y, bounds.center.z));
        }
        private void Arrive()
        {
            if (!isActiveAndEnabled || _area == null || !_area.enabled || _player == null || !_player.isActiveAndEnabled || _player.Actor == null || _player.Actor.IsDead) return;
            _arrived?.Invoke();
        }
        public void Disconnect()
        {
            _ready = false; _inside = false; _scope?.Dispose(); _scope = null; _player = null; _area = null; _arrived = null;
        }
        private void OnDisable() => Disconnect();
    }
}
