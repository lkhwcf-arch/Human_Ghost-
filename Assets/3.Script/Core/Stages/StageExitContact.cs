using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 출구 접촉만 보관하고 이동 명령과 표시는 호출자가 담당합니다.
    public class StageExitContact
    {
        private readonly StageExit _definition;
        private bool _observed, _inside;
        private readonly long _rangeX, _rangeY;
        public StageExitContact(StageExit definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _rangeX = IntegerPosition.FromData(definition.RangeX);
            _rangeY = IntegerPosition.FromData(definition.RangeY);
        }
        public bool Observe(long x, long y, long minimumX, long maximumX, long anchorX, long anchorY)
        {
            bool inside;
            switch (_definition.TriggerType)
            {
                case "LeftEdge": inside = x <= minimumX + _rangeX; break;
                case "RightEdge": inside = x >= maximumX - _rangeX; break;
                case "Area": inside = Math.Abs(x - anchorX) <= _rangeX && Math.Abs(y - anchorY) <= _rangeY; break;
                default: throw new InvalidOperationException("자동 진입 출구의 실행 방식을 확인해 주세요: " + _definition.TriggerType);
            }
            // 도착 위치가 출구와 겹쳐도 나갔다 다시 들어와야 이동합니다.
            bool entered = _observed && inside && !_inside;
            _observed = true; _inside = inside;
            return entered;
        }
    }
}
