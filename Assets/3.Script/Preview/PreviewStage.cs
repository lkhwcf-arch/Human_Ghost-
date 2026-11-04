using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 스테이지 하나를 조립합니다: 맵 → JSON 플랫폼 → JSON 몬스터. 생성은 비활성 상태에서 마친 뒤 활성화합니다.
    // 재시작·스테이지 이동은 씬을 다시 불러오므로 몬스터를 재사용하는 풀은 아직 두지 않습니다.
    internal sealed class PreviewStage
    {
        private const string _markersName = "Markers";
        private const float _groundLine = 2f;
        public Transform MapRoot { get; }
        public Transform Markers { get; }
        public Transform Interactables { get; }
        public List<MonsterRuntime> Monsters { get; } = new List<MonsterRuntime>();
        public Bounds Area { get; private set; }
        public float CameraSize { get; }

        public PreviewStage(StageContentSet content, int stageId, IReadOnlyDataCatalog data, Vector3 origin)
        {
            StageContentSet.StageEntry entry = content.GetStage(stageId) ?? throw new InvalidOperationException("스테이지 리소스가 없습니다: " + stageId);
            if (entry.Map == null) throw new InvalidOperationException("스테이지 맵 프리팹이 없습니다: " + stageId);
            CameraSize = entry.CameraSize;
            GameObject map = UnityEngine.Object.Instantiate(entry.Map, origin + entry.Map.transform.localPosition, Quaternion.identity);
            map.name = entry.Map.name;
            MapRoot = map.transform;
            Markers = MapRoot.Find(_markersName) ?? throw new InvalidOperationException(map.name + "에 Markers가 없습니다.");
            Interactables = new GameObject("Interactables").transform;
            Interactables.SetParent(MapRoot, false);
            if (entry.Spawn != null) Build(content, StageLayoutReader.Read(entry.Spawn.text), data);
            Physics2D.SyncTransforms();
            Area = FindArea();
        }

        private void Build(StageContentSet content, StageLayout layout, IReadOnlyDataCatalog data)
        {
            var anchors = new SpawnAnchors(Markers);
            var placed = new Dictionary<int, StagePlacement>();
            foreach (StagePlacement placement in layout.Placements)
            {
                GameObject prefab = content.GetPlatform(placement.Prefab);
                if (prefab == null) { Debug.LogWarning($"배치 {placement.PlacementID}: 플랫폼 프리팹이 없습니다: {placement.Prefab}"); continue; }
                Transform anchor = anchors.Get(placement.Anchor);
                GameObject platform = UnityEngine.Object.Instantiate(prefab, anchor.position, Quaternion.identity, MapRoot);
                platform.name = prefab.name;
                placed[placement.PlacementID] = placement;
            }
            IReadOnlyDataTable<Monster> table = data.GetTable<Monster>();
            var known = new HashSet<int>();
            var enabled = new HashSet<int>();
            foreach (Monster monster in table.All) { known.Add(monster.ID); if (monster.ContentEnabled) enabled.Add(monster.ID); }
            var warnings = new List<string>();
            List<StageMonsterSpawn> spawns = StageLayoutRules.ResolveMonsters(layout, known.Contains, enabled.Contains, warnings);
            foreach (string warning in warnings) Debug.LogWarning(warning);
            foreach (StageMonsterSpawn spawn in spawns)
                if (placed.TryGetValue(spawn.PlacementId, out StagePlacement placement)) SpawnMonster(content, spawn, placement, anchors.Get(placement.Anchor));
        }

        private void SpawnMonster(StageContentSet content, StageMonsterSpawn spawn, StagePlacement placement, Transform anchor)
        {
            MonsterRuntime prefab = content.GetMonster(spawn.Code);
            if (prefab == null) { Debug.LogWarning("몬스터 프리팹이 없습니다: " + spawn.Code); return; }
            // 비활성 부모 아래 생성해 Awake 전에 위치를 정합니다. 프리팹 원본은 건드리지 않습니다.
            var holder = new GameObject("SpawnHolder");
            holder.SetActive(false);
            MonsterRuntime monster = UnityEngine.Object.Instantiate(prefab, holder.transform);
            monster.name = $"{prefab.name}_{spawn.PlacementId}_{spawn.Sequence}";
            // 발판을 옮기면 몬스터도 따라가도록 플랫폼 앵커에서 잽니다. 바닥선은 맵 y=2입니다.
            float baseY = StageLayoutRules.UsesGroundLine(placement.Prefab) ? MapRoot.position.y + _groundLine : anchor.position.y;
            float halfHeight = monster.GetComponentInChildren<SpriteRenderer>(true).transform.lossyScale.y * 0.5f;
            monster.transform.position = new Vector3(anchor.position.x + spawn.X / 100f, baseY + spawn.Height / 100f + halfHeight, MapRoot.position.z);
            monster.transform.SetParent(null, true);
            UnityEngine.Object.Destroy(holder);
            monster.gameObject.SetActive(true);
            Monsters.Add(monster);
        }

        private Bounds FindArea()
        {
            bool first = true;
            var area = new Bounds(MapRoot.position, Vector3.zero);
            foreach (BoxCollider2D box in MapRoot.GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (box.isTrigger) continue;
                if (first) { area = box.bounds; first = false; } else area.Encapsulate(box.bounds);
            }
            return area;
        }
    }
}
