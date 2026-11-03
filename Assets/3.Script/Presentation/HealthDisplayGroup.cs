using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Presentation
{
    // 호출자가 수명을 소유합니다. 데이터 로딩과 플레이어 생성은 이 모듈의 책임이 아닙니다.
    public sealed class HealthDisplayGroup : IDisposable
    {
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<HealthDisplay> _views = new List<HealthDisplay>();

        private HealthDisplayGroup() { }

        public static HealthDisplayGroup Create(IReadOnlyDataCatalog data, HealthDisplayPrefabs prefabs,
            PlayerActor player, SpawnAnchors anchors)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (prefabs == null) throw new ArgumentNullException(nameof(prefabs));
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (anchors == null) throw new ArgumentNullException(nameof(anchors));
            var group = new HealthDisplayGroup();
            try
            {
                foreach (HealthDisplayConfig config in data.GetTable<HealthDisplayConfig>().All)
                {
                    if (!config.Visible) continue;
                    var anchor = anchors.Get(config.AnchorName) as RectTransform;
                    if (anchor == null)
                        throw new InvalidOperationException($"체력 표시 앵커는 RectTransform이어야 합니다: {config.AnchorName}");
                    var ownedRoot = new GameObject($"HealthDisplay_{config.ID}", typeof(RectTransform));
                    group._roots.Add(ownedRoot);
                    ownedRoot.SetActive(false);
                    var root = (RectTransform)ownedRoot.transform;
                    root.SetParent(anchor, false);
                    root.anchorMin = root.anchorMax = anchor.pivot;
                    root.sizeDelta = Vector2.zero;
                    root.localPosition = Vector3.zero;
                    HealthDisplay view = UnityEngine.Object.Instantiate(prefabs.Get(config.PrefabKey), root, false);
                    group._views.Add(view);
                    var rect = (RectTransform)view.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(config.Width, config.Height);
                    rect.localPosition = Vector3.zero;
                    rect.localRotation = Quaternion.identity;
                    rect.localScale = Vector3.one;
                    view.Bind(player);
                }
                // 모든 표시의 대상과 위치를 연결한 후에만 구독을 시작합니다.
                foreach (GameObject root in group._roots) root.SetActive(true);
                return group;
            }
            catch
            {
                group.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            foreach (GameObject root in _roots)
                if (root != null) root.SetActive(false);
            foreach (HealthDisplay view in _views)
                if (view != null) view.Unbind();
            _views.Clear();
            foreach (GameObject root in _roots)
                if (root != null) UnityEngine.Object.Destroy(root);
            _roots.Clear();
        }
    }
}
