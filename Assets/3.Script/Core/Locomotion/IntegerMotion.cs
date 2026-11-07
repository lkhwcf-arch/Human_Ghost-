using System;

namespace HumanGhost.Gameplay
{
    // 위치와 시간의 나머지를 보존합니다. 충돌·프레임 시간·Unity 좌표 변환은 어댑터의 책임입니다.
    public class IntegerMotion
    {
        private long _horizontalRemainder;
        private long _verticalRemainder;
        private long _verticalSpeed;
        private long _gravity;
        private long _jumpSpeed;
        private long _rollDistance;
        private int _rollDuration, _rollElapsed;
        public bool IsRolling => _rollElapsed < _rollDuration;
        public void ConfigureJump(long height, int milliseconds)
        {
            if (height <= 0 || milliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            _jumpSpeed = checked(4 * height * 1000000 / milliseconds);
            _gravity = checked(8 * height * 1000000 / ((long)milliseconds * milliseconds));
        }
        public void StartJump() { _verticalSpeed = _jumpSpeed; _verticalRemainder = 0; }
        public void SetVerticalSpeed(long unitsPerSecond = 0) { _verticalSpeed = checked(unitsPerSecond * 1000); _verticalRemainder = 0; }
        public void ResetHorizontal() => _horizontalRemainder = 0;
        public long StepHorizontal(int dataSpeed, int milliseconds)
        {
            RequireTime(milliseconds);
            long total = checked((long)dataSpeed * IntegerPosition.UnitsPerTile * milliseconds + _horizontalRemainder);
            _horizontalRemainder = total % 100000;
            return total / 100000;
        }
        public void StartRoll(int direction, int dataDistance, int milliseconds)
        {
            if (milliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            _rollDistance = checked(direction * IntegerPosition.FromData(dataDistance));
            _rollDuration = milliseconds; _rollElapsed = 0;
            ResetHorizontal();
        }
        public void StopRoll() { _rollElapsed = _rollDuration; }
        public long StepRoll(int milliseconds)
        {
            RequireTime(milliseconds);
            if (!IsRolling) return 0;
            long previous = checked(_rollDistance * _rollElapsed / _rollDuration);
            _rollElapsed += Math.Min(milliseconds, _rollDuration - _rollElapsed);
            return checked(_rollDistance * _rollElapsed / _rollDuration - previous);
        }
        public long StepVertical(int milliseconds)
        {
            RequireTime(milliseconds);
            long distance = 0;
            // 정수 1ms 적분으로 프레임 분할에 따른 차이를 없앱니다. 물리 조회는 여기서 하지 않습니다.
            for (int i = 0; i < milliseconds; i++)
            {
                long next = Math.Max(checked(_verticalSpeed - _gravity), -30 * IntegerPosition.UnitsPerTile * 1000);
                long total = checked(_verticalSpeed + next + _verticalRemainder);
                distance = checked(distance + total / 2000000);
                _verticalRemainder = total % 2000000;
                _verticalSpeed = next;
            }
            return distance;
        }
        private static void RequireTime(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }
    }
}
