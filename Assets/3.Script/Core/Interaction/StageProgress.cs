using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // 클리어한 맵 목록(K14 clearedStages)입니다. 영구 저장 연결은 저장 담당과 맞춥니다. ContentAccess에 IsCleared를 전달합니다.
    public sealed class StageProgress
    {
        private readonly HashSet<int> _cleared = new HashSet<int>();
        public event System.Action Changed;
        public int[] GetCleared() { var ids = new int[_cleared.Count]; _cleared.CopyTo(ids); System.Array.Sort(ids); return ids; }
        public bool IsCleared(int stageId) => _cleared.Contains(stageId);
        public bool Clear(int stageId)
        {
            if (!_cleared.Add(stageId)) return false;
            Changed?.Invoke();
            return true;
        }
    }
}
