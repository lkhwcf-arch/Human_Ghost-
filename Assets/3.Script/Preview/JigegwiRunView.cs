using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 이동 결과를 관찰해 리그만 재생합니다. AI·속도·판정·원본 그림은 변경하지 않습니다.
    [DefaultExecutionOrder(5)]
    public sealed class JigegwiRunView : MonoBehaviour
    {
        [SerializeField] private MonsterRuntime _monster = null;
        [SerializeField] private SpriteRenderer _source = null;
        [SerializeField] private SpriteRenderer _ground = null;
        [SerializeField] private GameObject _rig = null;
        [SerializeField] private SpriteRenderer _skin = null;
        [SerializeField] private AnimationClip _run = null;
        // [정수화 미적용 이유] 원화 리그의 발 위치·크기 표시 보정과 이동 감지용 Transform 관측값입니다. 이동 상태는 정수 좌표입니다.
        [SerializeField] private float _sourceFeet = 0f;
        [SerializeField] private AnimationCurve _groundOffset = new AnimationCurve();
        // 기존 지게귀 스프라이트(10fps)와 같은 프레임감으로 끊어 보여 줍니다. 이동·판정 속도는 바꾸지 않습니다.
        [SerializeField, InspectorName("리그 표시 프레임 (fps)"), Min(1)] private int _framesPerSecond = 10;
        [SerializeField, InspectorName("돌진 재생 비율 (분모 10000)"), Min(10000)] private int _chargeRate = 14000;
        // [정수화 미적용 이유] 원화 리그의 발 위치·크기 표시 보정과 이동 감지용 Transform 관측값입니다. 이동 상태는 정수 좌표입니다.
        private Vector3 _baseScale;
        private float _lastX;
        private long _timeMilliseconds;
        private MillisecondClock _clock;
        private bool _visible;
        private MaterialPropertyBlock _properties;

        private void Awake()
        {
            if (_monster == null || _source == null || _ground == null || _rig == null || _skin == null || _run == null)
                throw new System.InvalidOperationException("지게귀 달리기 리그의 참조를 연결해 주세요.");
            _baseScale = _rig.transform.localScale;
            _properties = new MaterialPropertyBlock();
            _lastX = transform.position.x;
        }
        private void OnEnable() { _lastX = transform.position.x; _timeMilliseconds = 0; _clock = default; Show(false); }
        private void OnDisable() { Show(false); }
        private void Show(bool visible)
        {
            _visible = visible;
            if (_source != null) _source.enabled = !visible;
            if (_rig != null && _rig.activeSelf != visible) _rig.SetActive(visible);
        }
        private void LateUpdate()
        {
            MonsterActor actor = _monster.Actor;
            bool eligible = actor != null && !actor.IsDead && (actor.CurrentState == StateKey.Walk || actor.IsCharging);
            bool moved = Mathf.Abs(transform.position.x - _lastX) > .00001f;
            _lastX = transform.position.x;
            // 히트스톱 중에는 멈춘 리그를 유지합니다. 피격·패링·처형은 즉시 기존 표시로 돌아갑니다.
            bool visible = eligible && (Time.deltaTime <= 0f ? _visible : moved);
            if (!visible) { Show(false); _timeMilliseconds = 0; _clock = default; return; }
            Show(true);
            _timeMilliseconds = checked(_timeMilliseconds + _clock.AdvanceFrame(Time.deltaTime * (actor.IsCharging ? _chargeRate : 10000) / 10000d));
            _timeMilliseconds %= System.Math.Max(1, MillisecondClock.ToMilliseconds(_run.length));
            // [정수화 미적용 이유] AnimationClip.SampleAnimation은 클립 안 시각을 초(float)로만 받습니다. 경과는 정수 ms로 누적하고 넘기는 순간에만 초로 바꿉니다.
            float sampleTime = BossPhaseTwoView.Frame(_timeMilliseconds / 1000f, _framesPerSecond);
            _run.SampleAnimation(_rig, sampleTime);
            _rig.transform.localScale = new Vector3(_baseScale.x * (actor.Facing > 0 ? -1f : 1f), _baseScale.y, _baseScale.z);
            Vector3 position = _source.transform.position;
            position.y = _ground.bounds.min.y - (_sourceFeet + _groundOffset.Evaluate(sampleTime)) * _rig.transform.lossyScale.y;
            _rig.transform.position = position;
            _skin.color = _source.color;
            _skin.sortingLayerID = _source.sortingLayerID;
            _skin.sortingOrder = _source.sortingOrder;
            _source.GetPropertyBlock(_properties);
            _skin.SetPropertyBlock(_properties);
        }
    }
}
