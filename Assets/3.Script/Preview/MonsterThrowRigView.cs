using System;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 투척 판정의 진행률을 같은 리그와 발사 손에 공급합니다. AI·피해·비행 시간은 기존 몬스터가 소유합니다.
    [DefaultExecutionOrder(5)]
    public sealed class MonsterThrowRigView : MonoBehaviour
    {
        [SerializeField] private MonsterRuntime _monster = null;
        [SerializeField] private SpriteRenderer _source = null, _ground = null, _skin = null;
        [SerializeField] private GameObject _rig = null, _heldStone = null;
        [SerializeField] private Transform _hand = null;
        [SerializeField] private AnimationClip _throw = null;
        [SerializeField, InspectorName("높이 던지기 클립 (선택)")] private AnimationClip _throwHigh = null;
        [SerializeField] private float _sourceFeet = 0f;
        private Vector3 _scale;
        private RigBindPose _bind;
        private MaterialPropertyBlock _properties;
        private SpriteRenderer _heldRenderer, _handOverlay;
        private void Awake()
        {
            if (_monster == null || _source == null || _ground == null || _skin == null || _rig == null ||
                _hand == null || _throw == null || _heldStone == null)
                throw new InvalidOperationException("투척 리그의 적·그림·손·돌·클립을 연결해 주세요.");
            _scale = _rig.transform.localScale; _bind = new RigBindPose(_rig);
            _properties = new MaterialPropertyBlock();
            _heldRenderer = _heldStone.GetComponent<SpriteRenderer>();
            _handOverlay = _rig.transform.Find("HandOverlay")?.GetComponent<SpriteRenderer>();
        }
        public Vector3 ProjectilePosition { get { Sample(); return _hand.position; } }
        private void LateUpdate()
        {
            var actor = _monster.Actor;
            bool visible = actor != null && !actor.IsDead && actor.CurrentPattern != null && actor.CurrentPattern.IsProjectile;
            Show(visible);
            if (visible) Sample();
        }
        private void Sample()
        {
            var actor = _monster.Actor; var pattern = actor?.CurrentPattern;
            if (pattern == null || !pattern.IsProjectile) return;
            Show(true); _bind.Restore();
            AnimationClip clip = pattern.IsLob && _throwHigh != null ? _throwHigh : _throw;
            float time = (float)AttackAnimationTiming.FrameAt(pattern.Attack, actor.AttackElapsed, 0, 5, 12, 17) / clip.frameRate;
            clip.SampleAnimation(_rig, time);
            _rig.transform.localScale = new Vector3(_scale.x * (actor.Facing > 0 ? -1f : 1f), _scale.y, _scale.z);
            Vector3 position = _source.transform.position;
            position.y = _ground.bounds.min.y - _sourceFeet * _rig.transform.lossyScale.y;
            _rig.transform.position = position;
            _heldStone.SetActive(actor.AttackElapsed < pattern.Attack.Windup);
            _skin.color = _source.color;
            _skin.sortingLayerID = _source.sortingLayerID; _skin.sortingOrder = _source.sortingOrder;
            // 그리는 순서: 항아리 본체 < 돌 < 팔 덮개. 돌이 손 뒤에 쥐어진 것처럼 보입니다.
            if (_heldRenderer != null) { _heldRenderer.sortingLayerID = _source.sortingLayerID; _heldRenderer.sortingOrder = _source.sortingOrder + 1; }
            if (_handOverlay != null)
            {
                _handOverlay.sortingLayerID = _source.sortingLayerID; _handOverlay.sortingOrder = _source.sortingOrder + 2;
                _handOverlay.color = _source.color; _handOverlay.SetPropertyBlock(_properties);
            }
            _source.GetPropertyBlock(_properties); _skin.SetPropertyBlock(_properties);
        }
        private void Show(bool value)
        {
            if (_source != null) _source.enabled = !value;
            if (_rig != null && _rig.activeSelf != value) _rig.SetActive(value);
        }
        private void OnDisable() => Show(false);
    }
}
