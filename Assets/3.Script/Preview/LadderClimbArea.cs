using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class LadderClimbArea : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D _volume = null;
        [SerializeField, Min(10)] private int _speedUnitsPerSecond = 300;
        [SerializeField, InspectorName("사다리 그림 축 보정 (tile_x100)")] private int _axisOffsetUnits = 0;
        // [정수화 미적용 이유] 사다리 중심축의 월드 X(Collider 범위·Transform 관측)입니다. 보정값은 tile_x100 정수로 공급하며, 축 맞춤은 플레이어 float 이동 계약(비탈 보류)에서 씁니다.
        public float AxisX => Area.center.x + transform.TransformVector(new Vector3(_axisOffsetUnits / 100f, 0f, 0f)).x;
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
