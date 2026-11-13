using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Presentation
{
    // UI를 비활성 상태로 생성합니다. 호출자가 연결을 마친 뒤 반환된 부모를 활성화하고 소유합니다.
    public static class UiPrefabScope
    {
        public static T Create<T>(T prefab, Transform parent, out GameObject root) where T : Component
        {
            if (prefab == null || parent == null) throw new ArgumentNullException(nameof(prefab), "UI 프리팹과 부모를 연결해 주세요.");
            root = new GameObject("UiScope", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            try { return UnityEngine.Object.Instantiate(prefab, root.transform); }
            catch { UnityEngine.Object.Destroy(root); root = null; throw; }
        }
    }
}
