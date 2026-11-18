using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 플레이어 이동 뒤 맵 범위 안에서 따라갑니다. 현재 보스방도 추적하며 장부방의 상하 보기만 조립자가 끕니다.
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Camera))]
    public sealed class PreviewFollowCamera : MonoBehaviour
    {
        [SerializeField, InspectorName("따라갈 대상")] private Transform _target = null;
        // [정수화 미적용 이유] 카메라가 따라가는 맵 범위·높이 보정(Transform 표시)입니다. 게임 판정에 쓰지 않습니다.
        [SerializeField, InspectorName("맵 왼쪽 끝")] private float _minX = 0f;
        [SerializeField, InspectorName("맵 오른쪽 끝")] private float _maxX = 80f;
        [SerializeField, InspectorName("카메라 최저 높이")] private float _minY = 0f;
        [SerializeField, InspectorName("대상 기준 카메라 높이")] private float _heightOffset = 2f;
        [SerializeField, InspectorName("따라가기 시간 (ms)"), Min(0)] private int _smoothMilliseconds = 120;
        // [정수화 미적용 이유] 카메라 Transform·orthographicSize 계산의 표시 상태·보정값입니다. 입력 유지·흔들림 시간은 정수 ms입니다.
        [SerializeField, InspectorName("바라보는 쪽 여백"), Min(0f)] private float _lookAhead = 0.8f;
        [SerializeField, InspectorName("착지 높이 구도 보정 (tile_x100)")] private int _groundHeightBiasUnits = -200;
        [SerializeField, InspectorName("위아래 보기 대기 (ms)"), Min(0)] private int _lookDelayMilliseconds = 250;
        [SerializeField, InspectorName("위아래 보기 거리 (tile_x100)"), Min(0)] private int _lookDistanceUnits = 300;
        [SerializeField, InspectorName("아래 보기 거리 (tile_x100)"), Min(0)] private int _lookDownDistanceUnits = 600;
        private PlayerRuntime _player;
        private PlayerLocomotionInput _input;
        // [정수화 미적용 이유] 카메라 Transform·orthographicSize 계산의 표시 상태입니다. 입력 유지·흔들림 시간은 정수 ms입니다.
        private float _groundHeight, _lookOffset;
        private int _lookHeldMilliseconds;
        private MillisecondClock _lookClock, _shakeClock;
        private int _lookDirection;
        private int _lookBlendRemainingMilliseconds;
        private bool _hasGroundHeight;
        private Camera _camera;
        // [정수화 미적용 이유] 카메라 Transform·orthographicSize 계산의 표시 상태·보정값입니다. 입력 유지·흔들림 시간은 정수 ms입니다.
        private Vector2 _velocity;
        private int _shakeRemainingMilliseconds;
        private int _shakeDurationMilliseconds;
        // [정수화 미적용 이유] 카메라 Transform·orthographicSize 계산의 표시 상태·보정값입니다. 입력 유지·흔들림 시간은 정수 ms입니다.
        private float _shakeStrength;
        private Vector3 _shakeOffset;
        private bool _fixed;
        // [정수화 미적용 이유] 카메라 Transform·orthographicSize 계산의 표시 상태·보정값입니다. 입력 유지·흔들림 시간은 정수 ms입니다.
        private Vector2 _fixedPosition;
        private float _baseSize;
        public bool ShakeEnabled { get; set; } = true;
        // 화면이 방 크기와 같은 스테이지(장부방)는 위아래 보기로 맵 밖 채움 그림이 드러나므로 끕니다.
        public bool LookEnabled { get; set; } = true;

        // 테스트 전용 확대 배율입니다. 1보다 크면 더 확대합니다(화면 크기 = 스테이지 값 / 배율). 스테이지를 옮겨도 유지하고 Play마다 1로 돌아갑니다.
        // [정수화 미적용 이유] 개발 시험용 카메라 배율·위치 보정(표시)입니다.
        public const float MinTestZoom = 0.5f, MaxTestZoom = 2f;
        public static float TestZoomScale { get; private set; } = 1f;
        public static Vector2 TestPositionOffset { get; private set; } = Vector2.zero;
        public float BaseSize => _baseSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTestOverrides() { TestZoomScale = 1f; TestPositionOffset = Vector2.zero; }

        public void SetTestPositionOffset(Vector2 offset)
        {
            if (float.IsNaN(offset.x) || float.IsInfinity(offset.x) || float.IsNaN(offset.y) || float.IsInfinity(offset.y))
                throw new System.ArgumentOutOfRangeException(nameof(offset));
            TestPositionOffset = offset;
            if (_camera == null) _camera = GetComponent<Camera>();
            _velocity = Vector2.zero;
            if (_target != null || _fixed) transform.position = GetGoal() + _shakeOffset;
        }

        public void SetTestZoom(float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale)) throw new System.ArgumentOutOfRangeException(nameof(scale));
            TestZoomScale = Mathf.Clamp(scale, MinTestZoom, MaxTestZoom);
            ApplySize();
        }

        private void ApplySize()
        {
            if (_baseSize <= 0f) return;
            if (_camera == null) _camera = GetComponent<Camera>();
            _camera.orthographicSize = _baseSize / TestZoomScale;
        }

        // 스테이지를 조립한 뒤 범위를 정하고 대상으로 바로 옮깁니다.
        public void SetArea(float minX, float maxX, float minY, float size)
        {
            _fixed = false;
            ResetFocus();
            _minX = minX; _maxX = maxX; _minY = minY;
            _camera = GetComponent<Camera>();
            _baseSize = size;
            ApplySize();
            _velocity = Vector2.zero;
            if (_target != null) transform.position = GetGoal();
        }

        public void SetFixed(Vector2 position, float size)
        {
            _fixed = true;
            ResetFocus();
            _fixedPosition = position;
            _camera = GetComponent<Camera>();
            _baseSize = size;
            ApplySize();
            _velocity = Vector2.zero;
            transform.position = GetGoal();
        }

        // 시간 기준은 멈추지 않는 시간입니다. 설정에서 끌 수 있습니다(C11).
        public void Shake(float strength, int milliseconds)
        {
            if (!ShakeEnabled) return;
            if (milliseconds < 0) throw new System.ArgumentOutOfRangeException(nameof(milliseconds));
            _shakeStrength = strength; _shakeDurationMilliseconds = milliseconds; _shakeRemainingMilliseconds = milliseconds;
            _shakeClock = default;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            // 맵/투명 레이어 밖도 게임용 불투명 암색으로 지웁니다.
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(.012f,.008f,.01f,1f);
            if (_target != null) _player = _target.GetComponentInParent<PlayerRuntime>();
            if (_player != null) _input = _player.GetComponent<PlayerLocomotionInput>();
        }
        private void OnEnable() { ResetFocus(); if (_target != null) transform.position = GetGoal(); }

        private void ResetFocus()
        {
            _hasGroundHeight = _target != null;
            _groundHeight = _target != null ? _target.position.y : 0f;
            _lookBlendRemainingMilliseconds = 0;
            _lookHeldMilliseconds = 0; _lookOffset = 0f; _lookDirection = 0; _lookClock = default;
        }
        private void UpdateFocus(int milliseconds)
        {
            if (_fixed || _target == null) return;
            var actor = _player != null ? _player.Actor : null;
            float height = _target.position.y;
            if (!_hasGroundHeight || actor == null || actor.Locomotion.IsGrounded || actor.IsHanging)
            { _groundHeight = height; _hasGroundHeight = true; }
            else
            {
                // 긴 낙하에서 플레이어가 화면 아래로 사라지는 경우만 추적을 재개합니다. 일반 점프는 높이를 고정합니다.
                float lowerMargin = _camera.orthographicSize * .65f;
                if (height < _groundHeight - lowerMargin) _groundHeight = height + lowerMargin;
            }
            if (milliseconds <= 0) return;
            int direction = LookEnabled && actor != null && actor.Locomotion.IsGrounded && actor.Locomotion.CanMove && _input != null
                ? _input.VerticalDirection : 0;
            if (direction != _lookDirection) { _lookDirection = direction; _lookHeldMilliseconds = 0; }
            _lookHeldMilliseconds = direction == 0 ? 0 : _lookHeldMilliseconds + System.Math.Min(milliseconds, int.MaxValue - _lookHeldMilliseconds);
            int distanceUnits = direction < 0 ? _lookDownDistanceUnits : _lookDistanceUnits;
            long distance = IntegerPosition.FromData(distanceUnits);
            float worldDistance = _player != null ? _player.ToWorld(distance) : distanceUnits / 100f;
            float nextLook = direction != 0 && _lookHeldMilliseconds >= _lookDelayMilliseconds ? direction * worldDistance : 0f;
            _lookBlendRemainingMilliseconds = nextLook != _lookOffset ? 800 : System.Math.Max(0, _lookBlendRemainingMilliseconds - milliseconds);
            _lookOffset = nextLook;
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            UpdateFocus(_lookClock.AdvanceFrame(Time.deltaTime));
            Vector3 goal = GetGoal();
            Vector3 position = transform.position - _shakeOffset;
            // 일시정지·히트스톱의 경과 시간 0에서는 SmoothDamp가 NaN을 낼 수 있습니다.
            if (Time.deltaTime > 0f)
            {
                position.x = Mathf.SmoothDamp(position.x, goal.x, ref _velocity.x, _smoothMilliseconds / 1000f);
                position.y = Mathf.SmoothDamp(position.y, goal.y, ref _velocity.y, (_lookBlendRemainingMilliseconds > 0 ? 300 : _smoothMilliseconds) / 1000f);
            }
            _shakeOffset = Vector3.zero;
            if (_shakeRemainingMilliseconds > 0)
            {
                _shakeRemainingMilliseconds = System.Math.Max(0, _shakeRemainingMilliseconds - _shakeClock.AdvanceFrame(Time.unscaledDeltaTime));
                float strength = _shakeStrength * (_shakeRemainingMilliseconds / (float)_shakeDurationMilliseconds);
                _shakeOffset = new Vector3(UnityEngine.Random.Range(-strength, strength), UnityEngine.Random.Range(-strength, strength), 0f);
            }
            transform.position = position + _shakeOffset;
        }

        private Vector3 GetGoal()
        {
            if (_fixed) return new Vector3(_fixedPosition.x, _fixedPosition.y, transform.position.z) + (Vector3)TestPositionOffset;
            int facing = _player != null && _player.Actor != null ? _player.Actor.Facing : 0;
            // 기본 구도에 더하는 테스트 이동량입니다. 맵 가장자리에서도 구도를 직접 비교할 수 있습니다.
            return Compose(_target.position.x, _groundHeight, facing) + new Vector3(0f, _lookOffset, 0f) + (Vector3)TestPositionOffset;
        }

        // 대상이 이 위치에 서서 facing 쪽을 볼 때의 기본 구도입니다(위아래 보기·흔들림·시험 보정 제외). 배경 시차 기준점처럼 도착 방향과 무관한 기준에 씁니다.
        public Vector3 GoalFor(Vector3 target, int facing)
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_fixed) return new Vector3(_fixedPosition.x, _fixedPosition.y, transform.position.z);
            return Compose(target.x, target.y, facing);
        }

        private Vector3 Compose(float targetX, float groundHeight, int facing)
        {
            float halfWidth = _camera.orthographicSize * _camera.aspect;
            float focusX = targetX + facing * _lookAhead;
            float x = _maxX - _minX <= halfWidth * 2f ? (_minX + _maxX) * 0.5f : Mathf.Clamp(focusX, _minX + halfWidth, _maxX - halfWidth);
            float scale = _player != null ? _player.ToWorld(IntegerPosition.UnitsPerTile) : 1f;
            float y = Mathf.Max(_minY, groundHeight + _heightOffset + _groundHeightBiasUnits / 100f * scale);
            return new Vector3(x, y, transform.position.z);
        }
    }
}
