using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace HumanGhost.Gameplay.Adapters
{
    // 스테이지 조립에 쓰는 리소스 목록입니다. 데이터의 이름·코드를 실제 프리팹으로 바꿉니다.
    // 원본 행은 Inspector로만 바뀌며, 실행 코드는 읽기 전용 속성·목록만 받습니다(S04). 옛 직렬화 이름은 FormerlySerializedAs로 읽습니다.
    [CreateAssetMenu(menuName = "인귀/스테이지 리소스 목록")]
    public sealed class StageContentSet : ScriptableObject
    {
        [Serializable]
        public sealed class StageEntry
        {
            [SerializeField, FormerlySerializedAs("StageId")] private int _stageId = 0;
            [SerializeField, FormerlySerializedAs("Map")] private GameObject _map = null;
            [SerializeField, FormerlySerializedAs("Spawn")] private TextAsset _spawn = null;
            [SerializeField, FormerlySerializedAs("CameraSize"), Min(0.1f)] private float _cameraSize = 5.625f;
            // 방 환경음(GameAudio 키, 루프). 비우면 무음입니다. 코드의 스테이지 번호 분기 대신 여기서 연결합니다(S02).
            [SerializeField, InspectorName("환경음 키 (루프)")] private string _ambienceKey = "";
            [SerializeField, FormerlySerializedAs("BackgroundPositions")] private BackgroundPosition[] _backgroundPositions = Array.Empty<BackgroundPosition>();
            [SerializeField, FormerlySerializedAs("WorldTexts")] private WorldTextBinding[] _worldTexts = Array.Empty<WorldTextBinding>();
            public int StageId => _stageId;
            public GameObject Map => _map;
            public TextAsset Spawn => _spawn;
            public float CameraSize => _cameraSize;
            public string AmbienceKey => _ambienceKey;
            public IReadOnlyList<BackgroundPosition> BackgroundPositions => Array.AsReadOnly(_backgroundPositions);
            public IReadOnlyList<WorldTextBinding> WorldTexts => Array.AsReadOnly(_worldTexts);
        }

        [Serializable] public sealed class WorldTextBinding
        {
            [SerializeField, FormerlySerializedAs("Anchor")] private string _anchor = "";
            [SerializeField, FormerlySerializedAs("TextIds")] private int[] _textIds = Array.Empty<int>();
            [SerializeField, FormerlySerializedAs("OffsetUnits")] private Vector2Int _offsetUnits = new Vector2Int(0, 150);
            public string Anchor => _anchor;
            public IReadOnlyList<int> TextIds => Array.AsReadOnly(_textIds);
            public Vector2Int OffsetUnits => _offsetUnits;
        }

        // [정수화 미적용 이유] 배경 그림 레이어(하늘·먼산·안개)의 Transform localPosition 표시값입니다.
        // 단위는 타일이 아니라 부모 기준 로컬 단위이고 게임 규칙·판정·상태에 쓰지 않으므로 표현 경계의 float로 유지합니다(2026-11-18 사용자 결정).
        [Serializable]
        public sealed class BackgroundPosition
        {
            [SerializeField, FormerlySerializedAs("Path")] private string _path;
            [SerializeField, FormerlySerializedAs("UseLocalX")] private bool _useLocalX;
            [SerializeField, FormerlySerializedAs("LocalX")] private float _localX;
            [SerializeField, FormerlySerializedAs("LocalY")] private float _localY;
            public string Path => _path;
            public bool UseLocalX => _useLocalX;
            public float LocalX => _localX;
            public float LocalY => _localY;
        }

        // 몬스터·NPC 행은 GetMonster·GetNpc로만 조회합니다. 목록 자체는 밖에 내보내지 않습니다.
        [Serializable]
        public sealed class MonsterEntry
        {
            [SerializeField, FormerlySerializedAs("Code")] private int _code = 0;
            [SerializeField, FormerlySerializedAs("Prefab")] private MonsterRuntime _prefab = null;
            public int Code => _code;
            public MonsterRuntime Prefab => _prefab;
        }
        [Serializable]
        public sealed class NpcEntry
        {
            [SerializeField, FormerlySerializedAs("Key")] private string _key = "";
            [SerializeField, FormerlySerializedAs("Prefab")] private GameObject _prefab = null;
            public string Key => _key;
            public GameObject Prefab => _prefab;
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
