using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.Gameplay.Preview
{
    // 체크포인트 위치로 돌아가되 개별 처치는 현재 슬롯의 마지막 저장 성공 상태로 되돌립니다.
    internal sealed class PreviewDeathRestart : System.IDisposable
    {
        // 1500ms를 어두워짐·검은 화면·밝아짐으로 나눕니다. Dead 애니 원본 전까지 보여 줄 시간은 개발 값입니다.
        private const int _deathAnimationMilliseconds = 600;
        private const int _fadeStepMilliseconds = 500;
        private readonly string _scene;
        private int _deadMilliseconds;
        private MillisecondClock _clock;
        private bool _restarting;
        private readonly SubscriptionScope _lifetime = new SubscriptionScope();

        private readonly GameProgressState _progress;
        private readonly System.Func<SaveSnapshot> _lastSaved;
        private readonly int? _stageAfterLedger;
        public PreviewDeathRestart(string scene, GameProgressState progress, System.Func<SaveSnapshot> lastSaved, int? stageAfterLedger = null)
        {
            _scene = scene;
            _progress = progress ?? throw new System.ArgumentNullException(nameof(progress));
            _lastSaved = lastSaved ?? throw new System.ArgumentNullException(nameof(lastSaved));
            _stageAfterLedger = stageAfterLedger;
        }

        public void Step(PlayerActor actor, float seconds)
        {
            if (_lifetime.IsDisposed || _restarting || !actor.IsDead || PreviewScreenFade.IsActive) return;
            _deadMilliseconds += System.Math.Min(_clock.AdvanceFrame(seconds), _deathAnimationMilliseconds - _deadMilliseconds);
            if (_deadMilliseconds < _deathAnimationMilliseconds) return;
            _restarting = true;
            string scene = _scene;
            PreviewScreenFade.Run(_fadeStepMilliseconds, _fadeStepMilliseconds, _fadeStepMilliseconds, _lifetime.Guard(() =>
            {
                _progress.RestartAfterDeath(_lastSaved(), _stageAfterLedger);
                SceneManager.LoadScene(scene);
            }));
        }

        public void Dispose() => _lifetime.Dispose();
    }
}
