using System;
using HumanGhost.Gameplay;

// 게임 속도·전투 판정·설정·일시정지 흐름 검사입니다.
internal static class GameLayerChecks
{
    public static void Run(Action<bool, string> check)
    {
        var speed = new GameSpeedState();
        check(speed.Scale == 1f, "기본 속도 1");
        speed.HitStop(50);
        check(speed.Scale == 0f, "히트스톱 중 멈춤");
        speed.Tick(50);
        check(speed.Scale == 1f, "히트스톱 뒤 복귀");
        speed.Critical(80, 200, 0.5f);
        check(speed.Scale == 0f, "치명타 멈춤 먼저");
        speed.Tick(80);
        check(speed.Scale == 0.5f, "멈춤 뒤 슬로 50%");
        speed.SetPaused(true);
        check(speed.Scale == 0f && speed.IsPaused, "슬로 중 일시정지");
        speed.Tick(1000);
        speed.SetPaused(false);
        check(speed.Scale == 0.5f, "일시정지 뒤 슬로 남은 시간 유지");
        speed.Tick(200);
        check(speed.Scale == 1f, "슬로 끝나면 정상 속도");
        speed.Critical(80, 200, 0.5f);
        speed.ClearSlow();
        speed.HitStop(150);
        speed.Tick(150);
        check(speed.Scale == 1f, "해체는 치명 슬로를 겹치지 않음");

        var combat = new CombatPresence(3000);
        check(!combat.InCombat, "처음은 탐색");
        combat.Engage();
        combat.Tick(2999);
        check(combat.InCombat, "3초 안은 전투");
        combat.Tick(1);
        check(!combat.InCombat, "3초 뒤 탐색");

        var defaults = new GameSettings();
        check(defaults.Music == 8 && defaults.Effects == 8 && defaults.CameraShake, "설정 기본값");
        var menu = new SettingsMenu(defaults);
        check(menu.RequestClose(), "바꾼 게 없으면 확인 없이 닫음");
        menu.Adjust(1);
        check(menu.Current.Music == 9 && menu.IsDirty && defaults.Music == 8, "값은 사본에 미리 적용");
        for (int i = 0; i < 5; i++) menu.Adjust(1);
        check(menu.Current.Music == 10, "볼륨 상한 10");
        check(!menu.RequestClose() && menu.IsConfirming && menu.ConfirmYes, "바꾼 채 닫으면 확인, 처음 커서 예");
        menu.Adjust(1);
        check(menu.Current.Music == 10, "확인 중 값 고정");
        check(menu.Answer(false).Music == 8, "아니오면 원래 값");
        var shake = new SettingsMenu(new GameSettings());
        shake.Move(1); shake.Move(1);
        check(shake.Row == SettingsRow.CameraShake, "줄 이동");
        shake.Adjust(1);
        check(!shake.Current.CameraShake, "흔들림 켜기·끄기");
        shake.RequestClose();
        check(!shake.Answer(true).CameraShake, "예면 바꾼 값");

        var pause = new PauseMenu(false);
        check(pause.Selected == PauseItem.Resume && pause.Submit() == PauseRequest.Close, "계속하기로 닫기");
        check(pause.Back() == PauseRequest.Close, "첫 화면 뒤로는 닫기");
        pause.Move(1);
        check(pause.Selected == PauseItem.Controls && pause.Submit() == PauseRequest.None && pause.View == PauseView.Controls, "조작 안내 진입");
        check(pause.Back() == PauseRequest.None && pause.View == PauseView.Main && pause.Selected == PauseItem.Controls, "뒤로 뒤 커서 유지");
        pause.Move(1);
        check(pause.Submit() == PauseRequest.OpenSettings, "설정 요청");
        pause.Move(1);
        check(pause.Submit() == PauseRequest.None && pause.View == PauseView.ConfirmTitle && !pause.ConfirmYes, "타이틀로 확인, 데이터 기본 아니오");
        check(pause.Submit() == PauseRequest.None && pause.View == PauseView.Main, "아니오면 메뉴로");
        pause.Submit(); pause.Move(1);
        check(pause.ConfirmYes && pause.Submit() == PauseRequest.ReturnToTitle, "예면 타이틀로");
    }
}
