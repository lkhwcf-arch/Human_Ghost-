using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 확인 장면 전용: 쇄문아비 상태별 프레임을 재생합니다. 공격은 패턴 시간에 맞춰 늘이고 나머지는 반복합니다. 판정은 기존 사각형이 맡습니다.
    [DefaultExecutionOrder(-10)]
    public sealed class PreviewBossSprite : MonoBehaviour
    {
        [Serializable]
        private sealed class StateClip
        {
            [SerializeField] private StateKey _state = StateKey.Idle;
            [SerializeField] private Sprite[] _frames = Array.Empty<Sprite>();
            public StateKey State => _state;
            public Sprite[] Frames => _frames;
        }

        [SerializeField, InspectorName("보스 런타임")] private BossRuntime _boss = null;
        [SerializeField, InspectorName("표시 렌더러")] private SpriteRenderer _renderer = null;
        [SerializeField, InspectorName("판정 사각형 (색 따라감)")] private SpriteRenderer _body = null;
        [SerializeField, InspectorName("상태별 프레임")] private StateClip[] _clips = Array.Empty<StateClip>();
        [SerializeField, InspectorName("반복 재생 초당 프레임"), Min(1f)] private float _framesPerSecond = 10f;
        [SerializeField, InspectorName("몸통 기준 가로 보정 (타일)")] private float _bodyOffsetX = 0.87f;
        private StateKey _current;
        private float _elapsed;
        private bool _started;

        private void LateUpdate()
        {
            BossPoise actor = _boss != null ? _boss.Actor : null;
            if (actor == null || _renderer == null) return;
            StateKey state = actor.CurrentState;
            if (!_started || state != _current) { _current = state; _elapsed = 0f; _started = true; }
            _elapsed += Time.deltaTime;
            Sprite[] frames = Find(state);
            if (frames.Length > 0) _renderer.sprite = frames[GetFrame(frames.Length, state)];
            if (_body != null) _renderer.color = ToTint(_body.color, _boss.Combat != null && _boss.Combat.IsAttacking, actor);
            int facing = _boss.Combat != null ? _boss.Combat.Facing : 1;
            _renderer.flipX = facing < 0;
            Vector3 position = _renderer.transform.localPosition;
            position.x = facing * _bodyOffsetX;
            _renderer.transform.localPosition = position;
        }

        private int GetFrame(int count, StateKey state)
        {
            bool attacking = _boss.Combat != null && _boss.Combat.IsAttacking;
            if (attacking)
            {
                int total = 0;
                foreach (AttackSpec hit in _boss.Combat.CurrentPattern.Hits) total += hit.TotalTime;
                return Mathf.Clamp(Mathf.FloorToInt(_elapsed * 1000f / total * count), 0, count - 1);
            }
            if (state == StateKey.Dead) return count - 1;
            return Mathf.FloorToInt(_elapsed * _framesPerSecond) % count;
        }

        // 예비 동작은 붉게(C09), 노출·사망은 기존 임시 색을 씁니다. 평소에는 원본 색입니다.
        private static Color ToTint(Color body, bool attacking, BossPoise actor)
            => attacking || actor.IsExposed || actor.IsInvulnerable || actor.IsDead ? body : Color.white;

        private Sprite[] Find(StateKey state)
        {
            StateClip fallback = null;
            foreach (StateClip clip in _clips)
            {
                if (clip.State == state) return clip.Frames;
                if (clip.State == StateKey.Idle) fallback = clip;
            }
            return fallback != null ? fallback.Frames : Array.Empty<Sprite>();
        }
    }
}
