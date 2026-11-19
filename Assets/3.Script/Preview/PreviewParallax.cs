using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 카메라 이동에 따라 배경 레이어를 움직입니다(K08). 비율은 StageLayer의 Parallax(가로)·ParallaxY(세로, 비면 Parallax) 만분율이며 레이어 이름으로 연결합니다.
    // 이동량은 정수 좌표(타일당 IntegerPosition.UnitsPerTile)와 분모 10000 비율로 계산하고, Transform에 넣는 순간만 월드 좌표로 바꿉니다.
    [DefaultExecutionOrder(1001)]
    public sealed class PreviewParallax : MonoBehaviour
    {
        private const int _rateDenominator = 10000;

        // 맵에 MapParallaxLayers가 없을 때 쓰는 기본 연결(맵 안 경로 → StageLayer 이름)입니다. 코드 대신 Inspector에서 바꿉니다.
        [Serializable]
        private sealed class DefaultLayer
        {
            [SerializeField, InspectorName("맵 안 경로")] private string _path = "";
            [SerializeField, InspectorName("StageLayer 이름")] private string _layerName = "";
            public DefaultLayer() { }
            public DefaultLayer(string path, string layerName) { _path = path; _layerName = layerName; }
            public string Path => _path;
            public string LayerName => _layerName;
        }

        private struct Target
        {
            public Transform Transform;
            public int RateX, RateY;
            public long BaseX, BaseY;
            // [정수화 미적용 이유] Transform z(그리기 깊이)입니다. 시차 계산에 쓰지 않고 그대로 되돌려 넣는 표시값입니다.
            public float BaseZ;
        }

        [SerializeField, InspectorName("카메라")] private Transform _camera = null;
        [SerializeField, InspectorName("기본 레이어 연결")] private DefaultLayer[] _defaultLayers =
        {
            new DefaultLayer("Background/Sky", "SKY"),
            new DefaultLayer("Background/Mountains", "FAR"),
            // 중경: K9 맵은 Midground…, 가마터·갈림길은 03_… 판입니다(StageLayer Note와 같음). Backfill·PitDepthFade는 땅과 같이 둡니다.
            new DefaultLayer("Background/Midground*", "MID"),
            new DefaultLayer("Background/03_*", "MID"),
            // 화면 맨 앞 전경 오브젝트(Object 아래 Foreground로 시작하는 것 전부)입니다. 경로 끝 *는 이름 앞부분 일치입니다.
            // StageLayer에 FRONT 행이 없는 맵은 비율 10000이라 지금처럼 월드와 같이 움직입니다.
            new DefaultLayer("Object/Foreground*", "FRONT"),
        };
        private readonly List<Target> _targets = new List<Target>();
        private long _cameraBaseX, _cameraBaseY;
        // [정수화 미적용 이유] 정수 좌표와 Unity 월드 좌표를 오가는 변환 계수(GameplayPreview의 타일당 화면 거리)입니다. 좌표 상태는 정수입니다.
        private float _worldUnitsPerTile = 1f;
        private bool _ready;

        // 맵별 표시 연결(MapParallaxLayers)이 있으면 사용하고, 없으면 기본 레이어 연결을 사용합니다.
        // cameraOrigin: 시차 기준 카메라 위치(도착 방향과 무관한 기준). 맵 고정 기준점이 우선이고, 둘 다 없으면 연결 순간 카메라입니다.
        public void Connect(IReadOnlyDataCatalog data, int stageId, Transform map, float worldUnitsPerTile, Vector3? cameraOrigin = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!(worldUnitsPerTile > 0f) || float.IsInfinity(worldUnitsPerTile)) throw new ArgumentOutOfRangeException(nameof(worldUnitsPerTile));
            Disconnect();
            _worldUnitsPerTile = worldUnitsPerTile;
            var layers = map.GetComponent<MapParallaxLayers>();
            if (layers != null)
            {
                foreach (MapParallaxLayers.Layer layer in layers.Layers)
                    Add(data, stageId, layer.Target, layer.LayerName, layer.RateMultiplier);
            }
            else
            {
                foreach (DefaultLayer layer in _defaultLayers)
                    foreach (Transform target in FindTargets(map, layer.Path))
                        Add(data, stageId, target, layer.LayerName, _rateDenominator);
            }
            // 기준 카메라 위치: 맵이 고정 기준점(맵 기준 tile_x100)을 정했으면 그 점, 아니면 연결 순간의 카메라입니다.
            Vector3 camera = layers != null && layers.UseFixedCameraOrigin
                ? map.TransformPoint(new Vector3(layers.CameraOriginUnits.x / 100f * worldUnitsPerTile, layers.CameraOriginUnits.y / 100f * worldUnitsPerTile, 0f))
                : cameraOrigin ?? _camera.position;
            _cameraBaseX = ToInteger(camera.x);
            _cameraBaseY = ToInteger(camera.y);
            _ready = true;
        }

        // 연결을 끊으면 레이어를 원래 위치로 되돌립니다.
        public void Disconnect()
        {
            _ready = false;
            foreach (Target target in _targets)
                if (target.Transform != null) target.Transform.position = new Vector3(ToWorld(target.BaseX), ToWorld(target.BaseY), target.BaseZ);
            _targets.Clear();
        }

        private void OnDisable() => Disconnect();

        // 비율 = StageLayer 비율 × 맵 배율 / 10000(둘 다 만분율). 행이 없으면 10000(월드와 같이 움직임)입니다.
        private void Add(IReadOnlyDataCatalog data, int stageId, Transform target, string layerName, int rateMultiplier)
        {
            if (target == null) return;
            int parallaxX = _rateDenominator, parallaxY = _rateDenominator;
            foreach (StageLayer row in data.GetTable<StageLayer>().All)
                if (row.StageID == stageId && row.LayerName == layerName) { parallaxX = row.Parallax; parallaxY = row.ParallaxY ?? row.Parallax; }
            Vector3 position = target.position;
            _targets.Add(new Target
            {
                Transform = target,
                RateX = Scale(parallaxX, rateMultiplier),
                RateY = Scale(parallaxY, rateMultiplier),
                BaseX = ToInteger(position.x),
                BaseY = ToInteger(position.y),
                BaseZ = position.z,
            });
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            Vector3 camera = _camera.position;
            long movedX = checked(ToInteger(camera.x) - _cameraBaseX);
            long movedY = checked(ToInteger(camera.y) - _cameraBaseY);
            foreach (Target target in _targets)
            {
                if (target.Transform == null) continue;
                // 카메라 이동 중 (10000 - 비율)/10000만큼 따라가 화면에서는 비율만큼 움직여 보입니다.
                long x = checked(target.BaseX + movedX * (_rateDenominator - target.RateX) / _rateDenominator);
                long y = checked(target.BaseY + movedY * (_rateDenominator - target.RateY) / _rateDenominator);
                target.Transform.position = new Vector3(ToWorld(x), ToWorld(y), target.BaseZ);
            }
        }

        // 경로 끝이 *이면 부모 경로 아래에서 이름이 앞부분과 같은 자식 전부, 아니면 그 경로 하나입니다.
        private static IEnumerable<Transform> FindTargets(Transform map, string path)
        {
            if (string.IsNullOrEmpty(path)) yield break;
            if (!path.EndsWith("*", StringComparison.Ordinal))
            {
                Transform single = map.Find(path);
                if (single != null) yield return single;
                yield break;
            }
            int slash = path.LastIndexOf('/');
            Transform parent = slash < 0 ? map : map.Find(path.Substring(0, slash));
            if (parent == null) yield break;
            string prefix = path.Substring(slash + 1, path.Length - slash - 2);
            foreach (Transform child in parent)
                if (child.name.StartsWith(prefix, StringComparison.Ordinal)) yield return child;
        }

        private static int Scale(int rate, int multiplier) => checked((int)((long)rate * multiplier / _rateDenominator));
        private long ToInteger(float world) => SpatialRuntime.ToInteger(world, _worldUnitsPerTile);
        private float ToWorld(long units) => SpatialRuntime.ToWorld(units, _worldUnitsPerTile);
    }
}
