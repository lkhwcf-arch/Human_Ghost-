using System;
using HumanGhost.Data.Definitions;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 스테이지 조립에 쓰는 리소스 목록입니다. 데이터의 이름·코드를 실제 프리팹으로 바꿉니다.
    [CreateAssetMenu(menuName = "인귀/스테이지 리소스 목록")]
    public sealed class StageContentSet : ScriptableObject
    {
        [Serializable]
        public sealed class StageEntry
        {
            public int StageId = 0;
            public GameObject Map = null;
            public TextAsset Spawn = null;
            [Min(0.1f)] public float CameraSize = 5.625f;
            public BackgroundPosition[] BackgroundPositions = Array.Empty<BackgroundPosition>();
            public WorldTextBinding[] WorldTexts = Array.Empty<WorldTextBinding>();
        }

        [Serializable] public sealed class WorldTextBinding
        {
            public string Anchor = "";
            public int[] TextIds = Array.Empty<int>();
            public Vector2Int OffsetUnits = new Vector2Int(0, 150);
        }

        // [정수화 미적용 이유] 배경 그림 레이어(하늘·먼산·안개)의 Transform localPosition 표시값입니다.
        // 단위는 타일이 아니라 부모 기준 로컬 단위이고 게임 규칙·판정·상태에 쓰지 않으므로 표현 경계의 float로 유지합니다(2026-11-18 사용자 결정).
        [Serializable]
        public sealed class BackgroundPosition
        {
            public string Path;
            public bool UseLocalX;
            public float LocalX;
            public float LocalY;
        }

        [Serializable]
        public sealed class MonsterEntry
        {
            public int Code = 0;
            public MonsterRuntime Prefab = null;
        }
        [Serializable]
        public sealed class NpcEntry
        {
            public string Key = "";
            public GameObject Prefab = null;
        }

        [SerializeField] private StageEntry[] _stages = Array.Empty<StageEntry>();
        [SerializeField] private GameObject[] _platforms = Array.Empty<GameObject>();
        [SerializeField] private MonsterEntry[] _monsters = Array.Empty<MonsterEntry>();
        [SerializeField] private NpcEntry[] _npcs = Array.Empty<NpcEntry>();
        public GameObject GetNpc(string key)
        {
            foreach (NpcEntry entry in _npcs) if (entry.Key == key) return entry.Prefab;
            return null;
        }

        [Header("필드 그림 리소스")]
        [SerializeField] private Sprite _fieldFire = null;
        [SerializeField] private Sprite _fieldIceLeft = null;
        [SerializeField] private Sprite _fieldIceMiddle = null;
        [SerializeField] private Sprite _fieldIceRight = null;
        public Sprite FieldFire => _fieldFire;
        public Sprite FieldIceLeft => _fieldIceLeft;
        public Sprite FieldIceMiddle => _fieldIceMiddle;
        public Sprite FieldIceRight => _fieldIceRight;
        public StageEntry[] Stages => _stages;

        public StageEntry GetStage(int stageId)
        {
            foreach (StageEntry entry in _stages) if (entry.StageId == stageId) return entry;
            return null;
        }

        public GameObject GetPlatform(string name)
        {
            foreach (GameObject platform in _platforms) if (platform != null && platform.name == name) return platform;
            return null;
        }

        public MonsterRuntime GetMonster(int code)
        {
            foreach (MonsterEntry entry in _monsters) if (entry.Code == code) return entry.Prefab;
            return null;
        }
    }
}
