using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 부모 인스턴스가 준비된 뒤 한 번 수집합니다. 다른 인스턴스의 앵커는 검색하지 않습니다.
    public sealed class SpawnAnchors
    {
        private readonly Dictionary<string, Transform> _anchors = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly HashSet<string> _duplicates = new HashSet<string>(StringComparer.Ordinal);

        public SpawnAnchors(Transform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root), "준비된 앵커 묶음 인스턴스가 필요합니다.");
            foreach (Transform anchor in root.GetComponentsInChildren<Transform>(true))
            {
                if (anchor == root) continue;
                if (_anchors.ContainsKey(anchor.name))
                    _duplicates.Add(anchor.name);
                else _anchors.Add(anchor.name, anchor);
            }
        }

        public Transform Get(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name), "선택할 앵커 이름이 필요합니다.");
            if (_duplicates.Contains(name))
                throw new InvalidOperationException($"같은 범위에 사용할 앵커 이름이 중복됩니다: {name}");
            if (!_anchors.TryGetValue(name, out Transform anchor) || anchor == null)
                throw new InvalidOperationException($"사용할 수 있는 스폰 앵커가 없습니다: {name}");
            return anchor;
        }
    }
}
