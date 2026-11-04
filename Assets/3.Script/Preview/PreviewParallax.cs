using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 카메라 이동에 따라 배경 레이어를 움직입니다(K08). 비율은 StageLayer의 Parallax 만분율이며 레이어 이름으로 연결합니다.
    [DefaultExecutionOrder(1001)]
    public sealed class PreviewParallax : MonoBehaviour
    {
        [System.Serializable]
        private sealed class Layer
        {
            [SerializeField] private string _layerName = "";
            [SerializeField] private Transform _target = null;
            public string LayerName => _layerName;
            public Transform Target => _target;
        }

        [SerializeField, InspectorName("카메라")] private Transform _camera = null;
        [SerializeField, InspectorName("Stage ID")] private int _stageId = 8001;
        [SerializeField, InspectorName("레이어")] private Layer[] _layers = new Layer[0];
        private readonly List<float> _rates = new List<float>();
        private readonly List<Vector3> _bases = new List<Vector3>();
        private Vector3 _cameraBase;
        private bool _ready;

        public void Connect(IReadOnlyDataCatalog data)
        {
            _rates.Clear(); _bases.Clear();
            foreach (Layer layer in _layers)
            {
                float rate = 1f;
                foreach (StageLayer row in data.GetTable<StageLayer>().All)
                    if (row.StageID == _stageId && row.LayerName == layer.LayerName) rate = row.Parallax / 10000f;
                _rates.Add(rate);
                _bases.Add(layer.Target.position);
            }
            _cameraBase = _camera.position;
            _ready = true;
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            Vector3 moved = _camera.position - _cameraBase;
            for (int i = 0; i < _layers.Length; i++)
            {
                Vector3 position = _bases[i];
                position.x += moved.x * (1f - _rates[i]);
                position.y += moved.y * (1f - _rates[i]);
                _layers[i].Target.position = position;
            }
        }
    }
}
