using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 결박 관문(L08·C07): 표식 BindGate_*에 막는 충돌을 만들고, 결박 가르기 판정이 닿으면 열립니다. 다른 공격으로는 열리지 않습니다.
    internal sealed class PreviewBindGates : System.IDisposable
    {
        private sealed class Gate { public int Id; public GameObject Blocker; public SpriteRenderer View; public Bounds Bounds; }
        // 표시는 3타일이지만 점프(3타일)로 넘지 못하게 충돌은 위로 길게 둡니다.
        private const float _blockerHeightScale = 10f;
        private readonly List<Gate> _gates = new List<Gate>();
        private bool _touching;
        private Sprite _sprite;
        private readonly GameProgressState _progress;

        public PreviewBindGates(Transform markers, int stageId, IReadOnlyDataCatalog data, GameProgressState progress)
        {
            _progress = progress ?? throw new System.ArgumentNullException(nameof(progress));
            // 임시 표시용 1×1 사각형입니다. 그림 공급 뒤 교체합니다.
            Texture2D texture = Texture2D.whiteTexture;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            _sprite = sprite;
            try
            {
                var ids = data.GetTable<GateConfig>().All.Where(row => row.StageID == stageId)
                    .ToDictionary(row => row.AnchorName, row => row.ID, System.StringComparer.Ordinal);
                foreach (Transform marker in markers)
                {
                    if (!marker.name.StartsWith("BindGate_", System.StringComparison.Ordinal)) continue;
                    if (!ids.TryGetValue(marker.name, out int id))
                        throw new System.InvalidOperationException($"관문 ID 연결이 없습니다. GateConfig에서 스테이지 {stageId}, 앵커 {marker.name}을 연결해 주세요.");
                    if (_progress.HasOpenedGate(id)) continue;
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
                    _gates.Add(new Gate { Id = id, Blocker = blocker, View = view, Bounds = box.bounds });
                }
                Physics2D.SyncTransforms();
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            foreach (Gate gate in _gates)
                if (gate.Blocker != null) { gate.Blocker.SetActive(false); Object.Destroy(gate.Blocker); }
            _gates.Clear();
            if (_sprite != null) Object.Destroy(_sprite);
            _sprite = null;
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
                _progress.OpenGate(gate.Id);
                gate.Blocker.SetActive(false);
                Object.Destroy(gate.Blocker);
                _gates.RemoveAt(i);
            }
        }
    }
}
