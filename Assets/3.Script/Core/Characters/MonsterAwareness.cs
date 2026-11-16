using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 좌표·대상 관측과 정수 ms만 받습니다. 실제 이동·HP·공격 상태는 소유하지 않습니다.
    public sealed class MonsterAwareness
    {
        private readonly MonsterAggro _definition;
        // 원본 규칙의 바로 위·아래 띠: 가로 반 칸(tile_x100)입니다.
        private const int _verticalStripHalfWidth = 50;
        private int _distance, _height, _spawnDistance, _outside, _reacquire;
        private int _horizontalOffset, _heightOffset, _facing;
        private bool _playerAlive, _targetEligible;
        public bool IsEngaged { get; private set; }
        public bool IsReturning { get; private set; }

        public MonsterAwareness(MonsterAggro definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (definition.LoseRange < 0 || definition.LoseHeight < 0 || definition.LoseTime < 0 ||
                definition.SameLevelHeight < 0 || definition.SameLevelHeight > definition.LoseHeight ||
                definition.LeashRange <= 0 || definition.ReacquireDelay < 0)
                throw new ArgumentException("몬스터 인식·해제·복귀 수치를 확인하세요.", nameof(definition));
            foreach (int range in new[] { definition.FrontRange, definition.BackRange, definition.FrontUpRange,
                definition.BackUpRange, definition.FrontDownRange, definition.BackDownRange })
                if (range < 0 || range > definition.LoseRange)
                    throw new ArgumentException("가로 인식 거리는 0 이상 해제 거리 이하여야 합니다.", nameof(definition));
            foreach (int range in new[] { definition.UpRange, definition.DownRange, definition.FrontUpRange,
                definition.BackUpRange, definition.FrontDownRange, definition.BackDownRange })
                if (range < 0 || range > definition.LoseHeight)
                    throw new ArgumentException("세로 인식 거리는 0 이상 해제 높이 이하여야 합니다.", nameof(definition));
        }
        // 위치 차이는 부호를 보존합니다. 몬스터의 현재 방향을 기준으로 앞·뒤를 고릅니다.
        public void Observe(int horizontalOffset, int heightOffset, int spawnDistance, bool targetEligible = true, int facing = 1)
        {
            if (horizontalOffset == int.MinValue || heightOffset == int.MinValue || spawnDistance < 0)
                throw new ArgumentOutOfRangeException(nameof(horizontalOffset));
            if (facing != -1 && facing != 1) throw new ArgumentOutOfRangeException(nameof(facing));
            _playerAlive = true; _targetEligible = targetEligible;
            _horizontalOffset = horizontalOffset; _heightOffset = heightOffset; _facing = facing;
            _distance = Math.Abs(horizontalOffset); _height = Math.Abs(heightOffset); _spawnDistance = spawnDistance;
            if (IsReturning && spawnDistance == 0) IsReturning = false;
            // 대상 수명이 끝나면 수치와 무관하게 해제합니다. 화면·발판 여부는 인식 조건으로 쓰지 않습니다.
            if (!_targetEligible) { if (IsEngaged) StartReturn(); return; }
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
        public void NotifyHit() { if (_playerAlive && _targetEligible) Engage(); }
        public bool LoseTarget()
        {
            if (!_playerAlive) return false;
            _playerAlive = false; StartReturn(); return true;
        }
        public void Reset()
        {
            _playerAlive = _targetEligible = IsEngaged = IsReturning = false;
            _outside = _reacquire = _distance = _height = _spawnDistance = 0;
        }
        private void TryAcquire()
        {
            if (!IsEngaged && _playerAlive && _targetEligible && _reacquire == 0 && WithinDirectionalRange()) Engage();
        }
        private bool WithinDirectionalRange()
        {
            bool front = (long)_horizontalOffset * _facing >= 0;
            if (_height <= _definition.SameLevelHeight)
            {
                int range = front ? _definition.FrontRange : _definition.BackRange;
                return range > 0 && _distance <= range;
            }
            bool above = _heightOffset > 0;
            if (_distance <= _verticalStripHalfWidth)
            {
                int range = above ? _definition.UpRange : _definition.DownRange;
                return range > 0 && _height <= range;
            }
            int diagonal = above
                ? (front ? _definition.FrontUpRange : _definition.BackUpRange)
                : (front ? _definition.FrontDownRange : _definition.BackDownRange);
            return diagonal > 0 && _distance <= diagonal && _height <= diagonal;
        }
        private void Engage() { IsEngaged = true; IsReturning = false; _outside = 0; _reacquire = 0; }
        private void StartReturn()
        {
            IsEngaged = false; IsReturning = _spawnDistance > 0;
            _outside = 0; _reacquire = _definition.ReacquireDelay;
        }
    }
}
