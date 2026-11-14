#if UNITY_EDITOR
using System;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Presentation;

namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        public bool CanEditorTravel => UnityEngine.Application.isPlaying && isActiveAndEnabled && _subscribed &&
            !_modalBlocked && !_saveLocked && !_leaving && !LoadingScreen.IsActive && !_actor.IsDead &&
            _saveFlow != null && _saveFlow.IsPlayable && !_saveFlow.IsBusy &&
            (_textbox == null || !_textbox.IsOpen);

        // 에디터 메뉴를 누르면 게임 창이 포커스를 잃어 일시정지가 열리므로, 이동 조건에서 빼고 이동 직전에 닫습니다.
        private void CloseEditorPause() => _pause?.CloseIfOpen();

        public void TravelToTestRoom(bool boss)
        {
            if (!CanEditorTravel) return;
            CloseEditorPause();
            int targetStage = boss ? BossStage : StageExitRules.LedgerStage;
            GoToStage(targetStage, GetUniqueArrivalAnchor(targetStage));
        }

        // 정방향 입구로 이동합니다. 도착 앵커가 여러 개인 스테이지(예: 갈림길)도 데이터로 하나를 고릅니다.
        public void TravelToStageEntrance(int stageId)
        {
            if (!CanEditorTravel) return;
            string anchor = GetForwardArrivalAnchor(stageId);
            CloseEditorPause();
            GoToStage(stageId, anchor);
        }

        // Stage.NextStage로 이 스테이지를 가리키는 앞 스테이지의 출구 도착 앵커를 씁니다. 앵커 이름을 코드에 두지 않습니다.
        private string GetForwardArrivalAnchor(int stageId)
        {
            int[] previous = _catalog.GetTable<Stage>().All.Where(stage => stage.NextStage == stageId).Select(stage => stage.ID).ToArray();
            string[] arrivals = _catalog.GetTable<StageExit>().All
                .Where(row => row.ContentEnabled && row.TargetStageID == stageId && Array.IndexOf(previous, row.StageID) >= 0)
                .Select(row => row.ArrivalAnchorName).Distinct().ToArray();
            if (arrivals.Length != 1 || string.IsNullOrWhiteSpace(arrivals[0]))
                throw new InvalidOperationException("정방향 입구를 하나로 정할 수 없습니다. Stage.NextStage와 StageExit 연결을 확인해 주세요. Stage: " + stageId);
            return arrivals[0];
        }
    }
}
#endif
