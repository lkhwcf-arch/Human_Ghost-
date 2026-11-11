using System;
using HumanGhost.Data.Definitions;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 플레이어 결과를 표현 소리로 연결합니다. 동작 선택·판정·진행 상태는 변경하지 않습니다.
    public sealed class PlayerAnimationAudio : MonoBehaviour
    {
        [SerializeField, InspectorName("임시 자동 발소리(애니 이벤트 연결 시 끔)")] private bool _automaticFootsteps = true;
        [SerializeField, Min(0.05f)] private float _walkStepSeconds = 0.5f;
        private PlayerRuntime _player;
        private Action<string> _play;
        private PlayerActor _actor;
        private int _health;
        private float _lastX, _stepRemaining;

        public void Connect(PlayerRuntime player, Action<string> play)
        {
            if (player == null || player.Actor == null || play == null)
                throw new InvalidOperationException("플레이어 소리의 초기화된 런타임·재생기를 연결해 주세요.");
            Disconnect();
            _player = player; _play = play;
            if (isActiveAndEnabled) Bind();
        }

        private void Bind()
        {
            if (_actor != null) return;
            _actor = _player.Actor;
            _health = _actor.Health; _lastX = _player.transform.position.x; _stepRemaining = 0f;
            _actor.States.Changed += OnStateChanged;
            _actor.AttackStarted += OnAttack;
            _actor.HealthChanged += OnHealthChanged;
        }
        private void OnEnable() { if (_player != null && _play != null) Bind(); }
        private void OnDisable() => Unbind();
        private void Unbind()
        {
            if (_actor != null)
            {
                _actor.States.Changed -= OnStateChanged;
                _actor.AttackStarted -= OnAttack;
                _actor.HealthChanged -= OnHealthChanged;
            }
            _actor = null; _stepRemaining = 0f;
        }
        public void Disconnect() { Unbind(); _player = null; _play = null; }

        // 애니메이션 이벤트에서도 같은 키를 사용합니다. Run·새 변형 선택 정책은 애니 담당이 결정합니다.
        public void PlayAnimationSound(string key)
        {
            if (!isActiveAndEnabled || _play == null) return;
            try { _play(key); }
            catch (Exception error) { Debug.LogException(error, this); }
        }
        private void OnAttack(int index)
            => PlayAnimationSound(index == 4 ? "PLAYER_JUMP_ATTACK" : "PLAYER_ATTACK_" + (index + 1));
        private void OnHealthChanged(int health)
        {
            if (health < _health)
                PlayAnimationSound(health == 0 ? "PLAYER_DEAD" : _actor.CurrentState == StateKey.CritHit ? "PLAYER_CRIT_HIT" : "PLAYER_HIT");
            _health = health;
        }
        private void OnStateChanged(StateKey state)
        {
            switch (state)
            {
                case StateKey.Roll: PlayAnimationSound("PLAYER_ROLL"); break;
                case StateKey.Evade: PlayAnimationSound("PLAYER_BACK_EVADE"); break;
                case StateKey.Hang: PlayAnimationSound("PLAYER_HANG"); break;
                case StateKey.Heal: PlayAnimationSound("PLAYER_HEAL"); break;
                case StateKey.Execute: PlayAnimationSound("PLAYER_EXECUTE_1"); break;
            }
        }
        private void Update()
        {
            if (_actor == null || _player == null) return;
            float x = _player.transform.position.x;
            bool moving = Mathf.Abs(x - _lastX) > 0.0001f;
            _lastX = x;
            if (!_automaticFootsteps || Time.deltaTime <= 0f || !_player.isActiveAndEnabled ||
                _actor.CurrentState != StateKey.Walk || !_actor.Locomotion.IsGrounded || !moving)
            {
                _stepRemaining = 0f;
                return;
            }
            _stepRemaining -= Time.deltaTime;
            if (_stepRemaining > 0f) return;
            PlayAnimationSound("PLAYER_WALK");
            _stepRemaining = Mathf.Max(0.05f, _walkStepSeconds);
        }
    }
}
