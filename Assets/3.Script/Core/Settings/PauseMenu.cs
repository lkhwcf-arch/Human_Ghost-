using System;

namespace HumanGhost.Gameplay
{
    public enum PauseItem { Resume, Controls, Settings, ReturnToTitle }
    public enum PauseView { Main, Controls, ConfirmTitle }
    public enum PauseRequest { None, Close, OpenSettings, ReturnToTitle }

    // 일시정지 계층(O04·O04_1)입니다. 첫 화면에서 뒤로=닫기, 하위 화면에서 뒤로=한 단계 위, 돌아오면 커서 유지.
    public sealed class PauseMenu
    {
        public PauseItem Selected { get; private set; }
        public PauseView View { get; private set; }
        public bool ConfirmYes { get; private set; }
        private readonly bool _confirmDefaultYes;
        public event Action Changed;

        public PauseMenu(bool confirmDefaultYes) => _confirmDefaultYes = confirmDefaultYes;

        public void Move(int direction)
        {
            if (View == PauseView.ConfirmTitle) { ConfirmYes = !ConfirmYes; Changed?.Invoke(); return; }
            if (View != PauseView.Main) return;
            int count = Enum.GetValues(typeof(PauseItem)).Length;
            Selected = (PauseItem)(((int)Selected + direction + count) % count);
            Changed?.Invoke();
        }

        public void Select(PauseItem item) { if (View == PauseView.Main && Selected != item) { Selected = item; Changed?.Invoke(); } }
        public void SelectConfirm(bool yes) { if (View == PauseView.ConfirmTitle && ConfirmYes != yes) { ConfirmYes = yes; Changed?.Invoke(); } }

        public PauseRequest Submit()
        {
            if (View == PauseView.Controls) return PauseRequest.None;
            if (View == PauseView.ConfirmTitle)
            {
                bool yes = ConfirmYes;
                View = PauseView.Main;
                Changed?.Invoke();
                return yes ? PauseRequest.ReturnToTitle : PauseRequest.None;
            }
            switch (Selected)
            {
                case PauseItem.Resume: return PauseRequest.Close;
                case PauseItem.Settings: return PauseRequest.OpenSettings;
                case PauseItem.Controls: View = PauseView.Controls; break;
                default: View = PauseView.ConfirmTitle; ConfirmYes = _confirmDefaultYes; break;
            }
            Changed?.Invoke();
            return PauseRequest.None;
        }

        public PauseRequest Back()
        {
            if (View == PauseView.Main) return PauseRequest.Close;
            View = PauseView.Main;
            Changed?.Invoke();
            return PauseRequest.None;
        }
    }
}
