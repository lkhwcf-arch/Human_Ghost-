using System;

namespace HumanGhost.Gameplay
{
    // 설정 값(O05)입니다. 세이브와 별개이며 볼륨은 0~10칸입니다.
    public sealed class GameSettings
    {
        public const int MaxVolume = 10;
        public int Music { get; private set; } = 8;
        public int Effects { get; private set; } = 8;
        public bool CameraShake { get; private set; } = true;

        public GameSettings() { }
        public GameSettings(int music, int effects, bool cameraShake) { SetMusic(music); SetEffects(effects); CameraShake = cameraShake; }

        public void SetMusic(int value) => Music = Math.Max(0, Math.Min(MaxVolume, value));
        public void SetEffects(int value) => Effects = Math.Max(0, Math.Min(MaxVolume, value));
        public void SetCameraShake(bool value) => CameraShake = value;
        public GameSettings Clone() => new GameSettings(Music, Effects, CameraShake);
        public bool SameAs(GameSettings other) => other != null && Music == other.Music && Effects == other.Effects && CameraShake == other.CameraShake;
    }
}
