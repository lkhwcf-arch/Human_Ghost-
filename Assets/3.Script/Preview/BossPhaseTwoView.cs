using HumanGhost.Gameplay.Adapters;
using UnityEngine;
namespace HumanGhost.Gameplay.Preview
{
    // 편집 가능한 기존 8뼈 리그를 샘플링합니다. 판정·HP·이동은 전투/공간 소유자만 변경합니다.
    [DefaultExecutionOrder(5)]
    public sealed class BossPhaseTwoView : MonoBehaviour
    {
        [SerializeField] private BossRuntime _boss = null;
        [SerializeField] private SpriteRenderer _body = null;
        [SerializeField] private GameObject _rig = null;
        [SerializeField] private AnimationClip _idle = null, _walk = null, _slam = null, _sweep = null, _doubleSlam = null, _leap = null, _slump = null;
        [SerializeField] private LineRenderer _warning = null, _dust = null;
        [SerializeField] private SpriteRenderer _flash = null;
        private float _flashRemaining;
        public void FlashTransition() => _flashRemaining = .18f;
        private PreviewFollowCamera _camera;
        private float _idleTime, _slumpElapsed, _dustTime = 1f;
        private int _impactHit = -1;
        private BossPattern _impactPattern;
        private float _lastX;
        private Vector3 _rigPosition, _rigScale;
        private SpriteRenderer[] _parts;
        private int _health = -1;
        private float _hitFlash;
        private MaterialPropertyBlock _properties;
        public void ConnectCamera(PreviewFollowCamera camera) => _camera = camera;
        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            _rigPosition = _rig.transform.localPosition; _rigScale = _rig.transform.localScale;
            _parts = _rig.GetComponentsInChildren<SpriteRenderer>(true);
            _lastX = transform.position.x;
        }
        public void Show(bool value) { if (_rig.activeSelf != value) _rig.SetActive(value); }
        private void LateUpdate()
        {
            if (_boss.Actor == null) return;
            _flashRemaining = Mathf.Max(0f, _flashRemaining-Time.unscaledDeltaTime);
            _flash.enabled = _flashRemaining > 0f && _camera != null;
            if (_flash.enabled)
            {
                var camera = _camera.GetComponent<Camera>();
                _flash.transform.position = new Vector3(camera.transform.position.x,camera.transform.position.y,transform.position.z-.3f);
                _flash.transform.localScale = new Vector3(camera.orthographicSize*camera.aspect*2f/transform.lossyScale.x,camera.orthographicSize*2f/transform.lossyScale.y,1);
                _flash.color = new Color(1,.05f,0,_flashRemaining/.18f*.4f);
            }
            var actor = _boss.Actor; var combat = _boss.Combat;
            // 이동/선택(0) 뒤, SpriteSkin 변형(10) 전에 같은 프레임의 뼈 포즈를 공급합니다.
            Show(actor.Phase.Phase > 0 && !actor.IsTransitioning);
            if (_health >= 0 && actor.Health < _health) _hitFlash = .05f;
            _health = actor.Health;
            _properties.SetFloat("_FlashAmount", _hitFlash > 0f ? 1f : 0f);
            _properties.SetFloat("_AuraStrength", actor.IsDead ? 0f : .18f);
            _hitFlash = Mathf.Max(0f,_hitFlash-Time.deltaTime);
            if (!_rig.activeSelf) { _warning.enabled = false; _dust.enabled = false; return; }
            _idleTime += Time.deltaTime;
            bool moving = Mathf.Abs(transform.position.x - _lastX) > .00001f; _lastX = transform.position.x;
            AnimationClip clip = moving ? _walk : _idle;
            float time = _idleTime;
            if (actor.IsDead || actor.IsExposed || actor.CurrentState == HumanGhost.Data.Definitions.StateKey.Execute)
            { clip = _slump; _slumpElapsed = Mathf.Min(clip.length, _slumpElapsed + Time.deltaTime); time = _slumpElapsed; }
            else if (combat.IsAttacking)
            {
                _slumpElapsed = 0f;
                var pattern = combat.CurrentPattern; var spec = pattern.Hits[combat.HitIndex];
                float elapsed = combat.HitElapsed / 1000f;
                switch (pattern.Kind)
                {
                    case BossPatternKind.Pillar:
                    case BossPatternKind.TripleSlam:
                        clip = _slam;
                        time = Map(combat.HitElapsed, spec, .7f, .82f, clip.length); break;
                    case BossPatternKind.Sweep:
                        clip = _sweep;
                        // 리그의 두 번째 큰 휩쓸기를 논리상 850~950ms 판정 창으로 옮겨 재생합니다.
                        time = combat.HitIndex == 0 ? Map(combat.HitElapsed, spec, .5f, .6f, .7f)
                            : Map(combat.HitElapsed, spec, 1.23f, 1.33f, clip.length, .7f); break;
                    case BossPatternKind.CrawlCharge:
                        clip = combat.IsWindingUp ? _idle : _walk; time = _idleTime * (combat.IsWindingUp ? 1f : 2f); break;
                    case BossPatternKind.GroundSlam:
                        clip = _doubleSlam; time = combat.PatternElapsed / 1000f; break;
                    case BossPatternKind.DesperationLeap:
                        clip = _leap;
                        time = combat.HitIndex < 2 ? combat.PatternElapsed / 1000f : .8f + (combat.PatternElapsed - 2100) / 1000f;
                        break;
                }
                bool impact = !combat.IsWindingUp && combat.IsAttackActive;
                bool heavy = pattern.Kind == BossPatternKind.GroundSlam || pattern.Kind == BossPatternKind.DesperationLeap;
                if (impact && heavy && combat.HitIndex % 2 == 0 && (_impactPattern != pattern || _impactHit != combat.HitIndex))
                {
                    _impactPattern = pattern; _impactHit = combat.HitIndex; _dustTime = 0f;
                    _camera?.Shake(.18f, .2f);
                    _dust.transform.position = new Vector3(transform.position.x, _body.bounds.min.y + .05f, -.12f);
                }
            }
            else { _slumpElapsed = 0f; _impactPattern = null; _impactHit = -1; }
            clip.SampleAnimation(_rig, clip.isLooping ? Mathf.Repeat(time, clip.length) : Mathf.Min(time, clip.length));
            var position = _rigPosition;
            if (combat.IsAirborne)
            {
                int start = combat.HitIndex == 0 ? combat.CurrentPattern.Hits[0].Windup - 900 : 0;
                position.y += Mathf.Sin(Mathf.Clamp01((combat.HitElapsed - start) / 900f) * Mathf.PI) * 6f;
            }
            _rig.transform.localPosition = position;
            _rig.transform.localScale = new Vector3(_rigScale.x * combat.Facing, _rigScale.y, _rigScale.z);
            bool red = combat.IsWindingUp && !combat.IsAirborne;
            float pulse = .5f + .35f * Mathf.Sin(_idleTime * 18f);
            if (combat.CurrentPattern?.Kind == BossPatternKind.TripleSlam && combat.HitIndex == 0)
                pulse = .25f + .75f * Mathf.Abs(Mathf.Sin(combat.HitElapsed / (float)combat.CurrentPattern.Hits[0].Windup * Mathf.PI * 3f));
            Color color = actor.IsDead ? Color.gray : red ? Color.Lerp(Color.white, new Color(1f,.15f,.1f), pulse) : actor.IsExposed ? new Color(.9f,.8f,1f) : Color.white;
            foreach (var part in _parts) { part.color = color; part.SetPropertyBlock(_properties); }
            _warning.enabled = combat.IsWindingUp;
            if (_warning.enabled)
            {
                var area = combat.CurrentPattern.Areas[combat.HitIndex];
                float width = area.Range / 100f;
                float center = combat.CurrentPattern.Kind == BossPatternKind.DesperationLeap && combat.IsAirborne ? _boss.LandingX : transform.position.x;
                if (!area.Radial) center += combat.Facing * width * .5f;
                _warning.transform.position = new Vector3(center, _body.bounds.min.y + .04f, -.1f);
                Ring(_warning, area.Radial ? width : width * .5f, .12f);
            }
            _dustTime += Time.deltaTime;
            _dust.enabled = _dustTime < .35f;
            if (_dust.enabled)
            {
                var colorDust = new Color(.55f,.34f,.17f,1f-_dustTime/.35f);
                _dust.startColor = _dust.endColor = colorDust;
                float radius = combat.CurrentPattern?.Kind == BossPatternKind.DesperationLeap ? 4f : 6f;
                Ring(_dust, radius * _dustTime/.35f, .12f);
            }
        }
        private static float Map(int elapsed, AttackSpec spec, float contact, float recovery, float end, float start = 0f)
        {
            if (elapsed < spec.Windup) return Mathf.Lerp(start, contact, (float)elapsed / spec.Windup);
            if (elapsed < spec.Windup + spec.Active) return Mathf.Lerp(contact, recovery, (float)(elapsed-spec.Windup)/spec.Active);
            return spec.Recovery == 0 ? end : Mathf.Lerp(recovery, end, (float)(elapsed-spec.Windup-spec.Active)/spec.Recovery);
        }
        private static void Ring(LineRenderer line, float radius, float height)
        {
            for (int i=0;i<line.positionCount;i++)
            { float a=i*2f*Mathf.PI/line.positionCount; line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*height,0)); }
        }
    }
}
