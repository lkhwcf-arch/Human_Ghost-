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
        // [정수화 미적용 이유] Bounds는 관문 Collider2D.bounds 관측값(Unity 물리 경계)입니다.
        private sealed class Gate { public int Id; public GameObject Blocker; public SpriteRenderer View; public Bounds Bounds; }
        // 표시는 3타일이지만 점프(3타일)로 넘지 못하게 충돌은 위로 길게 둡니다.
        // [정수화 미적용 이유] 결박 관문 차단 Collider의 Transform 높이 배율(물리 경계)입니다.
        private const float _blockerHeightScale = 10f;
        // 관문 안내를 띄우는 접근 판정의 좌우 합계 여유(tile_x100)입니다.
        private const int _touchMarginUnits = 100;
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
                    {
                        // 삭제 확정된 이전 맵 표식은 경고 없이 생략합니다. 새 설정은 위 조회에서 우선합니다.
                        if (IsRemovedMarker(stageId, marker.name)) continue;
                        // 임시 연결 정책: 표에 없는 표식은 통행을 막거나 저장 ID를 임의로 만들지 않습니다.
                        Debug.LogWarning($"관문을 임시로 생성하지 않습니다. GateConfig에서 스테이지 {stageId}, 앵커 {marker.name}의 ID를 연결한 뒤 다시 배포해 주세요. 이 위치는 통행 가능하며 개방 기록은 저장하지 않습니다.", marker);
                        continue;
                    }
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

        private static bool IsRemovedMarker(int stageId, string name)
            => (stageId == 8002 && (name == "BindGate_1" || name == "BindGate_2")) ||
                (stageId == 8003 && name == "BindGate_3");

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
            // 좌우 여유(tile_x100)만큼 넓혀 안내 접근을 봅니다. Bounds(Unity)에 넣는 순간에만 월드로 바꿉니다. 이 파일의 관문 크기와 같이 1타일 = 월드 1입니다.
            Bounds near = new Bounds(player.center, player.size + new Vector3(_touchMarginUnits / 100f, 0f, 10f));
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
