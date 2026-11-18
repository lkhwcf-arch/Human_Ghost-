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
        // 1페이즈·일반몹 스프라이트(10fps)와 같은 프레임감으로 리그 포즈를 끊어 보여 줍니다. 판정 시간은 바꾸지 않습니다.
        [SerializeField, InspectorName("리그 표시 프레임 (fps)"), Min(1)] private int _framesPerSecond = 10;
        [SerializeField, InspectorName("대기·걷기 재생 비율 (분모 10000)"), Range(3000, 10000)] private int _loopRate = 8000;
        // 걷기 클립에서 손이 바닥을 짚는 시간(ms). 표시 프레임이 이 시간을 넘는 순간 발소리를 냅니다.
        [SerializeField, InspectorName("걷기 손 짚는 시간 (ms, 클립 기준)")] private int[] _walkSteps = { 0, 600 };
        public int FramesPerSecond => _framesPerSecond;
        public int LoopRate => _loopRate;
        // [정수화 미적용 이유] SampleAnimation에 넘길 클립 시각(초)을 프레임 간격으로 끊는 표현 계산입니다. 입력은 정수 ms에서 바꾼 값입니다.
        public static float Frame(float time, int framesPerSecond) => Mathf.Floor(time * framesPerSecond) / framesPerSecond;
        // 표시 프레임이 previous→frame으로 바뀔 때(루프 되감김 포함) 발 닿는 시간(ms) 중 하나의 프레임에 들어섰는지. 걷기 플레이어·보스가 같이 씁니다.
        public static bool EntersStep(int previous, int frame, int[] stepsMilliseconds, float framesPerSecond)
        {
            if (previous < 0 || frame < 0 || frame == previous || stepsMilliseconds == null) return false;
            foreach (int step in stepsMilliseconds)
            {
                int at = Mathf.FloorToInt(step / 1000f * framesPerSecond + .001f);
                if (frame > previous ? at > previous && at <= frame : at > previous || at <= frame) return true;
            }
            return false;
        }
        private int _flashRemainingMilliseconds;
        private long _loopMilliseconds;
        private MillisecondClock _loopClock;
        private MillisecondClock _scaledClock, _unscaledClock;
        public void FlashTransition() => _flashRemainingMilliseconds = 180;
        private PreviewFollowCamera _camera;
        // [정수화 미적용 이유] 경고 범위 그림의 중심 월드 X(Transform 표시)입니다. 판정에 쓰지 않습니다.
        private System.Func<float> _warningOrigin;
        public void ConnectWarningOrigin(System.Func<float> origin) => _warningOrigin = origin;
        private System.Action _walkStep;
        private int _walkFrame = -1;
        public void ConnectWalkStep(System.Action step) => _walkStep = step;
        private long _idleMilliseconds;
        private int _slumpMilliseconds;
        private int _elapsedMilliseconds;
        private int _impactHit = -1;
        private BossPattern _impactPattern;
        // 이동 여부(걷기/대기 그림 선택)는 보스 정수 X 변화로 판단합니다. 첫 프레임은 비교 기준이 없어 대기로 봅니다.
        private long _lastX = long.MinValue;
        // [정수화 미적용 이유] 리그 Transform 위치·크기를 복원하는 표시값입니다.
        private Vector3 _rigPosition, _rigScale;
        private SpriteRenderer[] _parts;
        private RigBindPose _bind;
        private int _health = -1;
        private int _hitFlashMilliseconds;
        private MaterialPropertyBlock _properties;
        private Camera _cameraView;
        public void ConnectCamera(PreviewFollowCamera camera) { _camera = camera; _cameraView = camera != null ? camera.GetComponent<Camera>() : null; }
        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            _rigPosition = _rig.transform.localPosition; _rigScale = _rig.transform.localScale;
            _parts = _rig.GetComponentsInChildren<SpriteRenderer>(true);
            _bind = new RigBindPose(_rig);
        }
        // 걷기 표시 프레임이 손 짚는 프레임에 들어서면 한 번 알립니다. 걷기 시작 첫 프레임은 울리지 않습니다.
        private void UpdateWalkStep(int frame)
        {
            int previous = _walkFrame; _walkFrame = frame;
            if (EntersStep(previous, frame, _walkSteps, _framesPerSecond)) _walkStep?.Invoke();
        }
        public void Show(bool value) { if (_rig.activeSelf != value) _rig.SetActive(value); }
        // 리그가 보일 때 SpriteSkin으로 변형된 부품들의 가장 높은 월드 y입니다. 숨김이면 음의 무한대입니다.
        public float VisibleTop()
        {
            float top = float.NegativeInfinity;
            if (_parts == null || !_rig.activeInHierarchy) return top;
            foreach (SpriteRenderer part in _parts)
                if (part.enabled && part.gameObject.activeInHierarchy) top = Mathf.Max(top, part.bounds.max.y);
            return top;
        }
        private void LateUpdate()
        {
            if (_boss.Actor == null) return;
            _elapsedMilliseconds = _scaledClock.AdvanceFrame(Time.deltaTime);
            _flashRemainingMilliseconds = System.Math.Max(0, _flashRemainingMilliseconds - _unscaledClock.AdvanceFrame(Time.unscaledDeltaTime));
            _flash.enabled = _flashRemainingMilliseconds > 0 && _cameraView != null;
            if (_flash.enabled)
            {
                var camera = _cameraView;
                _flash.transform.position = new Vector3(camera.transform.position.x,camera.transform.position.y,transform.position.z-.3f);
                _flash.transform.localScale = new Vector3(camera.orthographicSize*camera.aspect*2f/transform.lossyScale.x,camera.orthographicSize*2f/transform.lossyScale.y,1);
                _flash.color = new Color(1,.05f,0,_flashRemainingMilliseconds/180f*.4f);
            }
            var actor = _boss.Actor; var combat = _boss.Combat;
            // 이동/선택(0) 뒤, SpriteSkin 변형(10) 전에 같은 프레임의 뼈 포즈를 공급합니다.
            Show(actor.Phase.Phase > 0 && !actor.IsTransitioning);
            if (_health >= 0 && actor.Health < _health) _hitFlashMilliseconds = 50;
            _health = actor.Health;
            _properties.SetFloat("_FlashAmount", _hitFlashMilliseconds > 0 ? 1f : 0f);
            _properties.SetFloat("_AuraStrength", actor.IsDead ? 0f : .18f);
            _hitFlashMilliseconds = System.Math.Max(0, _hitFlashMilliseconds - _elapsedMilliseconds);
            // 1페이즈도 범위가 있는 패턴은 같은 바닥 예고를 씁니다(2B3).
            if (!_rig.activeSelf) { _dust.enabled = false; _walkFrame = -1; DrawWarning(combat); return; }
            _idleMilliseconds = checked(_idleMilliseconds + _elapsedMilliseconds);
            _loopMilliseconds = checked(_loopMilliseconds + _loopClock.AdvanceFrame(Time.deltaTime * _loopRate / 10000d));
            long x = _boss.Position.X;
            bool moving = _lastX != long.MinValue && x != _lastX; _lastX = x;
            AnimationClip clip = moving ? _walk : _idle;
            // [정수화 미적용 이유] AnimationClip.SampleAnimation은 클립 안 시각을 초(float)로만 받습니다. 경과는 정수 ms로 누적하고 넘기는 순간에만 초로 바꿉니다.
            // 아래 상태별 time도 모두 정수 ms 경과(_loop·_slump·HitElapsed·PatternElapsed)에서 같은 방식으로 바꿉니다.
            float time = _loopMilliseconds / 1000f;
            if (actor.IsDead || actor.IsExposed || actor.CurrentState == HumanGhost.Data.Definitions.StateKey.Execute)
            { clip = _slump; _slumpMilliseconds = System.Math.Min(MillisecondClock.ToMilliseconds(clip.length), _slumpMilliseconds + _elapsedMilliseconds); time = _slumpMilliseconds / 1000f; }
            else if (combat.IsAttacking)
            {
                _slumpMilliseconds = 0;
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
                        // 크게 한 번 휩쓸기: 선딜 동안 몸 뒤로 크게 감아 올리고, 판정 창에 클립 .8~.9초(앞으로 내려쓸기)를 맞춥니다.
                        time = Map(combat.HitElapsed, spec, .8f, .9f, clip.length); break;
                    case BossPatternKind.CrawlCharge:
                        clip = combat.IsWindingUp ? _idle : _walk; time = _loopMilliseconds / 1000f * (combat.IsWindingUp ? 1f : 2f); break;
                    case BossPatternKind.GroundSlam:
                        clip = _doubleSlam; time = combat.PatternElapsed / 1000f; break;
                    case BossPatternKind.DesperationLeap:
                        clip = _leap;
                        time = LeapTime(combat);
                        break;
                }
                bool impact = !combat.IsWindingUp && combat.IsAttackActive;
                bool heavy = pattern.Kind == BossPatternKind.GroundSlam || pattern.Kind == BossPatternKind.DesperationLeap;
                if (impact && heavy && combat.HitIndex % 2 == 0 && (_impactPattern != pattern || _impactHit != combat.HitIndex))
                {
                    _impactPattern = pattern; _impactHit = combat.HitIndex;
                    _camera?.Shake(.18f, 200);
                    _dust.transform.position = new Vector3(transform.position.x, _body.bounds.min.y + .05f, -.12f);
                }
            }
            else { _slumpMilliseconds = 0; _impactPattern = null; _impactHit = -1; }
            _bind.Restore();
            float sample = Frame(clip.isLooping ? Mathf.Repeat(time, clip.length) : Mathf.Min(time, clip.length), _framesPerSecond);
            UpdateWalkStep(clip == _walk ? Mathf.RoundToInt(sample * _framesPerSecond) : -1);
            clip.SampleAnimation(_rig, sample);
            var position = _rigPosition;
            if (combat.IsAirborne)
            {
                int start = combat.HitIndex == 0 ? combat.CurrentPattern.Hits[0].Windup - 900 : 0;
                position.y += Mathf.Sin(Mathf.Clamp01(Frame((combat.HitElapsed - start) / 1000f, _framesPerSecond) / .9f) * Mathf.PI) * 6f;
            }
            _rig.transform.localPosition = position;
            _rig.transform.localScale = new Vector3(_rigScale.x * combat.Facing, _rigScale.y, _rigScale.z);
            bool red = combat.IsWindingUp && !combat.IsAirborne;
            float pulse = .5f + .35f * Mathf.Sin(_idleMilliseconds / 1000f * 18f);
            if (combat.CurrentPattern?.Kind == BossPatternKind.TripleSlam && combat.HitIndex == 0)
                pulse = .25f + .75f * Mathf.Abs(Mathf.Sin(combat.HitElapsed / (float)combat.CurrentPattern.Hits[0].Windup * Mathf.PI * 3f));
            Color color = actor.IsDead ? Color.gray : red ? Color.Lerp(Color.white, new Color(1f,.15f,.1f), pulse) : actor.IsExposed ? new Color(.9f,.8f,1f) : Color.white;
            foreach (var part in _parts) { part.color = color; part.SetPropertyBlock(_properties); }
            DrawWarning(combat);
            // 충격파 흙먼지 원 = 판정 반지름(BossHitGeometry.ShockwaveReach, B06). 판정과 같은 값 하나에서 그립니다.
            var current = combat.CurrentPattern;
            int reach = current != null && combat.IsAttacking ? BossHitGeometry.ShockwaveReach(current, combat.HitIndex, combat.HitElapsed) : -1;
            _dust.enabled = reach > 0;
            if (_dust.enabled)
            {
                int wave = BossHitGeometry.IsShockwave(current, combat.HitIndex) ? combat.HitIndex : combat.HitIndex + 1;
                float progress = reach / (float)current.Areas[wave].Range;
                var colorDust = new Color(.55f,.34f,.17f,1f-.6f*progress);
                _dust.startColor = _dust.endColor = colorDust;
                _dust.transform.position = new Vector3(transform.position.x, _body.bounds.min.y + .05f, -.12f);
                Ring(_dust, reach / 100f, .12f);
            }
        }
        // 공급 도약 클립의 착지 자세는 1.2초입니다. 실제 공중 구간 끝과 두 번째 착지에도 같은 자세를 맞춥니다.
        private float LeapTime(BossCombat combat)
        {
            AttackSpec spec = combat.CurrentPattern.Hits[combat.HitIndex];
            if (combat.HitIndex % 2 == 0)
            {
                int preparation = combat.HitIndex == 0 ? spec.Windup - 900 : 0;
                if (combat.HitElapsed < preparation)
                    return Mathf.Lerp(0f, .8f, (float)combat.HitElapsed / preparation);
                if (combat.HitElapsed < spec.Windup)
                    return Mathf.Lerp(.8f, 1.2f, (float)(combat.HitElapsed - preparation) / (spec.Windup - preparation));
                return Mathf.Lerp(1.2f, 1.7f, (float)(combat.HitElapsed - spec.Windup) / spec.Active);
            }
            // 첫 착지는 짧게 재도약 자세로, 마지막 착지는 공급 클립 끝까지 회복합니다.
            float end = combat.HitIndex == 1 ? 2.05f : _leap.length;
            return Mathf.Lerp(1.7f, end, (float)combat.HitElapsed / spec.TotalTime);
        }

        // 선딜 동안 바닥에 판정 범위를 그립니다. 범위가 없는 패턴은 그리지 않습니다.
        private void DrawWarning(BossCombat combat)
        {
            var pattern = combat.CurrentPattern;
            _warning.enabled = pattern != null && combat.IsWindingUp && pattern.Areas.Count > combat.HitIndex;
            if (!_warning.enabled) return;
            var area = pattern.Areas[combat.HitIndex];
            float width = area.Range / 100f;
            float center = _warningOrigin != null ? _warningOrigin() : transform.position.x;
            if (!area.Radial) center += combat.Facing * width * .5f;
            _warning.transform.position = new Vector3(center, _body.bounds.min.y + .04f, -.1f);
            Ring(_warning, area.Radial ? width : width * .5f, .12f);
        }
        private static float Map(int elapsed, AttackSpec spec, float contact, float recovery, float end, float start = 0f)
        {
            if (elapsed < spec.Windup) return Mathf.Lerp(start, contact, (float)elapsed / spec.Windup);
            if (elapsed < spec.Windup + spec.Active) return Mathf.Lerp(contact, recovery, (float)(elapsed-spec.Windup)/spec.Active);
            return spec.Recovery == 0 ? end : Mathf.Lerp(recovery, end, (float)(elapsed-spec.Windup-spec.Active)/spec.Recovery);
        }
        private static void Ring(LineRenderer line, float radius, float height)
        {
            // 리그/부모 배율과 무관하게 판정 데이터의 월드 폭을 유지합니다.
            Vector3 scale = line.transform.lossyScale;
            float x = radius / Mathf.Max(.0001f, Mathf.Abs(scale.x));
            float y = height / Mathf.Max(.0001f, Mathf.Abs(scale.y));
            for (int i=0;i<line.positionCount;i++)
            { float a=i*2f*Mathf.PI/line.positionCount; line.SetPosition(i,new Vector3(Mathf.Cos(a)*x,Mathf.Sin(a)*y,0)); }
        }
    }
}
