using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.Gameplay.Preview
{
    // 체크포인트 위치로 돌아가되 개별 처치는 현재 슬롯의 마지막 저장 성공 상태로 되돌립니다.
    internal sealed class PreviewDeathRestart : System.IDisposable
    {
        // 1500ms를 어두워짐·검은 화면·밝아짐으로 나눕니다. Dead 애니 원본 전까지 보여 줄 시간은 개발 값입니다.
        private const float _deathAnimationSeconds = 0.6f;
        private const float _fadeStepSeconds = 0.5f;
        private readonly string _scene;
        private float _deadSeconds;
        private bool _restarting;
        private readonly SubscriptionScope _lifetime = new SubscriptionScope();

        public PreviewDeathRestart(string scene) => _scene = scene;

        public void Step(PlayerActor actor, float seconds)
        {
            if (_lifetime.IsDisposed || _restarting || !actor.IsDead || PreviewScreenFade.IsActive) return;
            _deadSeconds += seconds;
            if (_deadSeconds < _deathAnimationSeconds) return;
            _restarting = true;
            string scene = _scene;
            PreviewScreenFade.Run(_fadeStepSeconds, _fadeStepSeconds, _fadeStepSeconds, _lifetime.Guard(() =>
            {
                SaveSnapshot saved = HumanGhost.Gameplay.Adapters.GameSaveContext.Current?.Session.CurrentSnapshot;
                PreviewSession.DefeatedMonsters.Clear();
                if (saved != null) PreviewSession.DefeatedMonsters.UnionWith(saved.DefeatedMonsters);
                PreviewSession.CarriedHealth = null;
                PreviewSession.CurrentStage = PreviewSession.CheckpointPassed ? PreviewSession.ShrineId : StageExitRules.VillageStage;
                PreviewSession.ArrivalAnchor = PreviewSession.CheckpointPassed ? "Respawn_Seonangdang" : null;
                SceneManager.LoadScene(scene);
            }));
        }

        public void Dispose() => _lifetime.Dispose();
    }
}
