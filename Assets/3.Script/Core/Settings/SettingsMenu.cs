using System;

namespace HumanGhost.Gameplay
{
    public enum SettingsRow { Music, Effects, CameraShake }

    // 설정 화면 규칙(O05_1)입니다. 값은 바로 미리 적용하고, 바꾼 채 나가면 확인을 받습니다. 예=저장, 아니오=원래 값.
    public sealed class SettingsMenu
    {
        private readonly GameSettings _original;
        public GameSettings Current { get; }
        public SettingsRow Row { get; private set; }
        public bool IsConfirming { get; private set; }
        public bool ConfirmYes { get; private set; }
        public bool IsDirty => !Current.SameAs(_original);
        public event Action Changed;

        public SettingsMenu(GameSettings settings)
        {
            _original = (settings ?? throw new ArgumentNullException(nameof(settings))).Clone();
            Current = settings.Clone();
        }

        public void Move(int direction)
        {
            if (IsConfirming) { ConfirmYes = !ConfirmYes; Changed?.Invoke(); return; }
            int count = Enum.GetValues(typeof(SettingsRow)).Length;
            Row = (SettingsRow)(((int)Row + direction + count) % count);
            Changed?.Invoke();
        }

        public void Select(SettingsRow row) { if (!IsConfirming && Row != row) { Row = row; Changed?.Invoke(); } }

        public void Adjust(int direction)
        {
            if (IsConfirming) return;
            if (Row == SettingsRow.Music) Current.SetMusic(Current.Music + direction);
            else if (Row == SettingsRow.Effects) Current.SetEffects(Current.Effects + direction);
            else Current.SetCameraShake(!Current.CameraShake);
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
