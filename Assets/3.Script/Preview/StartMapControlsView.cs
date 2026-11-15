using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 시작 맵의 안내만 소유합니다. 이름은 공통 언어 데이터, 키는 현재 입력 설정에서 읽습니다.
    public sealed class StartMapControlsView : MonoBehaviour
    {
        private static readonly int[] _textIds = { 30058, 30059, 30244, 30060, 30061, 0, 30010, 30022, 30062 };
        private TextMesh[] _labels;
        private LanguageTextSource _text;
        private SubscriptionScope _scope;
        public static void AddTo(Transform map, Transform markers, TextMesh prototype, Sprite square, LanguageTextSource text)
        {
            if (map == null || markers == null || prototype == null || square == null) throw new System.ArgumentException("시작 안내의 맵·표식·글꼴·받침을 연결해 주세요.");
            Transform start = markers.Find("PlayerStart");
            if (start == null) throw new System.InvalidOperationException("시작 안내의 PlayerStart가 없습니다.");
            var root = new GameObject("StartControls"); root.SetActive(false); root.transform.SetParent(map, false);
            var view = root.AddComponent<StartMapControlsView>(); view._text = text; view._labels = new TextMesh[_textIds.Length];
            for (int i = 0; i < view._labels.Length; i++)
            {
                var label = new GameObject("Action_" + i, typeof(TextMesh)); label.transform.SetParent(root.transform, false);
                label.transform.position = start.position + new Vector3(3f + i * 6f, 2.2f, 0f);
                var mesh = label.GetComponent<TextMesh>(); mesh.font = prototype.font; mesh.fontSize = 96;
                mesh.characterSize = .031f; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
                mesh.fontStyle = FontStyle.Bold; mesh.color = new Color(1f,.96f,.86f,1f);
                var renderer = label.GetComponent<MeshRenderer>(); renderer.sharedMaterial = prototype.GetComponent<MeshRenderer>().sharedMaterial;
                renderer.sortingOrder = 62; view._labels[i] = mesh;
                var plate = new GameObject("Backplate", typeof(SpriteRenderer)); plate.transform.SetParent(label.transform, false);
                plate.transform.localPosition = new Vector3(0f,0f,.02f); var sr = plate.GetComponent<SpriteRenderer>();
                sr.sprite = square; sr.color = new Color(.025f,.02f,.015f,.85f); sr.sortingOrder = 61;
                plate.transform.localScale = new Vector3(4.5f / square.bounds.size.x, 1.45f / square.bounds.size.y, 1f);
            }
            root.SetActive(true);
        }
        private void OnEnable()
        {
            if (_labels == null) return;
            var scope = _scope = new SubscriptionScope();
            System.Action refresh = scope.Guard(Refresh);
            scope.Add(() => KeyboardControls.Changed -= refresh); KeyboardControls.Changed += refresh;
            scope.Add(() => LanguageSettings.Changed -= refresh); LanguageSettings.Changed += refresh; Refresh();
        }
        private void OnDisable() { _scope?.Dispose(); _scope = null; }
        private void Refresh()
        {
            string[] keys = KeyboardControls.ReadActionKeys();
            for (int i = 0; i < _labels.Length; i++)
            {
                // 기획 공급 30368+의 설명은 아직 없습니다. 패링 이름도 ID 공급 전의 명시적 표시 예외입니다.
                string name = _textIds[i] == 0 ? (LanguageSettings.Current == LanguageCode.Korean ? "패링" : "Parry") : _text.Read(_textIds[i], LanguageSettings.Current);
                _labels[i].text = name + "\n[ " + keys[i] + " ]";
            }
        }
    }
}
