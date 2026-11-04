using System;
using System.Linq;
using HumanGhost.Gameplay;

internal static class TitleMenuChecks
{
    public static void Run(Action<bool, string> check)
    {
        var menu = new TitleMenu(false, true, true, true);
        int changes = 0;
        Action Observe = () => changes++;
        menu.Changed += Observe;
        check(menu.Items.SequenceEqual(new[] { TitleAction.NewGame, TitleAction.Settings, TitleAction.Quit }), "저장이 없으면 이어하기를 제외한 연속 목록");
        menu.Back();
        check(changes == 0 && menu.Selected == TitleAction.NewGame, "타이틀 뒤로 입력 무시");
        menu.Move(-1);
        check(menu.Selected == TitleAction.Quit, "보이는 목록의 끝으로 순환 이동");
        menu.Move(1);
        check(menu.Submit() == TitleAction.NewGame, "저장이 없으면 확인 없이 새 게임 요청");
        check(menu.Submit() == null, "처리 중 중복 요청 차단");
        menu.Move(1);
        check(menu.Selected == TitleAction.NewGame, "처리 중 선택 이동 차단");
        menu.CompleteRequest();
        menu.RefreshSave(true);
        check(menu.Items.Count == 4 && menu.Selected == TitleAction.NewGame, "저장 갱신 시 기존 선택 유지");
        check(menu.Submit() == null && menu.State == TitleMenuState.ConfirmingNewGame, "저장이 있으면 확인 단계 진입");
        check(menu.AnswerNewGame(false) == null && menu.State == TitleMenuState.Selecting, "새 게임 확인 취소");
        menu.Submit();
        check(menu.AnswerNewGame(true) == TitleAction.NewGame, "확인 후 새 게임 요청");
        menu.CompleteRequest();
        menu.Move(1);
        check(menu.Selected == TitleAction.Continue && menu.Submit() == TitleAction.Continue, "이어하기 요청");
        menu.CompleteRequest();
        menu.RefreshSave(false);
        check(menu.Selected == TitleAction.NewGame && !menu.Items.Contains(TitleAction.Continue), "저장 제거 시 숨긴 선택에서 복구");
        menu.Move(1);
        check(menu.Submit() == TitleAction.Settings, "설정 요청");
        menu.CompleteRequest();
        menu.Move(1);
        check(menu.Submit() == TitleAction.Quit, "종료 요청");
        menu.Changed -= Observe;
        int before = changes;
        menu.CompleteRequest();
        check(changes == before, "표시 소비자 구독 해제");
        var booth = new TitleMenu(true, false, false, true);
        check(booth.Selected == TitleAction.Continue && !booth.Items.Contains(TitleAction.Quit), "전달된 최초 선택과 종료 숨김 정책");
        booth.Move(-1);
        check(booth.Submit() == TitleAction.NewGame, "부스 정책은 저장이 있어도 확인 생략");
        booth.CompleteRequest();
        booth.RefreshSave(false);
        check(booth.Items.Count == 2, "부스에서 저장과 종료가 모두 없으면 두 메뉴만 구성");
        var defaultNo = new TitleMenu(true, true, true, false, false);
        defaultNo.Submit();
        check(defaultNo.State == TitleMenuState.ConfirmingNewGame && !defaultNo.ConfirmYes, "Popup 기본 아니오면 확인 창 처음 커서는 아니오");
        check(defaultNo.AnswerNewGame(defaultNo.ConfirmYes) == null && defaultNo.State == TitleMenuState.Selecting, "기본 아니오에서 바로 결정하면 메뉴로 복귀");
        defaultNo.Submit();
        defaultNo.Move(1);
        check(defaultNo.ConfirmYes, "확인 창 위아래 이동으로 답 전환");
        check(defaultNo.AnswerNewGame(defaultNo.ConfirmYes) == TitleAction.NewGame, "예를 고른 뒤 결정하면 새 게임 요청");
        defaultNo.CompleteRequest();
        defaultNo.Submit();
        check(!defaultNo.ConfirmYes, "확인 창을 다시 열면 처음 커서로 복귀");
        defaultNo.AnswerNewGame(false);
        defaultNo.SelectItem(TitleAction.Settings);
        check(defaultNo.Selected == TitleAction.Settings, "마우스 올리기로 항목 직접 선택");
        var hidden = new TitleMenu(false, true, true, true);
        hidden.SelectItem(TitleAction.Continue);
        check(hidden.Selected == TitleAction.NewGame, "숨긴 항목은 직접 선택 불가");
        hidden.Submit();
        hidden.SelectItem(TitleAction.Quit);
        check(hidden.Selected == TitleAction.NewGame, "처리 중 직접 선택 차단");
    }
}
