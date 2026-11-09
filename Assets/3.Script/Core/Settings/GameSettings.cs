using System;

namespace HumanGhost.Gameplay
{
    // 설정 값(O05)입니다. 세이브와 별개이며 볼륨은 0~10칸입니다.
    public enum KeyboardLayout { Arrows = 0, Wasd = 1 }
    public enum WindowMode { Fullscreen = 0, Windowed = 1 }

    public class GameSettings
    {
        public const int MaxVolume = 10;
        public int Master { get; private set; } = MaxVolume;
        public int Music { get; private set; } = 8;
        public int Effects { get; private set; } = 8;
        public bool CameraShake { get; private set; } = true;
        public KeyboardLayout KeyboardLayout { get; private set; }
        public WindowMode WindowMode { get; private set; }

        public GameSettings() { }
        public GameSettings(int music, int effects, bool cameraShake) { SetMusic(music); SetEffects(effects); CameraShake = cameraShake; }
        public GameSettings(int master, int music, int effects, bool cameraShake) : this(music, effects, cameraShake) { SetMaster(master); }

        // 마스터와 개별 음량을 합친 백분율입니다. 실수 변환은 소리 출력 경계에서만 합니다.
        public int MusicPercent => Master * Music;
        public int EffectsPercent => Master * Effects;
        public void SetMaster(int value) => Master = ClampVolume(value);
        public void SetMusic(int value) => Music = ClampVolume(value);
        public void SetEffects(int value) => Effects = ClampVolume(value);
        private static int ClampVolume(int value) => Math.Max(0, Math.Min(MaxVolume, value));
        public void SetCameraShake(bool value) => CameraShake = value;
        public void SetKeyboardLayout(KeyboardLayout layout)
        {
            if (layout != KeyboardLayout.Arrows && layout != KeyboardLayout.Wasd)
                throw new ArgumentOutOfRangeException(nameof(layout));
            KeyboardLayout = layout;
        }

        public void SetWindowMode(WindowMode mode)
        {
            if (mode != WindowMode.Fullscreen && mode != WindowMode.Windowed)
                throw new ArgumentOutOfRangeException(nameof(mode));
            WindowMode = mode;
        }

        public GameSettings Clone()
        {
            var copy = new GameSettings(Master, Music, Effects, CameraShake);
            copy.SetKeyboardLayout(KeyboardLayout);
            copy.SetWindowMode(WindowMode);
            return copy;
        }
        public bool SameAs(GameSettings other) => other != null && Master == other.Master && Music == other.Music && Effects == other.Effects && CameraShake == other.CameraShake && KeyboardLayout == other.KeyboardLayout && WindowMode == other.WindowMode;
    }
}
