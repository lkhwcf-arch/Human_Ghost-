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
        }

        [Serializable]
        public sealed class MonsterEntry
        {
            public int Code = 0;
            public MonsterRuntime Prefab = null;
        }

        [SerializeField] private StageEntry[] _stages = Array.Empty<StageEntry>();
        [SerializeField] private GameObject[] _platforms = Array.Empty<GameObject>();
        [SerializeField] private MonsterEntry[] _monsters = Array.Empty<MonsterEntry>();

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
