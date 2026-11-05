using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Presentation
{
    // 창을 열 때만 표시 계약을 확인합니다. 입력을 넘긴 채 보이지 않는 화면에 갇히지 않게 합니다.
    internal static class ScreenVisibility
    {
        public static void RequireVisible(Canvas canvas)
        {
            if (canvas == null || !canvas.isActiveAndEnabled)
                throw new InvalidOperationException("표시할 화면의 Canvas가 없거나 비활성 상태입니다.");
            Canvas.ForceUpdateCanvases();
            for (Transform current = canvas.transform; current != null; current = current.parent)
            {
                Vector3 scale = current.localScale;
                if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f))
                    throw new InvalidOperationException($"화면을 표시할 수 없습니다. 오브젝트 {current.name}의 가로 또는 세로 배율이 0입니다.");
            }
            var rect = canvas.transform as RectTransform;
            if (rect == null || rect.rect.width <= 0f || rect.rect.height <= 0f)
                throw new InvalidOperationException($"화면 {canvas.name}의 표시 영역이 비어 있습니다. RectTransform 크기·앵커를 확인해 주세요.");
        }
    }
}
