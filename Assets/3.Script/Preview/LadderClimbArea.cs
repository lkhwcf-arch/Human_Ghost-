using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class LadderClimbArea : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D _volume = null;
        [SerializeField, Min(0.1f)] private float _tilesPerSecond = 3f;
        public Bounds Area => (_volume != null ? _volume : _volume = GetComponent<BoxCollider2D>()).bounds;
        public float TilesPerSecond => _tilesPerSecond;
        private void Awake()
        {
            if (_volume == null) _volume = GetComponent<BoxCollider2D>();
            if (!_volume.isTrigger || _tilesPerSecond <= 0f || float.IsNaN(_tilesPerSecond) || float.IsInfinity(_tilesPerSecond))
                throw new InvalidOperationException("사다리는 트리거 영역과 양수 이동 속도가 필요합니다.");
        }
    }
}
