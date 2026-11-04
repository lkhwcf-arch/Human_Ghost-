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
        [SerializeField, InspectorName("카메라")] private Transform _camera = null;
        private readonly List<Transform> _targets = new List<Transform>();
        private readonly List<float> _rates = new List<float>();
        private readonly List<Vector3> _bases = new List<Vector3>();
        private Vector3 _cameraBase;
        private bool _ready;

        // 맵 프리팹의 Background 아래 Sky=SKY, Mountains=FAR로 연결합니다.
        public void Connect(IReadOnlyDataCatalog data, int stageId, Transform map)
        {
            _targets.Clear(); _rates.Clear(); _bases.Clear();
            Add(data, stageId, map.Find("Background/Sky"), "SKY");
            Add(data, stageId, map.Find("Background/Mountains"), "FAR");
            _cameraBase = _camera.position;
            _ready = true;
        }

        private void Add(IReadOnlyDataCatalog data, int stageId, Transform target, string layerName)
        {
            if (target == null) return;
            float rate = 1f;
            foreach (StageLayer row in data.GetTable<StageLayer>().All)
                if (row.StageID == stageId && row.LayerName == layerName) rate = row.Parallax / 10000f;
            _targets.Add(target); _rates.Add(rate); _bases.Add(target.position);
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            Vector3 moved = _camera.position - _cameraBase;
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i] == null) continue;
                Vector3 position = _bases[i];
                position.x += moved.x * (1f - _rates[i]);
                position.y += moved.y * (1f - _rates[i]);
                _targets[i].position = position;
            }
        }
    }
}
