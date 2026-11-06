using UnityEngine;

// 기존 전투 코드를 호출하지 않는 Attack2 비교 전용 재생기.
// 뼈 포즈는 편집 가능한 .anim에서 재생하고, 이 코드는 비교와 전환만 담당합니다.
[DefaultExecutionOrder(10000)]
public sealed class SwaemunabiAttack2Preview : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _reference;
    [SerializeField] private Transform _actor;
    [SerializeField] private Transform _referenceActor;
    [SerializeField] private Sprite[] _attackFrames;
    [SerializeField] private Sprite[] _attack1Frames;
    [SerializeField] private Sprite[] _walkFrames;
    [SerializeField] private Camera _camera;
    [SerializeField] private LineRenderer _divider;
    [SerializeField] private LineRenderer _floor;

    private readonly string[] _states = { "Idle", "Attack2", "Attack2", "Idle", "Walk", "Attack2", "Attack1", "Attack2", "Walk", "Idle" };
    private readonly float[] _durations = { 1.2f, 1.55f, 1.55f, .8f, 2.6f, 1.55f, 1.42f, 1.55f, 2.6f, 1f };
    private float _elapsed;
    private float _stageElapsed;
    private float _walkDistance;
    private int _stage = -1;
    private int _direction = 1;
    private int _completedAttacks;
    private int _completedAttack1;
    private bool _finished;
    private GUIStyle _style;
    public Animator RigAnimator { get { return _animator; } }
    public int Direction { get { return _direction; } }
    public int CompletedAttacks { get { return _completedAttacks; } }
    public int CompletedAttack1 { get { return _completedAttack1; } }
    public int Stage { get { return _stage; } }
    public bool Finished { get { return _finished; } }
    public float StageElapsed { get { return _stageElapsed; } }
    public float Elapsed { get { return _elapsed; } }
    public int ReferenceFrame { get; private set; }

    private void Awake() { Restart(); }

    public void Restart()
    {
        _elapsed = 0; _stageElapsed = 0; _walkDistance = 0;
        _stage = -1; _direction = 1; _completedAttacks = 0; _completedAttack1 = 0; _finished = false;
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
            if (_stage >= 0 && _states[_stage % _states.Length] == "Attack2") _completedAttacks++;
            if (_stage >= 0 && _states[_stage % _states.Length] == "Attack1") _completedAttack1++;
            if (pass != (_stage < 0 ? 0 : _stage / _states.Length)) _walkDistance = 0;
            _stage = stage; _direction = pass == 0 ? 1 : -1;
            _animator.transform.localScale = new Vector3(_direction * 2.3f, 2.3f, 1);
            _reference.transform.localScale = new Vector3(_direction * 2.3f, 2.3f, 1);
            string state = _states[localStage];
            if (state == "Attack1" || state == "Attack2" || _stage == 0 || localStage == 0) _animator.Play(state, 0, 0);
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
        if (state.IsName("Attack2")) {
            ReferenceFrame = Mathf.Clamp(Mathf.FloorToInt(state.normalizedTime * _attackFrames.Length), 0, _attackFrames.Length - 1);
            _reference.sprite = _attackFrames[ReferenceFrame];
        } else if (state.IsName("Attack1")) {
            ReferenceFrame = Mathf.Clamp(Mathf.FloorToInt(state.normalizedTime * _attack1Frames.Length), 0, _attack1Frames.Length - 1);
            _reference.sprite = _attack1Frames[ReferenceFrame];
        } else if (state.IsName("Walk") && _walkFrames != null && _walkFrames.Length > 0) {
            ReferenceFrame = Mathf.FloorToInt(state.normalizedTime * _walkFrames.Length) % _walkFrames.Length;
            _reference.sprite = _walkFrames[ReferenceFrame];
        } else { ReferenceFrame = 0; _reference.sprite = _attackFrames[0]; }
    }

    private void OnGUI()
    {
        if (_style == null) {
            _style = new GUIStyle(GUI.skin.label); _style.fontSize = 20;
            _style.font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 20);
            _style.normal.textColor = Color.white;
        }
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        float t = state.normalizedTime * 1.55f;
        string timing = state.IsName("Attack2") ? (t >= .5f && t < .6f ? "1차 판정" : t >= .85f && t < .95f ? "2차 판정" : "Attack2") : (state.IsName("Attack1") ? "Attack1 전환" : state.IsName("Walk") ? "Walk" : "Idle");
        string frame = state.IsName("Attack2") ? (ReferenceFrame < 13 ? "attack2_" + ReferenceFrame.ToString("00") : "attack5_" + (ReferenceFrame - 13).ToString("00")) : ReferenceFrame.ToString("00");
        GUI.Label(new Rect(18, 12, Screen.width - 36, 100),
            "Attack2 기존 자료 대응 · 게임 미연결\n왼쪽: 2페이즈 리그 | 오른쪽: 기존 attack2 → attack5, 26장\n1.55초 / 판정 0.50~0.60 · 0.85~0.95초 | " + (_direction > 0 ? "오른쪽" : "왼쪽") + " · " + timing + " · " + frame + (_finished ? " · 재생 완료" : ""), _style);
        GUI.Label(new Rect(18, Screen.height - 66, Screen.width - 36, 60),
            "현재 게임의 26장 순서를 유지합니다. 2차 판정과 큰 휘두르기의 시점 차이, 가려진 면과 잔상 차이는 남습니다.", _style);
    }

    private void OnDisable()
    {
        if (_style == null) return;
        Font font = _style.font; _style.font = null; _style = null;
        if (font != null) Destroy(font);
    }
}
