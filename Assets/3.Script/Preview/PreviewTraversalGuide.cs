using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 실제 통과 발판과 사다리만 표시합니다. 충돌·상호작용·배치 데이터는 바꾸지 않습니다.
    internal static class PreviewTraversalGuide
    {
        public static void AddTo(Transform map, Transform markers, Sprite square, int layer, int order)
        {
            if (map == null || square == null) return;
            var root = new GameObject("TraversalGuide"); root.SetActive(false); root.transform.SetParent(map, false);
            foreach (BoxCollider2D box in map.GetComponentsInChildren<BoxCollider2D>())
            {
                if (!box.enabled || box.isTrigger || !box.usedByEffector) continue;
                Bounds bounds = box.bounds;
                Mark(root.transform, "PlatformEdge", square, layer, order,
                    new Vector2(bounds.center.x, bounds.max.y + .012f), new Vector2(bounds.size.x, .024f), new Color(.95f,.85f,.62f,.6f));
            }
            if (markers != null) foreach (LadderClimbArea ladder in markers.GetComponentsInChildren<LadderClimbArea>())
            {
                if (!ladder.isActiveAndEnabled) continue;
                Bounds bounds = ladder.Area;
                for (int end = 0; end < 2; end++)
                    for (int rung = 0; rung < 2; rung++)
                    {
                        float y = end == 0 ? bounds.min.y + .18f + rung * .22f : bounds.max.y - .18f - rung * .22f;
                        Mark(root.transform, "LadderRung", square, layer, order,
                            new Vector2(ladder.AxisX, y), new Vector2(Mathf.Min(.6f, bounds.size.x), .035f), new Color(.95f,.78f,.4f,.7f));
                    }
            }
            root.SetActive(true);
        }
        private static void Mark(Transform parent, string name, Sprite square, int layer, int order, Vector2 position, Vector2 size, Color color)
        {
            var view = new GameObject(name); view.SetActive(false); view.transform.SetParent(parent, false);
            var renderer = view.AddComponent<SpriteRenderer>(); renderer.sprite = square; renderer.sortingLayerID = layer;
            renderer.sortingOrder = order; renderer.color = color;
            view.transform.localScale = new Vector3(size.x / square.bounds.size.x, size.y / square.bounds.size.y, 1f);
            view.transform.position = new Vector3(position.x, position.y, parent.position.z);
            view.SetActive(true);
        }
    }
}
