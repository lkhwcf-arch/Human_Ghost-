using System;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 맵에 이미 있는 앵커의 번역 문구만 표시합니다. 경로 개방·배치는 변경하지 않습니다.
    public sealed class WorldGuideLabels : MonoBehaviour
    {
        private StageContentSet.WorldTextBinding[] _bindings;
        private TextMesh[] _labels;
        private LanguageTextSource _text;
        private SubscriptionScope _scope;
        public static void AddTo(Transform map, TextMesh prototype, IReadOnlyDataCatalog data, StageContentSet.WorldTextBinding[] bindings)
        {
            if (bindings == null || bindings.Length == 0) return;
            if (map == null || prototype == null || data == null) throw new ArgumentException("월드 안내의 맵·글꼴·데이터를 연결해 주세요.");
            var anchors = new Transform[bindings.Length]; var text = new LanguageTextSource(data);
            for (int i = 0; i < bindings.Length; i++)
            {
                anchors[i] = map.Find(bindings[i].Anchor) ?? throw new InvalidOperationException("이정표 앵커 누락: " + bindings[i].Anchor);
                if (bindings[i].TextIds.Length == 0) throw new InvalidOperationException("이정표 문구를 연결해 주세요.");
                foreach (int id in bindings[i].TextIds) text.Read(id);
            }
            var root = new GameObject("WorldGuideLabels"); root.SetActive(false); root.transform.SetParent(map, false);
            try
            {
                var view = root.AddComponent<WorldGuideLabels>(); view._text = text; view._bindings = bindings; view._labels = new TextMesh[bindings.Length];
                for (int i = 0; i < bindings.Length; i++)
                {
                    var label = new GameObject("Sign_" + i, typeof(TextMesh)); label.transform.SetParent(root.transform, false);
                    label.transform.position = anchors[i].position + new Vector3(bindings[i].OffsetUnits.x / 100f, bindings[i].OffsetUnits.y / 100f, -.1f);
                    var mesh = label.GetComponent<TextMesh>(); mesh.font = prototype.font; mesh.fontSize = 96; mesh.characterSize = .025f;
                    mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = new Color(1f, .96f, .86f, 1f);
                    var renderer = label.GetComponent<MeshRenderer>(); renderer.sharedMaterial = prototype.GetComponent<MeshRenderer>().sharedMaterial; renderer.sortingOrder = 62;
                    view._labels[i] = mesh;
                }
                root.SetActive(true);
            }
            catch { Destroy(root); throw; }
        }
        private void OnEnable()
        {
            if (_labels == null) return;
            var scope = _scope = new SubscriptionScope(); Action refresh = scope.Guard(RefreshSafely);
            scope.Add(() => LanguageSettings.Changed -= refresh); LanguageSettings.Changed += refresh; RefreshSafely();
        }
        private void OnDisable() { _scope?.Dispose(); _scope = null; }
        private void RefreshSafely() { try { Refresh(); } catch (Exception error) { ErrorPopupHost.WriteError(error, this); } }
        private void Refresh()
        {
            for (int i = 0; i < _labels.Length; i++)
            {
                var ids = _bindings[i].TextIds; var lines = new string[ids.Length];
                for (int j = 0; j < ids.Length; j++) lines[j] = _text.Read(ids[j]);
                _labels[i].text = string.Join("\n", lines);
            }
        }
    }
}
