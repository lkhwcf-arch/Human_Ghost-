using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 확인 장면 전용: 플레이어 이동 뒤 카메라를 맵 범위 안에서 따라가게 합니다. 보스방 고정 카메라·연출은 소유하지 않습니다.
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Camera))]
    public sealed class PreviewFollowCamera : MonoBehaviour
    {
        [SerializeField, InspectorName("따라갈 대상")] private Transform _target = null;
        [SerializeField, InspectorName("맵 왼쪽 끝")] private float _minX = 0f;
        [SerializeField, InspectorName("맵 오른쪽 끝")] private float _maxX = 80f;
        [SerializeField, InspectorName("카메라 최저 높이")] private float _minY = 0f;
        [SerializeField, InspectorName("대상 기준 카메라 높이")] private float _heightOffset = 2f;
        [SerializeField, InspectorName("따라가기 시간 (초)"), Min(0f)] private float _smoothSeconds = 0.12f;
        private Camera _camera;
        private Vector2 _velocity;
        private float _shakeRemaining;
        private float _shakeDuration;
        private float _shakeStrength;
        private Vector3 _shakeOffset;
        public bool ShakeEnabled { get; set; } = true;

        // 시간 기준은 멈추지 않는 시간입니다. 설정에서 끌 수 있습니다(C11).
        public void Shake(float strength, float seconds)
        {
            if (!ShakeEnabled) return;
            _shakeStrength = strength; _shakeDuration = seconds; _shakeRemaining = seconds;
        }

        private void Awake() => _camera = GetComponent<Camera>();
        private void OnEnable() { if (_target != null) transform.position = GetGoal(); }

        private void LateUpdate()
        {
            if (_target == null) return;
            Vector3 goal = GetGoal();
            Vector3 position = transform.position - _shakeOffset;
            position.x = Mathf.SmoothDamp(position.x, goal.x, ref _velocity.x, _smoothSeconds);
            position.y = Mathf.SmoothDamp(position.y, goal.y, ref _velocity.y, _smoothSeconds);
            _shakeOffset = Vector3.zero;
            if (_shakeRemaining > 0f)
            {
                _shakeRemaining -= Time.unscaledDeltaTime;
                float strength = _shakeStrength * Mathf.Clamp01(_shakeRemaining / _shakeDuration);
                _shakeOffset = new Vector3(UnityEngine.Random.Range(-strength, strength), UnityEngine.Random.Range(-strength, strength), 0f);
            }
            transform.position = position + _shakeOffset;
        }

        private Vector3 GetGoal()
        {
            float halfWidth = _camera.orthographicSize * _camera.aspect;
            float x = _maxX - _minX <= halfWidth * 2f ? (_minX + _maxX) * 0.5f : Mathf.Clamp(_target.position.x, _minX + halfWidth, _maxX - halfWidth);
            return new Vector3(x, Mathf.Max(_minY, _target.position.y + _heightOffset), transform.position.z);
        }
    }
}
