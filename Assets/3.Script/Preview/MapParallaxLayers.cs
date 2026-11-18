using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 맵이 소유하는 표시 레이어를 StageLayer의 이동 비율에 연결합니다. 이동 비율(가로 Parallax·세로 ParallaxY)은 StageLayer 표가 정합니다.
    // 원본 배열은 Inspector로만 바뀌며 실행 코드는 읽기 전용 목록을 받습니다(S04). 비율은 분모 10000, 위치는 tile_x100 정수입니다.
    public sealed class MapParallaxLayers : MonoBehaviour
    {
        [Serializable]
        public sealed class Layer
        {
            [SerializeField, InspectorName("대상")] private Transform _target = null;
            [SerializeField, InspectorName("StageLayer 이름")] private string _layerName = "";
            // 같은 StageLayer 이름에 묶인 대상끼리 차이를 둘 때 쓰는 배율입니다(만분율, 10000 = 표 값 그대로).
            [SerializeField, InspectorName("이동 비율 배율 (만분율)"), Min(0)] private int _rateMultiplier = 10000;

            public Transform Target => _target;
            public string LayerName => _layerName;
            public int RateMultiplier => _rateMultiplier;
        }

        [SerializeField, InspectorName("레이어")] private Layer[] _layers = Array.Empty<Layer>();
        // 켜면 연결 순간 카메라 대신 이 맵 기준점(tile_x100)을 시차 기준 카메라 위치로 씁니다. 도착 위치와 무관하게 배경 배치를 고정할 때 씁니다.
        [SerializeField, InspectorName("고정 카메라 기준점 사용")] private bool _useFixedCameraOrigin = false;
        [SerializeField, InspectorName("고정 카메라 기준점 (맵 기준 tile_x100)")] private Vector2Int _cameraOriginUnits = Vector2Int.zero;
        public IReadOnlyList<Layer> Layers => Array.AsReadOnly(_layers);
        public bool UseFixedCameraOrigin => _useFixedCameraOrigin;
        public Vector2Int CameraOriginUnits => _cameraOriginUnits;
    }
}
