using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.Gameplay.Preview
{
    // 사망 뒤 암전 재시작(K16add·O09)입니다. 체크포인트는 맵 1 시작 지점이며 같은 맵이라 로딩 없이 씬을 다시 불러옵니다.
    // 서낭당 체크포인트·보스 처치 유지는 맵 3 연결 때 추가합니다.
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
            PreviewScreenFade.Run(_fadeStepSeconds, _fadeStepSeconds, _fadeStepSeconds, () => SceneManager.LoadScene(scene));
        }
    }
}
