using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // 클리어한 맵 목록(K14 clearedStages)입니다. 영구 저장 연결은 저장 담당과 맞춥니다. ContentAccess에 IsCleared를 전달합니다.
    public sealed class StageProgress
    {
        private readonly HashSet<int> _cleared = new HashSet<int>();
        public bool IsCleared(int stageId) => _cleared.Contains(stageId);
        public bool Clear(int stageId) => _cleared.Add(stageId);
    }
}
