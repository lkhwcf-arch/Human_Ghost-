using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Presentation
{
    // 에디터가 프리팹 참조를 채웁니다. 직접 참조하므로 빌드에도 해당 외형이 포함됩니다.
    public sealed class HealthDisplayPrefabs : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string _key = null;
            [SerializeField] private HealthDisplay _prefab = null;
            public string Key => _key;
            public HealthDisplay Prefab => _prefab;
        }

        [SerializeField, InspectorName("체력 표시 프리팹")] private List<Entry> _entries = new List<Entry>();

        public HealthDisplay Get(string key)
        {
            foreach (Entry entry in _entries)
                if (entry.Prefab != null && string.Equals(entry.Key, key, StringComparison.Ordinal)) return entry.Prefab;
            throw new InvalidOperationException($"체력 표시 프리팹 목록에 '{key}'가 없습니다. 에디터에서 표현 목록을 갱신해주세요.");
        }
    }
}
