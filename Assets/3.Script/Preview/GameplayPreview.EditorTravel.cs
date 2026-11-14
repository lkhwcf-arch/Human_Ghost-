#if UNITY_EDITOR
using HumanGhost.Gameplay.Presentation;

namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        public bool CanEditorTravel => UnityEngine.Application.isPlaying && isActiveAndEnabled && _subscribed &&
            !_modalBlocked && !_saveLocked && !_leaving && !LoadingScreen.IsActive && !_actor.IsDead &&
            _saveFlow != null && _saveFlow.IsPlayable && !_saveFlow.IsBusy &&
            (_pause == null || !_pause.IsOpen) && (_textbox == null || !_textbox.IsOpen);

        public void TravelToTestRoom(bool boss)
        {
            if (!CanEditorTravel) return;
            int targetStage = boss ? BossStage : StageExitRules.LedgerStage;
            GoToStage(targetStage, GetUniqueArrivalAnchor(targetStage));
        }
    }
}
#endif
