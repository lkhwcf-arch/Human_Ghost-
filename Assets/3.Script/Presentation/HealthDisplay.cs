using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Presentation
{
    // Canvas 아래의 체력바 프리팹입니다. 외부 조립 코드가 준비된 플레이어를 명시적으로 전달합니다.
    public sealed class HealthDisplay : MonoBehaviour
    {
        [SerializeField, InspectorName("체력 막대 영역")] private RectTransform _fill = null;
        private HealthDisplayBinding _binding;

        public bool IsBound => _binding != null && _binding.IsBound;

        public void Bind(PlayerActor source)
        {
            if (this == null) throw new InvalidOperationException("이미 제거된 체력바에는 대상을 연결할 수 없습니다.");
            if (_fill == null) throw new InvalidOperationException("체력바의 막대 영역을 프리팹에 연결해주세요.");
            if (_fill == transform) throw new InvalidOperationException("체력바의 막대 영역은 별도 자식이어야 합니다.");
            _binding ??= new HealthDisplayBinding(Show);
            _binding.Bind(source);
            _binding.SetActive(isActiveAndEnabled);
        }

        // 풀 반환이나 다른 스테이지에 전달하기 전에 호출합니다. 단순 숨김은 같은 대상 연결을 유지합니다.
        public void Unbind()
        {
            _binding?.Unbind();
            Show(0, 1);
        }

        private void OnEnable() => _binding?.SetActive(true);
        private void OnDisable() => _binding?.SetActive(false);
        private void OnDestroy()
        {
            _binding?.Dispose();
            _binding = null;
        }

        private void Show(int current, int maximum)
        {
            if (_fill == null) return;
            // 정수 체력은 그대로 유지하고 화면 너비로 전달하는 경계에서만 비율로 변환합니다.
            float ratio = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
            _fill.anchorMax = new Vector2(ratio, 1f);
        }
    }
}
