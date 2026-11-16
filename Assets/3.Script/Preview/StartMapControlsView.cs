using HumanGhost.Gameplay.Adapters;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 시작 맵의 안내만 소유합니다. 이름은 공통 언어 데이터, 키는 현재 입력 설정에서 읽습니다.
    public class StartMapControlsView : MonoBehaviour
    {
        private UI_Lang[] _entries;
        private TextMesh[] _labels;
        private LanguageTextSource _text;
        private SubscriptionScope _scope;
        public static void AddTo(Transform map, Transform markers, TextMesh prototype, Sprite square, IReadOnlyDataCatalog data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            UI_Lang[] entries = data.GetTable<UI_Lang>().All.Where(row => row.Key.StartsWith("StartGuide.", StringComparison.Ordinal)).OrderBy(row => row.ID).ToArray();
            if (entries.Length == 0) throw new InvalidOperationException("UI_Lang에 StartGuide 시작 안내 연결이 없습니다.");
            var text = new LanguageTextSource(data);
            // 연결 검사는 생성 전에 끝내 중간 생성 객체가 남지 않게 합니다.
            foreach (UI_Lang entry in entries)
            {
                string key = KeyboardControls.ReadActionKey(entry.Key.Substring("StartGuide.".Length));
                string.Format(text.Read(entry.TextID, LanguageCode.Korean), key);
                string.Format(text.Read(entry.TextID, LanguageCode.English), key);
            }
            if (map == null || markers == null || prototype == null || square == null) throw new System.ArgumentException("시작 안내의 맵·표식·글꼴·받침을 연결해 주세요.");
            Transform start = markers.Find("PlayerStart");
            if (start == null) throw new System.InvalidOperationException("시작 안내의 PlayerStart가 없습니다.");
            var root = new GameObject("StartControls"); root.SetActive(false); root.transform.SetParent(map, false);
            try
            {
                var view = root.AddComponent<StartMapControlsView>(); view._text = text; view._entries = entries; view._labels = new TextMesh[entries.Length];
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
                    plate.transform.localScale = new Vector3(5.5f / square.bounds.size.x, 2.6f / square.bounds.size.y, 1f);
                }
                root.SetActive(true);
            }
            catch
            {
                Destroy(root);
                throw;
            }
        }
        private void OnEnable()
        {
            if (_labels == null) return;
            var scope = _scope = new SubscriptionScope();
            System.Action refresh = scope.Guard(RefreshSafely);
            scope.Add(() => KeyboardControls.Changed -= refresh); KeyboardControls.Changed += refresh;
            scope.Add(() => LanguageSettings.Changed -= refresh); LanguageSettings.Changed += refresh; RefreshSafely();
        }
        private void OnDisable() { _scope?.Dispose(); _scope = null; }
        private void RefreshSafely()
        {
            try { Refresh(); }
            catch (Exception error) { ErrorPopupHost.WriteError(error, this); }
        }
        private void Refresh()
        {
            for (int i = 0; i < _labels.Length; i++)
            {
                UI_Lang entry = _entries[i];
                string key = KeyboardControls.ReadActionKey(entry.Key.Substring("StartGuide.".Length));
                TextMesh label = _labels[i];
                label.characterSize = .031f;
                label.text = Wrap(string.Format(_text.Read(entry.TextID), key));
                Vector3 size = label.GetComponent<MeshRenderer>().bounds.size;
                float scale = Mathf.Min(1f, 5.1f / Mathf.Max(size.x, .001f), 2.2f / Mathf.Max(size.y, .001f));
                // 받침 크기는 유지하고 글자 크기만 표시 경계에서 보정합니다.
                label.characterSize = .031f * scale;
            }
        }

        private static string Wrap(string value)
        {
            var result = new StringBuilder();
            int width = 0;
            foreach (string word in value.Split(' '))
            {
                int wordWidth = word.Sum(character => character > 127 ? 2 : 1);
                if (width > 0)
                {
                    bool nextLine = width + wordWidth + 1 > 32;
                    result.Append(nextLine ? '\n' : ' ');
                    width = nextLine ? 0 : width + 1;
                }
                result.Append(word); width += wordWidth;
            }
            return result.ToString();
        }
    }
}
