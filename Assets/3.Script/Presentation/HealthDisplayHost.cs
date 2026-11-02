using System;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Presentation
{
    // 씬 조립자가 준비한 의존성만 전달받습니다. 직접 데이터 로딩·플레이어 생성·장면 검색을 하지 않습니다.
    [DisallowMultipleComponent]
    public sealed class HealthDisplayHost : MonoBehaviour
    {
        private IReadOnlyDataCatalog _data;
        private HealthDisplayPrefabs _prefabs;
        private PlayerActor _player;
        private Transform _anchorRoot;
        private HealthDisplayGroup _group;

        public bool IsConnected => _group != null;

        public void Connect(IReadOnlyDataCatalog data, HealthDisplayPrefabs prefabs, PlayerActor player, Transform anchorRoot)
        {
            if (this == null) throw new InvalidOperationException("제거된 체력 표시 연결 컴포넌트는 사용할 수 없습니다.");
            if (data == null) throw new ArgumentNullException(nameof(data), "준비된 데이터가 필요합니다.");
            if (prefabs == null) throw new ArgumentNullException(nameof(prefabs), "체력 표시 프리팹 목록이 필요합니다.");
            if (player == null) throw new ArgumentNullException(nameof(player), "준비된 플레이어가 필요합니다.");
            if (anchorRoot == null) throw new ArgumentNullException(nameof(anchorRoot), "생성 완료된 앵커 묶음이 필요합니다.");
            Disconnect();
            _data = data;
            _prefabs = prefabs;
            _player = player;
            _anchorRoot = anchorRoot;
            try { if (isActiveAndEnabled) CreateGroup(); }
            catch { Disconnect(); throw; }
        }

        public void Disconnect()
        {
            ReleaseGroup();
            _data = null;
            _prefabs = null;
            _player = null;
            _anchorRoot = null;
        }

        private void OnEnable()
        {
            if (_data == null) return;
            try { CreateGroup(); }
            catch (Exception error)
            {
                Disconnect();
                Debug.LogError($"체력 표시 재연결에 실패했습니다.\n{error}", this);
            }
        }

        // 일시 비활성화는 의존성을 보관하고 표시만 정리합니다. 대상 교체·반환은 Disconnect로 구분합니다.
        private void OnDisable() => ReleaseGroup();
        private void OnDestroy() => Disconnect();

        private void CreateGroup()
            => _group ??= HealthDisplayGroup.Create(_data, _prefabs, _player, new SpawnAnchors(_anchorRoot));

        private void ReleaseGroup()
        {
            _group?.Dispose();
            _group = null;
        }
    }
}
