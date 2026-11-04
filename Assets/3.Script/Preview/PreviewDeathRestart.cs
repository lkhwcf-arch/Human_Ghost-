using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.Gameplay.Preview
{
    // 사망 뒤 암전 재시작(K16add·O09)입니다. 세이브 포인트 전이면 맵 1 시작 지점, 지난 뒤면 맵 3 서낭당 앞입니다. 잡몹은 다시 나옵니다.
    internal sealed class PreviewDeathRestart
    {
        // 1500ms를 어두워짐·검은 화면·밝아짐으로 나눕니다. Dead 애니 원본 전까지 보여 줄 시간은 개발 값입니다.
        private const float _deathAnimationSeconds = 0.6f;
        private const float _fadeStepSeconds = 0.5f;
        private readonly string _scene;
        private float _deadSeconds;
        private bool _restarting;

        public PreviewDeathRestart(string scene) => _scene = scene;

        public void Step(PlayerActor actor, float seconds)
        {
            if (_restarting || !actor.IsDead) return;
            _deadSeconds += seconds;
            if (_deadSeconds < _deathAnimationSeconds) return;
            _restarting = true;
            string scene = _scene;
            PreviewScreenFade.Run(_fadeStepSeconds, _fadeStepSeconds, _fadeStepSeconds, () =>
            {
                PreviewSession.CarriedHealth = null;
                PreviewSession.CurrentStage = PreviewSession.CheckpointPassed ? 8003 : StageExitRules.VillageStage;
                PreviewSession.ArrivalAnchor = PreviewSession.CheckpointPassed ? "Respawn_Seonangdang" : null;
                SceneManager.LoadScene(scene);
            });
        }
    }
}
