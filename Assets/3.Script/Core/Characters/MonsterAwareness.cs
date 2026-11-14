using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 좌표·대상 관측과 정수 ms만 받습니다. 실제 이동·HP·공격 상태는 소유하지 않습니다.
    public sealed class MonsterAwareness
    {
        private readonly MonsterAggro _definition;
        private readonly int _sightRange;
        private int _distance, _height, _spawnDistance, _outside, _reacquire;
        private bool _playerAlive;
        public bool IsEngaged { get; private set; }
        public bool IsReturning { get; private set; }

        public MonsterAwareness(int sightRange, MonsterAggro definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (sightRange < 0 || definition.SightHeight < 0 || definition.LoseRange < sightRange ||
                definition.LoseHeight < definition.SightHeight || definition.LoseTime < 0 ||
                definition.LeashRange <= 0 || definition.ReacquireDelay < 0)
                throw new ArgumentException("몬스터 인식·해제·복귀 수치를 확인하세요.", nameof(definition));
            _sightRange = sightRange;
        }
        public void Observe(int distance, int height, int spawnDistance)
        {
            if (distance < 0 || height < 0 || spawnDistance < 0) throw new ArgumentOutOfRangeException(nameof(distance));
            _playerAlive = true;
            _distance = distance; _height = height; _spawnDistance = spawnDistance;
            if (IsReturning && spawnDistance == 0) IsReturning = false;
            TryAcquire();
        }
        // true는 이번 호출에서 복귀를 새로 시작했다는 뜻입니다.
        public bool Advance(int milliseconds, bool protectedAction)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            _reacquire = Math.Max(0, _reacquire - milliseconds);
            if (IsEngaged && _playerAlive)
            {
                bool outside = _distance > _definition.LoseRange || _height > _definition.LoseHeight;
                _outside = outside ? (int)Math.Min(int.MaxValue, (long)_outside + milliseconds) : 0;
                if (!protectedAction && (_spawnDistance > _definition.LeashRange || outside && _outside >= _definition.LoseTime))
                { StartReturn(); return true; }
            }
            TryAcquire();
            return false;
        }
        public void NotifyHit() { if (_playerAlive) Engage(); }
        public bool LoseTarget()
        {
            if (!_playerAlive) return false;
            _playerAlive = false; StartReturn(); return true;
        }
        public void Reset()
        {
            _playerAlive = IsEngaged = IsReturning = false;
            _outside = _reacquire = _distance = _height = _spawnDistance = 0;
        }
        private void TryAcquire()
        {
            if (!IsEngaged && _playerAlive && _reacquire == 0 && _distance <= _sightRange && _height <= _definition.SightHeight) Engage();
        }
        private void Engage() { IsEngaged = true; IsReturning = false; _outside = 0; _reacquire = 0; }
        private void StartReturn()
        {
            IsEngaged = false; IsReturning = _spawnDistance > 0;
            _outside = 0; _reacquire = _definition.ReacquireDelay;
        }
    }
}
