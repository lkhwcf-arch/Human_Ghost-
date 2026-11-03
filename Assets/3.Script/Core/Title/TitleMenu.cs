using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    public enum TitleAction { NewGame, Continue, Settings, Quit }
    public enum TitleMenuState { Selecting, ConfirmingNewGame, WaitingForCompletion }

    // 빌드별 정책은 조립자가 전달합니다. 저장 파일·화면·씬 이동은 소유하지 않습니다.
    public sealed class TitleMenu
    {
        private readonly List<TitleAction> _items = new List<TitleAction>();
        private readonly bool _showQuit;
        private readonly bool _confirmNewGameWithSave;
        private bool _hasSave;
        private int _selectedIndex;

        public IReadOnlyList<TitleAction> Items { get; }
        public TitleAction Selected => _items[_selectedIndex];
        public TitleMenuState State { get; private set; }
        public event Action Changed;

        public TitleMenu(bool hasSave, bool showQuit, bool confirmNewGameWithSave, bool preferContinue)
        {
            _showQuit = showQuit;
            _confirmNewGameWithSave = confirmNewGameWithSave;
            Items = _items.AsReadOnly();
            Rebuild(hasSave);
            if (hasSave && preferContinue) _selectedIndex = _items.IndexOf(TitleAction.Continue);
        }

        public void Move(int direction)
        {
            if (direction != -1 && direction != 1)
                throw new ArgumentOutOfRangeException(nameof(direction), "선택 이동은 -1 또는 1입니다.");
            if (State != TitleMenuState.Selecting) return;
            _selectedIndex = (_selectedIndex + direction + _items.Count) % _items.Count;
            Changed?.Invoke();
        }

        public TitleAction? Submit()
        {
            if (State != TitleMenuState.Selecting) return null;
            if (Selected == TitleAction.NewGame && _hasSave && _confirmNewGameWithSave)
            {
                SetState(TitleMenuState.ConfirmingNewGame);
                return null;
            }
            return Request(Selected);
        }

        public TitleAction? AnswerNewGame(bool accepted)
        {
            if (State != TitleMenuState.ConfirmingNewGame) return null;
            if (accepted) return Request(TitleAction.NewGame);
            SetState(TitleMenuState.Selecting);
            return null;
        }

        // 타이틀 자체에는 뒤로 동작이 없습니다. 확인 창 취소는 AnswerNewGame으로 구분합니다.
        public void Back() { }

        // 설정 화면 복귀 또는 실행 실패 후 호출합니다. 성공한 씬 이동은 기존 메뉴를 폐기합니다.
        public void CompleteRequest()
        {
            if (State == TitleMenuState.WaitingForCompletion) SetState(TitleMenuState.Selecting);
        }

        // 저장 조회가 완료된 뒤 명시적으로 호출하며, 실행 중인 요청의 조건을 바꾸지 않습니다.
        public void RefreshSave(bool hasSave)
        {
            if (State != TitleMenuState.Selecting)
                throw new InvalidOperationException("메뉴 선택 상태에서만 저장 상태를 갱신할 수 있습니다.");
            if (_hasSave == hasSave) return;
            TitleAction previous = Selected;
            Rebuild(hasSave);
            _selectedIndex = Math.Max(0, _items.IndexOf(previous));
            Changed?.Invoke();
        }

        private void Rebuild(bool hasSave)
        {
            _hasSave = hasSave;
            _items.Clear();
            _items.Add(TitleAction.NewGame);
            if (hasSave) _items.Add(TitleAction.Continue);
            _items.Add(TitleAction.Settings);
            if (_showQuit) _items.Add(TitleAction.Quit);
        }

        private TitleAction Request(TitleAction action)
        {
            SetState(TitleMenuState.WaitingForCompletion);
            return action;
        }

        private void SetState(TitleMenuState state)
        {
            State = state;
            Changed?.Invoke();
        }
    }
}
