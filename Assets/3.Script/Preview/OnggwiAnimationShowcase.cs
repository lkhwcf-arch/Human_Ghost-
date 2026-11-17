using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 애니메이션 확인 씬 전용. 운영 리그·클립을 그대로 샘플하며 AI/피해/저장에는 연결하지 않습니다.
    public sealed class OnggwiAnimationShowcase : MonoBehaviour
    {
        [SerializeField] private AnimationClip _throw = null;
        [SerializeField] private GameObject _heldStone = null;
        [SerializeField] private bool _faceRight = false;
        [SerializeField] private bool _play = true;
        [SerializeField, Range(0,1700)] private int _sampleMilliseconds = 0;
        private MillisecondClock _clock;
        private RigBindPose _bind;
        private int _elapsed;
        private Vector3 _scale = Vector3.one;
        private void Awake()
        {
            if(_throw==null || _heldStone==null)throw new InvalidOperationException("옹귀 확인용 투척 클립·돌을 연결해 주세요.");
            _scale=transform.localScale;_bind=new RigBindPose(gameObject);
            var animator=GetComponent<Animator>();if(animator!=null)animator.enabled=false;
        }
        private void Update()
        {
            if(_play) _elapsed=(int)(((long)_elapsed+_clock.AdvanceFrame(Time.deltaTime))%2200);
            else _elapsed=_sampleMilliseconds;
            _bind.Restore();
            // 다른 옹귀 스프라이트 애니와 같은 프레임 간격(클립 frameRate, 10fps)으로 끊어 샘플합니다.
            float time=Mathf.Min(Mathf.Floor(_elapsed/1000f*_throw.frameRate)/_throw.frameRate,_throw.length);
            _throw.SampleAnimation(gameObject,time);
            transform.localScale=new Vector3(_scale.x*(_faceRight?-1:1),_scale.y,_scale.z);
            _heldStone.SetActive(_elapsed<500 || _elapsed>=1700);
        }
        private void OnDisable() { _bind?.Restore();transform.localScale=_scale;_clock=default;_elapsed=0; }
    }
}
