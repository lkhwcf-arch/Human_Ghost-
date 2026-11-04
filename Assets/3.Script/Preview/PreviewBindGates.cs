using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 결박 관문(L08·C07): 표식 BindGate_*에 막는 충돌을 만들고, 결박 가르기 판정이 닿으면 열립니다. 다른 공격으로는 열리지 않습니다.
    internal sealed class PreviewBindGates
    {
        private sealed class Gate { public string Key; public GameObject Blocker; public SpriteRenderer View; public Bounds Bounds; }
        // 표시는 3타일이지만 점프(3타일)로 넘지 못하게 충돌은 위로 길게 둡니다.
        private const float _blockerHeightScale = 10f;
        private readonly List<Gate> _gates = new List<Gate>();
        private bool _touching;

        public PreviewBindGates(Transform markers, int stageId)
        {
            // 임시 표시용 1×1 사각형입니다. 그림 공급 뒤 교체합니다.
            Texture2D texture = Texture2D.whiteTexture;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            foreach (Transform marker in markers)
            {
                if (!marker.name.StartsWith("BindGate_", System.StringComparison.Ordinal)) continue;
                string key = stageId + "_" + marker.name;
                if (PreviewSession.OpenedGates.Contains(key)) continue;
                // 폭 1·높이 3타일(L08).
                var blocker = new GameObject("Blocker");
                blocker.transform.SetParent(marker, false);
                blocker.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                blocker.transform.localScale = new Vector3(1f, 3f, 1f);
                var box = blocker.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1f, _blockerHeightScale);
                box.offset = new Vector2(0f, (_blockerHeightScale - 1f) * 0.5f);
                var view = blocker.AddComponent<SpriteRenderer>();
                view.sprite = sprite;
                view.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
                view.sortingOrder = 50;
                _gates.Add(new Gate { Key = key, Blocker = blocker, View = view, Bounds = box.bounds });
            }
            Physics2D.SyncTransforms();
        }

        // 관문에 처음 닿을 때 한 번 안내하고, 결박 가르기 판정이 닿은 관문을 엽니다.
        public void Step(PlayerActor actor, Bounds attack, Bounds player, System.Action onTouch)
        {
            Bounds near = new Bounds(player.center, player.size + new Vector3(1f, 0f, 10f));
            bool touching = false;
            foreach (Gate gate in _gates)
                touching |= near.Intersects(new Bounds(gate.View.bounds.center, gate.View.bounds.size + Vector3.forward * 10f));
            if (touching && !_touching) onTouch();
            _touching = touching;
            if (actor.CurrentState != HumanGhost.Data.Definitions.StateKey.Skill1 || !actor.IsAttackActive) return;
            for (int i = _gates.Count - 1; i >= 0; i--)
            {
                Gate gate = _gates[i];
                Bounds area = new Bounds(gate.View.bounds.center, gate.View.bounds.size + Vector3.forward * 10f);
                if (!area.Intersects(new Bounds(attack.center, attack.size + Vector3.forward * 10f))) continue;
                PreviewSession.OpenedGates.Add(gate.Key);
                Object.Destroy(gate.Blocker);
                _gates.RemoveAt(i);
            }
        }
    }
}
