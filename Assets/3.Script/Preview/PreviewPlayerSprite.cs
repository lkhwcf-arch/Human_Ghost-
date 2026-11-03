using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 확인 장면 전용: 플레이어 상태마다 스프라이트 한 프레임을 보여줍니다. 애니메이션·판정은 소유하지 않습니다.
    public sealed class PreviewPlayerSprite : MonoBehaviour
    {
        [Serializable]
        private sealed class StateFrame
        {
            [SerializeField] private StateKey _state = StateKey.Idle;
            [SerializeField] private Sprite _sprite = null;
            public StateKey State => _state;
            public Sprite Sprite => _sprite;
        }

        [SerializeField, InspectorName("플레이어 런타임")] private PlayerRuntime _player = null;
        [SerializeField, InspectorName("표시 렌더러")] private SpriteRenderer _renderer = null;
        [SerializeField, InspectorName("기본 프레임")] private Sprite _fallback = null;
        [SerializeField, InspectorName("상태별 프레임")] private StateFrame[] _frames = Array.Empty<StateFrame>();
        [SerializeField, InspectorName("원본이 왼쪽을 봄")] private bool _sourceFacesLeft = true;
        private int _facing = 1;

        private void LateUpdate()
        {
            PlayerActor actor = _player != null ? _player.Actor : null;
            if (actor == null || _renderer == null) return;
            if (actor.Locomotion.Direction != 0) _facing = actor.Locomotion.Direction;
            Sprite sprite = GetFrame(actor.CurrentState);
            // 4타 원본 전까지 검사용 이전 마무리 프레임을 재사용합니다. 명시한 4타 프레임이 우선입니다.
            if (sprite == null && actor.CurrentState == StateKey.Attack4) sprite = GetFrame(StateKey.Attack3);
            if (sprite == null) sprite = _fallback;
            if (_renderer.sprite != sprite) _renderer.sprite = sprite;
            _renderer.flipX = _sourceFacesLeft ? _facing > 0 : _facing < 0;
        }

        private Sprite GetFrame(StateKey state)
        {
            foreach (StateFrame frame in _frames)
                if (frame != null && frame.State == state && frame.Sprite != null) return frame.Sprite;
            return null;
        }
    }
}
