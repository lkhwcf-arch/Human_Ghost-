using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class LadderClimbArea : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D _volume = null;
        [SerializeField, Min(10)] private int _speedUnitsPerSecond = 300;
        public Bounds Area => (_volume != null ? _volume : _volume = GetComponent<BoxCollider2D>()).bounds;
        public int SpeedUnitsPerSecond => _speedUnitsPerSecond;
        private void Awake()
        {
            if (_volume == null) _volume = GetComponent<BoxCollider2D>();
            if (!_volume.isTrigger || _speedUnitsPerSecond <= 0)
                throw new InvalidOperationException("사다리는 트리거 영역과 양수 이동 속도가 필요합니다.");
        }
    }
}
