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
        // [정수화 미적용 이유] 원화 리그의 발 위치·크기 표시 보정값입니다.
        [SerializeField] private float _sourceFeet = 0f;
        private Vector3 _scale;
        private RigBindPose _bind;
        private MaterialPropertyBlock _properties;
        private SpriteRenderer _heldRenderer, _handOverlay;
        // 마지막 샘플 키(패턴·클립·공격 경과 ms·Facing)입니다. 같으면 포즈 복원·SampleAnimation을 건너뜁니다(같은 프레임 ProjectilePosition 재조회·경직 정지 포함). -1은 무효입니다.
        // 표시 프레임은 경과에서 보간한 소수라 정수 프레임 번호를 키로 쓰면 동작이 끊겨 보이므로 경과 ms를 키로 씁니다.
        private MonsterPattern _sampledPattern;
        private AnimationClip _sampledClip;
        private int _sampledElapsed = -1, _sampledFacing;
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
        // [정수화 미적용 이유] 던지는 손의 Transform 위치입니다. 투사체는 받는 즉시 정수 좌표로 바꿉니다.
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
            Show(true);
            AnimationClip clip = pattern.IsLob && _throwHigh != null ? _throwHigh : _throw;
            int elapsed = actor.AttackElapsed;
            if (pattern != _sampledPattern || clip != _sampledClip || elapsed != _sampledElapsed || actor.Facing != _sampledFacing)
            {
                _sampledPattern = pattern; _sampledClip = clip; _sampledElapsed = elapsed; _sampledFacing = actor.Facing;
                _bind.Restore();
                // [정수화 미적용 이유] 정수 ms 공격 경과에서 고른 프레임 번호를 SampleAnimation이 받는 클립 시각(초)으로 바꿉니다.
                float time = (float)AttackAnimationTiming.FrameAt(pattern.Attack, elapsed, 0, 5, 12, 17) / clip.frameRate;
                clip.SampleAnimation(_rig, time);
                _rig.transform.localScale = new Vector3(_scale.x * (actor.Facing > 0 ? -1f : 1f), _scale.y, _scale.z);
            }
            Vector3 position = _source.transform.position;
            position.y = _ground.bounds.min.y - _sourceFeet * _rig.transform.lossyScale.y;
            _rig.transform.position = position;
            bool held = actor.AttackElapsed < pattern.Attack.Windup;
            if (_heldStone.activeSelf != held) _heldStone.SetActive(held);
            // 그리는 순서: 항아리 본체 < 돌 < 팔 덮개. 돌이 손 뒤에 쥐어진 것처럼 보입니다. 바뀐 값만 대입합니다.
            Match(_skin, 0, true);
            if (_heldRenderer != null) Match(_heldRenderer, 1, false);
            // 피격 번쩍임은 MaterialPropertyBlock이라 매번 원본에서 읽어 같은 값으로 전달합니다.
            _source.GetPropertyBlock(_properties); _skin.SetPropertyBlock(_properties);
            if (_handOverlay != null) { Match(_handOverlay, 2, true); _handOverlay.SetPropertyBlock(_properties); }
        }
        private void Match(SpriteRenderer target, int orderOffset, bool color)
        {
            int layer = _source.sortingLayerID, order = _source.sortingOrder + orderOffset;
            if (target.sortingLayerID != layer) target.sortingLayerID = layer;
            if (target.sortingOrder != order) target.sortingOrder = order;
            if (color && target.color != _source.color) target.color = _source.color;
        }
        private void Show(bool value)
        {
            if (_source != null) _source.enabled = !value;
            if (_rig != null && _rig.activeSelf != value) _rig.SetActive(value);
            // 투척 종료·비활성화 때 캐시를 비워 다음 공격이 이전 손 위치를 쓰지 않게 합니다.
            if (!value) _sampledElapsed = -1;
        }
        private void OnDisable() => Show(false);
    }
}
