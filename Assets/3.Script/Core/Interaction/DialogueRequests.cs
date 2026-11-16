using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // 대화 요청 하나의 종료 결과입니다. 사용 가능 통지(AvailabilityChanged)와 구분합니다.
    public enum DialogueEndReason { Completed, Cancelled, Failed }

    // 공용 대화 실행기의 요청 번호·소유자와 결과 보관을 담당합니다.
    // 결과를 요청한 소유자만 다음 실행 순서에서 한 번 가져갑니다. 이벤트 콜백 안에서 진행 명령을 호출하지 않게 하기 위한 경계입니다.
    public sealed class DialogueRequests
    {
        private readonly struct Ended
        {
            public object Owner { get; }
            public int RequestId { get; }
            public DialogueEndReason Reason { get; }
            public Ended(object owner, int requestId, DialogueEndReason reason) { Owner = owner; RequestId = requestId; Reason = reason; }
        }

        private readonly List<Ended> _ended = new List<Ended>();
        private int _lastRequestId;
        private int _currentRequestId;
        private object _currentOwner;
        private bool _currentTracked;

        public bool IsRunning => _currentRequestId != 0;
        public bool IsCurrent(object owner) => IsRunning && ReferenceEquals(_currentOwner, owner);

        // 결과가 필요 없는 요청(NPC 말 걸기·보스 대사)은 tracked=false로 시작해 결과를 쌓지 않습니다.
        public int Begin(object owner, bool tracked)
        {
            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            if (IsRunning) throw new System.InvalidOperationException("이미 실행 중인 대화 요청이 있습니다.");
            _currentRequestId = ++_lastRequestId;
            _currentOwner = owner;
            _currentTracked = tracked;
            return _currentRequestId;
        }

        // 현재 요청을 끝내고 결과를 요청한 소유자에게만 보관합니다. 같은 소유자의 이전 결과는 대체합니다.
        public void End(DialogueEndReason reason)
        {
            if (!IsRunning) return;
            if (_currentTracked)
            {
                Discard(_currentOwner);
                _ended.Add(new Ended(_currentOwner, _currentRequestId, reason));
            }
            Drop();
        }

        // 시작 자체가 실패한 요청은 호출자가 반환값으로 알기 때문에 결과를 남기지 않습니다.
        public void Drop() { _currentRequestId = 0; _currentOwner = null; _currentTracked = false; }

        // true: 결과 확정(정상·취소·실패). false: 아직 실행 중.
        // 모르는 요청(실행기 교체·정리·이미 가져감)은 진행을 허용하지 않도록 취소로 돌려줍니다.
        public bool TryTake(object owner, int requestId, out DialogueEndReason reason)
        {
            for (int i = 0; i < _ended.Count; i++)
            {
                if (!ReferenceEquals(_ended[i].Owner, owner) || _ended[i].RequestId != requestId) continue;
                reason = _ended[i].Reason;
                _ended.RemoveAt(i);
                return true;
            }
            reason = DialogueEndReason.Cancelled;
            return !(IsCurrent(owner) && _currentRequestId == requestId);
        }

        public void Discard(object owner) => _ended.RemoveAll(entry => ReferenceEquals(entry.Owner, owner));
        public void Clear() { _ended.Clear(); Drop(); }
    }
}
