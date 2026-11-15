using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // GameSpeedState를 Time.timeScale로 옮기는 유일한 지점입니다. 씬을 떠나면 원래 속도로 돌립니다.
    [DefaultExecutionOrder(-100)]
    public sealed class GameSpeedController : MonoBehaviour
    {
        private MillisecondClock _clock;
        public GameSpeedState State { get; } = new GameSpeedState();

        private void Update()
        {
            State.Tick(_clock.Advance(Time.unscaledDeltaTime));
            float scale = State.Scale / (float)GameSpeedState.RateScale;
            if (Time.timeScale != scale) Time.timeScale = scale;
        }

        private void OnDisable() => Time.timeScale = 1f;
    }
}
