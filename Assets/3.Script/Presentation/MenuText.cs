using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 임시 글자 메뉴의 공통 표시·마우스 연결입니다. 왼쪽 버튼만 결정으로 씁니다(O01_3).
    internal static class MenuText
    {
        public static readonly Color Selected = Color.white;
        public static readonly Color Normal = new Color(0.55f, 0.53f, 0.5f);

        public static void SetSelected(Text text, bool selected) => text.color = selected ? Selected : Normal;

        public static void Connect(GameObject target, Action hover, Action click)
        {
            var trigger = target.AddComponent<EventTrigger>();
            if (hover != null) Add(trigger, EventTriggerType.PointerEnter, _ => hover());
            Add(trigger, EventTriggerType.PointerClick, data =>
            {
                if (data is PointerEventData pointer && pointer.button == PointerEventData.InputButton.Left) click();
            });
        }

        private static void Add(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }
    }
}
