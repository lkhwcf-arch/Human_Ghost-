
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

// 별도 Walk 시험 씬 전용. 뼈 포즈는 Animator의 .anim만 재생합니다.
[DefaultExecutionOrder(10000)]
public sealed class SwaemunabiWalkPreview : MonoBehaviour
{
    [SerializeField] private Transform _actor;
    [SerializeField] private Animator _animator;
    [SerializeField] private float _speed = 2f;
    [SerializeField] private float _visualScale = 2.3f;
    [SerializeField] private float _walkDuration = 4.55f;
    private float _elapsed;
    private int _stage = -1;
    private bool _walking;
    private int _direction = 1;
    private Transform[] _hands;
    private Vector3[] _previousHands = new Vector3[2];
    private int[] _previousContacts = new int[] { -999, -999 };
    private Vector3[] _stanceStart = new Vector3[2];
    private float _maximumStepSlip;
    private float _maximumStanceSlip;
    private int _contactChecks;
    private float _rightCycles;
    private float _leftCycles;
    private int _idleReturns;
    private bool _wasWalking;
    private bool _reported;
    private GUIStyle _labelStyle;
    private string _status = "Idle";
    public float MaximumStepSlipPixels { get { return _maximumStepSlip * 100f / _visualScale; } }
    public float MaximumStanceSlipPixels { get { return _maximumStanceSlip * 100f / _visualScale; } }
    public int ContactChecks { get { return _contactChecks; } }
    public float RightCycles { get { return _rightCycles; } }
    public float LeftCycles { get { return _leftCycles; } }
    public int IdleReturns { get { return _idleReturns; } }
    public bool Finished { get { return _reported; } }
    public string Status { get { return _status; } }

    private void Awake()
    {
        _hands = new Transform[] {
            _animator.transform.Find("Root/Body/UpperArm_L/Forearm_L/Hand_L"),
            _animator.transform.Find("Root/Body/UpperArm_R/Forearm_R/Hand_R")
        };
        _animator.applyRootMotion = false;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        StartCoroutine(MeasureAfterRender());
    }

    private void Update()
    {
        if (_reported) return;
        _elapsed += Time.deltaTime;
        float idle = 2.4f;
        int stage;
        float x;
        float extent = _speed * _walkDuration * .5f;
        if (_elapsed < idle) { stage = 0; x = -extent; }
        else if (_elapsed < idle + _walkDuration) { stage = 1; x = -extent + (_elapsed - idle) * _speed; }
        else if (_elapsed < idle * 2 + _walkDuration) { stage = 2; x = extent; }
        else if (_elapsed < idle * 2 + _walkDuration * 2) { stage = 3; x = extent - (_elapsed - idle * 2 - _walkDuration) * _speed; }
        else { stage = 4; x = -extent; }
        _walking = stage == 1 || stage == 3;
        if (stage != _stage)
        {
            _stage = stage;
            if (stage == 3) _direction = -1;
            _animator.transform.localScale = new Vector3(_direction * _visualScale, _visualScale, 1f);
            _animator.SetBool("Walking", _walking);
            _status = _walking ? (_direction > 0 ? "오른쪽 Walk" : "왼쪽 Walk") : "Idle";
            _previousContacts[0] = _previousContacts[1] = -999;
        }
        _actor.position = new Vector3(x, 0f, 0f);
        if (_elapsed >= idle * 3 + _walkDuration * 2)
        {
            _reported = true;
            _status = "양방향 재생 완료 — 시각 품질 확인 필요";
        }
    }

    private IEnumerator MeasureAfterRender()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (_animator == null) yield break;
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            bool walk = state.IsName("Walk") && !_animator.IsInTransition(0);
            if (walk)
            {
                if (_direction > 0) _rightCycles = Mathf.Max(_rightCycles, state.normalizedTime);
                else _leftCycles = Mathf.Max(_leftCycles, state.normalizedTime);
            }
            if (_wasWalking && state.IsName("Idle") && !_animator.IsInTransition(0)) { _idleReturns++; _wasWalking = false; }
            if (walk) _wasWalking = true;
            for (int hand = 0; hand < 2; hand++)
            {
                float time = state.normalizedTime + (hand == 0 ? 0f : .5f);
                float phase = Mathf.Repeat(time, 1f);
                bool planted = walk && _walking && phase > .025f && phase < .575f;
                int contact = Mathf.FloorToInt(time) + (_direction > 0 ? 0 : 100);
                Vector3 position = _hands[hand].position;
                if (planted && _previousContacts[hand] == contact)
                {
                    _maximumStepSlip = Mathf.Max(_maximumStepSlip, Vector3.Distance(position, _previousHands[hand]));
                    _maximumStanceSlip = Mathf.Max(_maximumStanceSlip, Vector3.Distance(position, _stanceStart[hand]));
                    _contactChecks++;
                }
                else if (planted) _stanceStart[hand] = position;
                _previousContacts[hand] = planted ? contact : -999;
                _previousHands[hand] = position;
            }
        }
    }

    private void OnGUI()
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 22);
            _labelStyle.fontSize = 22;
            _labelStyle.normal.textColor = Color.white;
        }
        GUI.Label(new Rect(18, 12, 1400, 90), "쇄문아비 2페이즈 Walk 테스트 | " + _status + "\n1.3초 / 이동 2유닛·초 / 배율 2.3 / 8개 뼈 공유", _labelStyle);
    }


private void OnDisable()
    {
        if (_labelStyle == null) return;
        Font font = _labelStyle.font;
        _labelStyle.font = null;
        _labelStyle = null;
        if (font != null) Destroy(font);
    }
}
