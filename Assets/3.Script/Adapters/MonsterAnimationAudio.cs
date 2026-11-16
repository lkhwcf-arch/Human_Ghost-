using System;
using HumanGhost.Data.Definitions;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 몬스터 행동 결과와 실제 이동만 관찰합니다. 전투·상태·이동 규칙은 변경하지 않습니다.
    public sealed class MonsterAnimationAudio : MonoBehaviour
    {
        [SerializeField] private string _soundPrefix = "";
        [SerializeField] private bool _separateCriticalHit = false;
        [SerializeField, Min(50)] private int _walkStepMilliseconds = 500;
        private MonsterRuntime _monster;
        private MonsterActor _actor;
        private SubscriptionScope _scope;
        private Action<string> _play;
        private int _health;
        private float _lastX;
        private int _stepRemainingMilliseconds;
        private MillisecondClock _stepClock;

        public void Connect(MonsterRuntime monster, Action<string> play)
        {
            if (monster == null || monster.Actor == null || play == null || string.IsNullOrWhiteSpace(_soundPrefix))
                throw new InvalidOperationException("몬스터 소리의 초기화된 런타임·재생기·소리 접두어를 연결해 주세요.");
            Disconnect();
            _monster = monster; _play = play;
            if (isActiveAndEnabled) Bind();
        }

        private void Bind()
        {
            if (_actor != null) return;
            _actor = _monster.Actor;
            _health = _actor.Health; _lastX = _monster.transform.position.x; _stepRemainingMilliseconds = 0; _stepClock = default;
            var actor = _actor;
            var scope = _scope = new SubscriptionScope();
            Action<MonsterPattern> pattern = scope.Guard<MonsterPattern>(OnPatternStarted);
            Action<int> health = scope.Guard<int>(OnHealthChanged);
            scope.Add(() => actor.PatternStarted -= pattern); actor.PatternStarted += pattern;
            scope.Add(() => actor.HealthChanged -= health); actor.HealthChanged += health;
        }

        private void OnEnable() { if (_monster != null && _play != null) Bind(); }
        private void OnDisable() => Unbind();
        private void Unbind()
        {
            var scope = _scope; _scope = null;
            scope?.Dispose();
            _actor = null; _stepRemainingMilliseconds = 0; _stepClock = default;
        }
        public void Disconnect() { Unbind(); _monster = null; _play = null; }

        private void Play(string suffix)
        {
            if (!isActiveAndEnabled || _play == null) return;
            try { _play(_soundPrefix + "_" + suffix); }
            catch (Exception error) { Debug.LogException(error, this); }
        }
        private void OnPatternStarted(MonsterPattern pattern)
        {
            if (pattern.Key == StateKey.Attack1) Play("ATTACK_1");
            else if (pattern.Key == StateKey.Attack2) Play("ATTACK_2");
        }
        private void OnHealthChanged(int health)
        {
            bool damaged = (health < _health || _actor.KeepsHealth) && health > 0;
            _health = health;
            if (damaged) Play(_separateCriticalHit && _actor.CurrentState == StateKey.CritHit ? "CRIT_HIT" : "HIT");
        }

        private void LateUpdate() => Step(Time.deltaTime);
        private void Step(float seconds)
        {
            if (_actor == null || _monster == null) return;
            float x = _monster.transform.position.x;
            bool moving = Mathf.Abs(x - _lastX) > 0.0001f;
            _lastX = x;
            if (seconds <= 0f || !_monster.isActiveAndEnabled || _actor.CurrentState != StateKey.Walk || !moving)
            {
                _stepRemainingMilliseconds = 0; _stepClock = default;
                return;
            }
            _stepRemainingMilliseconds -= _stepClock.AdvanceFrame(seconds);
            if (_stepRemainingMilliseconds > 0) return;
            Play("WALK");
            _stepRemainingMilliseconds = System.Math.Max(50, _walkStepMilliseconds);
        }
    }
}
