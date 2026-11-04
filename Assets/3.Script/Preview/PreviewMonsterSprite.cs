using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 확인 장면 전용: 일반몹 상태마다 스프라이트 한 프레임을 보여줍니다. 애니메이션·판정은 소유하지 않습니다.
    [DefaultExecutionOrder(-10)]
    public sealed class PreviewMonsterSprite : MonoBehaviour
    {
        [Serializable]
        private sealed class StateFrame
        {
            [SerializeField] private StateKey _state = StateKey.Idle;
            [SerializeField] private Sprite _sprite = null;
            public StateKey State => _state;
            public Sprite Sprite => _sprite;
        }

        [SerializeField, InspectorName("일반몹 런타임")] private MonsterRuntime _monster = null;
        [SerializeField, InspectorName("표시 렌더러")] private SpriteRenderer _renderer = null;
        [SerializeField, InspectorName("기본 프레임")] private Sprite _fallback = null;
        [SerializeField, InspectorName("상태별 프레임")] private StateFrame[] _frames = Array.Empty<StateFrame>();

        private void LateUpdate()
        {
            MonsterActor actor = _monster != null ? _monster.Actor : null;
            if (actor == null || _renderer == null) return;
            Sprite sprite = _fallback;
            foreach (StateFrame frame in _frames)
                if (frame != null && frame.State == actor.CurrentState && frame.Sprite != null) { sprite = frame.Sprite; break; }
            if (_renderer.sprite != sprite) _renderer.sprite = sprite;
        }
    }
}
