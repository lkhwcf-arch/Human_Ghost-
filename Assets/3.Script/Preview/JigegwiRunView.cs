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
        [SerializeField] private float _sourceFeet = 0f;
        // 기존 지게귀 스프라이트(10fps)와 같은 프레임감으로 끊어 보여 줍니다. 이동·판정 속도는 바꾸지 않습니다.
        [SerializeField, InspectorName("리그 표시 프레임 (fps)"), Min(1f)] private float _framesPerSecond = 10f;
        [SerializeField, InspectorName("돌진 재생 배율"), Min(1f)] private float _chargeSpeed = 1.4f;
        private Vector3 _baseScale;
        private float _lastX, _time;
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
        private void OnEnable() { _lastX = transform.position.x; _time = 0f; Show(false); }
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
            if (!visible) { Show(false); _time = 0f; return; }
            Show(true);
            _time = Mathf.Repeat(_time + Time.deltaTime * (actor.IsCharging ? _chargeSpeed : 1f), _run.length);
            _run.SampleAnimation(_rig, BossPhaseTwoView.Frame(_time, _framesPerSecond));
            _rig.transform.localScale = new Vector3(_baseScale.x * (actor.Facing > 0 ? -1f : 1f), _baseScale.y, _baseScale.z);
            Vector3 position = _source.transform.position;
            position.y = _ground.bounds.min.y - _sourceFeet * _rig.transform.lossyScale.y;
            _rig.transform.position = position;
            _skin.color = _source.color;
            _skin.sortingLayerID = _source.sortingLayerID;
            _skin.sortingOrder = _source.sortingOrder;
            _source.GetPropertyBlock(_properties);
            _skin.SetPropertyBlock(_properties);
        }
    }
}
