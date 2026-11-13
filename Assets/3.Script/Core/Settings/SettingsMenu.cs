using System;

namespace HumanGhost.Gameplay
{
    public enum SettingsRow { Music = 0, Effects = 1, CameraShake = 2, Master = 3, KeyboardLayout = 4, WindowMode = 5, Language = 6 }

    // 설정 화면 규칙(O05_1)입니다. 값은 바로 미리 적용하고, 바꾼 채 나가면 확인을 받습니다. 예=저장, 아니오=원래 값.
    public class SettingsMenu
    {
        private static readonly SettingsRow[] _rows = { SettingsRow.Master, SettingsRow.Music, SettingsRow.Effects, SettingsRow.CameraShake, SettingsRow.KeyboardLayout, SettingsRow.Language, SettingsRow.WindowMode };
        private readonly int _rowCount;
        private readonly GameSettings _original;
        public GameSettings Current { get; }
        public SettingsRow Row { get; private set; } = SettingsRow.Master;
        public bool IsConfirming { get; private set; }
        public bool ConfirmYes { get; private set; }
        public bool IsDirty => !Current.SameAs(_original);
        public event Action Changed;

        public SettingsMenu(GameSettings settings, bool allowWindowMode = false)
        {
            _rowCount = allowWindowMode ? _rows.Length : _rows.Length - 1;
            _original = (settings ?? throw new ArgumentNullException(nameof(settings))).Clone();
            Current = settings.Clone();
        }

        public void Move(int direction)
        {
            if (IsConfirming) { ConfirmYes = !ConfirmYes; Changed?.Invoke(); return; }
            int count = _rowCount;
            int index = Array.IndexOf(_rows, Row);
            Row = _rows[(index + direction % count + count) % count];
            Changed?.Invoke();
        }

        public void Select(SettingsRow row)
        {
            int index = Array.IndexOf(_rows, row);
            if (!IsConfirming && Row != row && index >= 0 && index < _rowCount) { Row = row; Changed?.Invoke(); }
        }

        // 화면의 마스터 조작부는 이 진입점을 사용합니다. 확인 중에는 값을 바꾸지 않습니다.
        public void SetMaster(int value)
        {
            if (IsConfirming) return;
            int before = Current.Master;
            Current.SetMaster(value);
            if (before != Current.Master) Changed?.Invoke();
        }

        public void Adjust(int direction)
        {
            if (IsConfirming) return;
            if (Row == SettingsRow.Master) { SetMaster(Current.Master + direction); return; }
            if (Row == SettingsRow.Music) Current.SetMusic(Current.Music + direction);
            else if (Row == SettingsRow.Effects) Current.SetEffects(Current.Effects + direction);
            else if (Row == SettingsRow.CameraShake) Current.SetCameraShake(!Current.CameraShake);
            else if (Row == SettingsRow.KeyboardLayout) Current.SetKeyboardLayout(Current.KeyboardLayout == KeyboardLayout.Arrows ? KeyboardLayout.Wasd : KeyboardLayout.Arrows);
            else if (Row == SettingsRow.Language) Current.SetLanguage(Current.Language == LanguageCode.Korean ? LanguageCode.English : LanguageCode.Korean);
            else if (Row == SettingsRow.WindowMode) Current.SetWindowMode(Current.WindowMode == WindowMode.Fullscreen ? WindowMode.Windowed : WindowMode.Fullscreen);
            Changed?.Invoke();
        }

        // true면 화면을 닫아도 됩니다. false면 확인 창을 띄운 상태입니다. 확인 창 처음 커서는 예입니다.
        public bool RequestClose()
        {
            if (IsConfirming) return false;
            if (!IsDirty) return true;
            IsConfirming = true; ConfirmYes = true;
            Changed?.Invoke();
            return false;
        }

        // 확인 답. 반환값이 저장·적용할 값입니다.
        public GameSettings Answer(bool apply)
        {
            IsConfirming = false;
            GameSettings result = apply ? Current.Clone() : _original.Clone();
            return result;
        }
    }
}
