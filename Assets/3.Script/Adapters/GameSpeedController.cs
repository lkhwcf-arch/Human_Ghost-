using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // GameSpeedState를 Time.timeScale로 옮기는 유일한 지점입니다. 씬을 떠나면 원래 속도로 돌립니다.
    [DefaultExecutionOrder(-100)]
    public sealed class GameSpeedController : MonoBehaviour
    {
        private float _pendingMs;
        public GameSpeedState State { get; } = new GameSpeedState();

        private void Update()
        {
            _pendingMs += Time.unscaledDeltaTime * 1000f;
            int whole = (int)_pendingMs;
            _pendingMs -= whole;
            State.Tick(whole);
            Time.timeScale = State.Scale;
        }

        private void OnDisable() => Time.timeScale = 1f;
    }
}
