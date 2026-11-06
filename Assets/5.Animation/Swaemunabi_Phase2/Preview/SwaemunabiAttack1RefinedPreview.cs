using UnityEngine;

// 기존 전투 코드를 호출하지 않는 Attack1 비교 전용 재생기.
// 뼈 포즈는 편집 가능한 .anim에서 재생하고, 이 코드는 비교와 전환만 담당합니다.
[DefaultExecutionOrder(10000)]
public sealed class SwaemunabiAttack1RefinedPreview : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _reference;
    [SerializeField] private Transform _actor;
    [SerializeField] private Transform _referenceActor;
    [SerializeField] private Sprite[] _attackFrames;
    [SerializeField] private Sprite[] _walkFrames;
    [SerializeField] private Camera _camera;
    [SerializeField] private LineRenderer _divider;
    [SerializeField] private LineRenderer _floor;

    private readonly string[] _states = { "Idle", "Attack1", "Idle", "Attack1", "Walk", "Attack1", "Walk", "Idle" };
    private readonly float[] _durations = { 1.2f, 1.42f, 1f, 1.42f, 2.6f, 1.42f, 2.6f, 1f };
    private float _elapsed;
    private float _stageElapsed;
    private float _walkDistance;
    private int _stage = -1;
    private int _direction = 1;
    private int _completedAttacks;
    private bool _finished;
    private GUIStyle _style;
    public Animator RigAnimator { get { return _animator; } }
    public int Direction { get { return _direction; } }
    public int CompletedAttacks { get { return _completedAttacks; } }
    public int Stage { get { return _stage; } }
    public bool Finished { get { return _finished; } }
    public float StageElapsed { get { return _stageElapsed; } }
    public float Elapsed { get { return _elapsed; } }
    public int ReferenceFrame { get; private set; }

    private void Awake() { Restart(); }

    public void Restart()
    {
        _elapsed = 0; _stageElapsed = 0; _walkDistance = 0;
        _stage = -1; _direction = 1; _completedAttacks = 0; _finished = false;
        _animator.applyRootMotion = false;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.Rebind(); _animator.Update(0);
    }

private void Update()
    {
        if (_finished) return;
        _elapsed += Time.deltaTime;
        float half = 0; foreach (float value in _durations) half += value;
        int pass = _elapsed < half ? 0 : 1;
        float cursor = _elapsed - pass * half;
        int localStage = 0;
        while (localStage < _durations.Length - 1 && cursor >= _durations[localStage]) { cursor -= _durations[localStage]; localStage++; }
        int stage = pass * _durations.Length + localStage;
        _stageElapsed = cursor;
        if (stage != _stage)
        {
            if (_stage >= 0 && _states[_stage % _states.Length] == "Attack1") _completedAttacks++;
            if (pass != (_stage < 0 ? 0 : _stage / _states.Length)) _walkDistance = 0;
            _stage = stage; _direction = pass == 0 ? 1 : -1;
            _animator.transform.localScale = new Vector3(_direction * 2.3f, 2.3f, 1);
            _reference.transform.localScale = new Vector3(_direction * 2.3f, 2.3f, 1);
            string state = _states[localStage];
            if (state == "Attack1" || _stage == 0 || localStage == 0) _animator.Play(state, 0, 0);
            else _animator.CrossFadeInFixedTime(state, .18f, 0, 0);
        }
        if (_states[localStage] == "Walk") _walkDistance += Time.deltaTime * 2f * _direction;
        // 이동 후 공격에서도 부모 위치를 유지하고 비교 카메라만 함께 이동합니다.
        float travel = _walkDistance;
        _camera.transform.position = new Vector3(travel, 2f, -10f);
        var divider = _divider;
        divider.SetPosition(0, new Vector3(travel, -.2f, .1f));
        divider.SetPosition(1, new Vector3(travel, 5.5f, .1f));
        var floor = _floor;
        floor.SetPosition(0, new Vector3(travel - 12f, 0, .1f));
        floor.SetPosition(1, new Vector3(travel + 12f, 0, .1f));
        _actor.position = new Vector3(-4.3f + travel, 0, 0);
        _referenceActor.position = new Vector3(4.3f + travel, -.552f, 0);
        if (_elapsed >= half * 2) { _finished = true; }
    }



    private void LateUpdate()
    {
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Attack1")) {
            ReferenceFrame = Mathf.Clamp(Mathf.FloorToInt(state.normalizedTime * _attackFrames.Length), 0, _attackFrames.Length - 1);
            _reference.sprite = _attackFrames[ReferenceFrame];
        } else if (state.IsName("Walk") && _walkFrames != null && _walkFrames.Length > 0) {
            ReferenceFrame = Mathf.FloorToInt(state.normalizedTime * _walkFrames.Length) % _walkFrames.Length;
            _reference.sprite = _walkFrames[ReferenceFrame];
        } else { ReferenceFrame = 0; _reference.sprite = _attackFrames[0]; }
    }

    private void OnGUI()
    {
        if (_style == null) {
            _style = new GUIStyle(GUI.skin.label); _style.fontSize = 21;
            _style.font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 21);
            _style.normal.textColor = Color.white;
        }
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        float attackTime = state.normalizedTime * 1.42f;
        string timing = state.IsName("Attack1") ? (attackTime < .70f ? "준비" : attackTime < .82f ? "타격" : "회복") : (state.IsName("Walk") ? "Walk" : "Idle");
        GUI.Label(new Rect(18, 12, Screen.width - 36, 92),
            "Attack1 리그 개선 비교 · 게임 미연결\n왼쪽: 2페이즈 공격 전용 리그 | 오른쪽: 1페이즈 원화 13장\n1.42초 · 타격 0.70~0.82초 | " + (_direction > 0 ? "오른쪽" : "왼쪽") + " · " + timing + " · 원화 " + ReferenceFrame.ToString("00") + (_finished ? " · 검사 재생 완료" : ""), _style);
        GUI.Label(new Rect(18, Screen.height - 62, Screen.width - 36, 54),
            "공격 동작·타이밍 유지 / 기존 픽셀의 관절 겹침 적용 / 가려진 팔 자세는 근사치입니다.", _style);
    }

    private void OnDisable()
    {
        if (_style == null) return;
        Font font = _style.font; _style.font = null; _style = null;
        if (font != null) Destroy(font);
    }
}
