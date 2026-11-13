using System;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;

// 게임 속도·전투 판정·설정·일시정지 흐름 검사입니다.
internal static class GameLayerChecks
{
    public static void Run(Action<bool, string> check)
    {
        var layout = new StageLayout { FormatVersion = 1, StageID = 8002 };
        layout.Placements.Add(new StagePlacement { PlacementID = 101, Prefab = "Ground_2", Anchor = "Anchor_Ground_2", Monsters = new System.Collections.Generic.List<StageMonsterGroup> {
            new StageMonsterGroup { MonsterCode = 3002, MonsterCount = 2, Points = new System.Collections.Generic.List<System.Collections.Generic.List<int>> { new System.Collections.Generic.List<int> { 1750, 0 }, new System.Collections.Generic.List<int> { 6500, 0 } } },
            new StageMonsterGroup { MonsterCode = 3003, MonsterCount = 2, Points = new System.Collections.Generic.List<System.Collections.Generic.List<int>> { new System.Collections.Generic.List<int> { 4050, 0 } } },
            new StageMonsterGroup { MonsterCode = 9999, MonsterCount = 1, Points = new System.Collections.Generic.List<System.Collections.Generic.List<int>> { new System.Collections.Generic.List<int> { 1, 0 } } },
            new StageMonsterGroup { MonsterCode = 3004, MonsterCount = 1, Points = new System.Collections.Generic.List<System.Collections.Generic.List<int>> { new System.Collections.Generic.List<int> { 2, 0 } } } } });
        layout.Placements.Add(new StagePlacement { PlacementID = 101 });
        var warnings = new System.Collections.Generic.List<string>();
        var spawns = StageLayoutRules.ResolveMonsters(layout, c => c < 9000, c => c != 3004, warnings);
        check(spawns.Count == 2 && spawns[0].Code == 3002 && spawns[0].X == 1750 && spawns[1].Sequence == 2, "JSON 몬스터 해석·순번");
        check(warnings.Count == 3, "수량 불일치·없는 코드·배치 번호 중복 경고");
        var ambush = new StageLayout();
        ambush.Placements.Add(new StagePlacement { PlacementID = 1, Monsters = new System.Collections.Generic.List<StageMonsterGroup> {
            new StageMonsterGroup { MonsterCode = 3003, MonsterCount = 1, Trigger = 6600, Points = new System.Collections.Generic.List<System.Collections.Generic.List<int>> { new System.Collections.Generic.List<int> { 6850, 0 } } },
            new StageMonsterGroup { MonsterCode = 3003, MonsterCount = 1, Points = new System.Collections.Generic.List<System.Collections.Generic.List<int>> { new System.Collections.Generic.List<int> { 100, 0 } } } } });
        var ambushSpawns = StageLayoutRules.ResolveMonsters(ambush, c => true, c => true, warnings);
        check(ambushSpawns[0].Trigger == 6600 && ambushSpawns[1].Trigger == null, "매복 trigger 전달");
        check(StageExitRules.CanLeave(8001, true) && !StageExitRules.CanLeave(8001, false), "마을 출구는 명부 조사 기준");
        check(StageExitRules.CanLeave(8002, false), "산길 출구는 전멸·명부 조건과 독립");
        check(StageExitRules.CanLeave(8003, false), "협곡 출구는 전멸 조건과 독립");

        check(StageLayoutRules.UsesGroundLine("Ground_2") && !StageLayoutRules.UsesGroundLine("Air_Platform_3"), "몬스터 높이 기준: Ground 바닥선, 발판 앵커");
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

        speed.SetPaused(true);
        speed.SetModalPaused(true);
        speed.SetModalPaused(false);
        check(speed.IsPaused && speed.Scale == 0f, "오류 팝업을 닫아도 기존 일시정지는 유지");
        speed.SetModalPaused(true);
        speed.SetPaused(false);
        check(speed.IsPaused && speed.Scale == 0f, "기존 일시정지를 풀어도 오류 팝업 잠금은 유지");
        speed.HitStop(50);
        speed.Tick(1000);
        speed.SetModalPaused(false);
        check(!speed.IsPaused && speed.Scale == 0f, "오류 팝업 동안 히트스톱 시간 보존");
        speed.Tick(50);
        check(speed.Scale == 1f, "모든 잠금과 연출 종료 뒤 정상 속도 복귀");
        var combat = new CombatPresence(3000);
        check(!combat.InCombat, "처음은 탐색");
        combat.Engage();
        combat.Tick(2999);
        check(combat.InCombat, "3초 안은 전투");
        combat.Tick(1);
        check(!combat.InCombat, "3초 뒤 탐색");

        var layoutSettings = new GameSettings();
        layoutSettings.SetKeyboardLayout(KeyboardLayout.Wasd);
        var layoutCopy = layoutSettings.Clone();
        check(layoutCopy.KeyboardLayout == KeyboardLayout.Wasd && layoutCopy.SameAs(layoutSettings), "조작 방식 사본 보존");
        layoutCopy.SetKeyboardLayout(KeyboardLayout.Arrows);
        check(!layoutCopy.SameAs(layoutSettings), "조작 방식 변경은 설정 변경으로 판정");
        var layoutMenu = new SettingsMenu(layoutSettings);
        layoutMenu.Select(SettingsRow.KeyboardLayout);
        layoutMenu.Adjust(1);
        check(layoutMenu.Current.KeyboardLayout == KeyboardLayout.Arrows && layoutMenu.IsDirty, "옵션에서 조작 방식 전환");
        check(layoutMenu.Answer(false).KeyboardLayout == KeyboardLayout.Wasd, "조작 방식 취소 시 원래 값 복원");
        var defaults = new GameSettings();
        var windowSettings = defaults.Clone();
        windowSettings.SetWindowMode(WindowMode.Windowed);
        check(defaults.WindowMode == WindowMode.Fullscreen && !defaults.SameAs(windowSettings)
            && windowSettings.Clone().WindowMode == WindowMode.Windowed, "화면 모드는 원본과 독립적으로 복제·변경 판정");
        var titleSettings = new SettingsMenu(defaults, allowWindowMode: true);
        titleSettings.Select(SettingsRow.WindowMode);
        titleSettings.Adjust(1);
        check(titleSettings.Current.WindowMode == WindowMode.Windowed && titleSettings.IsDirty, "타이틀 화면 모드 전환");
        check(!titleSettings.RequestClose() && titleSettings.Answer(false).WindowMode == WindowMode.Fullscreen, "화면 모드 취소 시 원래 값 복구");
        var pauseSettings = new SettingsMenu(windowSettings);
        pauseSettings.Select(SettingsRow.WindowMode);
        check(pauseSettings.Row == SettingsRow.Master, "일시정지에서 화면 모드 행 선택 차단");
        pauseSettings.Move(-1);
        check(pauseSettings.Row == SettingsRow.Language, "일시정지 순환에 화면 모드 행 제외");
        var titleNavigation = new SettingsMenu(defaults, true);
        titleNavigation.Move(-1);
        check(titleNavigation.Row == SettingsRow.WindowMode, "타이틀 순환에 화면 모드 행 포함");
        var failedWindowSave = new SettingsApplication(value => windowSettings = value,
            value => throw new InvalidOperationException("화면 모드 저장 실패 검사"), error => { });
        var windowSelection = defaults.Clone(); windowSelection.SetWindowMode(WindowMode.Windowed);
        check(!failedWindowSave.Complete(defaults, windowSelection) && windowSettings.WindowMode == WindowMode.Fullscreen,
            "화면 모드 저장 실패 시 기존 공용 경계에서 복구");
        GameSettings applied = null;
        Exception reported = null;
        int saves = 0;
        var application = new SettingsApplication(value => applied = value, value => saves++, error => reported = error);
        check(application.Complete(defaults, defaults.Clone()) && saves == 0, "미변경 설정은 적용하되 다시 저장하지 않음");
        var changedSettings = new GameSettings(4, 8, 8, true);
        check(application.Complete(defaults, changedSettings) && saves == 1 && applied.Master == 4, "타이틀·맵의 공용 설정 적용과 저장");
        var brokenStore = new SettingsApplication(value => applied = value, value => throw new InvalidOperationException("설정 파일 쓰기 실패 검사"), error => reported = error);
        check(!brokenStore.Complete(defaults, changedSettings) && applied.SameAs(defaults) && reported != null, "설정 저장 실패 시 원래 값 복구와 오류 전달");
        KeyboardLayout activeLayout = KeyboardLayout.Arrows;
        var keyboardSettings = defaults.Clone(); keyboardSettings.SetKeyboardLayout(KeyboardLayout.Wasd);
        var keyboardApplication = new SettingsApplication(value => applied = value, value => { }, error => reported = error, layout => activeLayout = layout);
        check(keyboardApplication.Complete(defaults, keyboardSettings) && activeLayout == KeyboardLayout.Wasd
            && defaults.KeyboardLayout == KeyboardLayout.Arrows, "선택한 키 배치는 입력에 적용하고 설정 원본은 보존");
        var keyboardFailure = new SettingsApplication(value => applied = value, value => throw new InvalidOperationException("키 배치 저장 실패"),
            error => reported = error, layout => activeLayout = layout);
        check(!keyboardFailure.Complete(defaults, keyboardSettings) && activeLayout == KeyboardLayout.Arrows, "키 배치 저장 실패 시 입력도 이전 방식으로 복구");
        var brokenApply = new SettingsApplication(value => { if (value.Master == 4) throw new InvalidOperationException("표시 적용 실패 검사"); applied = value; }, value => saves++, error => reported = error);
        check(!brokenApply.Complete(defaults, changedSettings) && saves == 1 && applied.SameAs(defaults), "설정 적용 실패 시 저장하지 않고 원래 값 복구");
        var mutatingConsumer = new SettingsApplication(value => value.SetMaster(0), value => value.SetEffects(0), error => reported = error);
        check(mutatingConsumer.Complete(defaults, changedSettings) && defaults.Master == 10 && changedSettings.Master == 4 && changedSettings.Effects == 8,
            "외부 소비자가 전달값을 바꿔도 설정 원본과 편집 결과 보존");
        check(defaults.Master == 10 && defaults.MusicPercent == 80 && defaults.EffectsPercent == 80, "기본 마스터는 기존 개별 음량을 유지" );
        var volumes = new GameSettings(5, 8, 6, true);
        check(volumes.MusicPercent == 40 && volumes.EffectsPercent == 30, "마스터와 개별 음량을 곱한 출력");
        volumes.SetMaster(0);
        check(volumes.MusicPercent == 0 && volumes.EffectsPercent == 0 && volumes.Music == 8 && volumes.Effects == 6, "전체 무음이어도 개별 음량 보존");
        volumes.SetMaster(20);
        check(volumes.Master == 10 && volumes.MusicPercent == 80, "마스터 상한과 개별 음량 복귀");
        volumes.SetMaster(-1);
        check(volumes.Master == 0, "마스터 하한");
        var masterMenu = new SettingsMenu(defaults);
        int changed = 0;
        masterMenu.Changed += () => changed++;
        masterMenu.SetMaster(5);
        masterMenu.SetMaster(5);
        check(changed == 1 && masterMenu.IsDirty && defaults.Master == 10, "마스터 사본 변경만 한 번 통지");
        check(!masterMenu.RequestClose(), "마스터만 바꿔도 저장 확인");
        masterMenu.SetMaster(3);
        check(masterMenu.Current.Master == 5, "저장 확인 중 마스터 변경 차단");
        check(masterMenu.Answer(false).Master == 10, "마스터 변경 취소 시 원래 음량 복구");
        var applyMaster = new SettingsMenu(defaults);
        applyMaster.SetMaster(4);
        applyMaster.RequestClose();
        GameSettings accepted = applyMaster.Answer(true);
        check(accepted.Master == 4 && accepted.Clone().SameAs(accepted), "마스터 변경 적용과 복제 보존");
        check(defaults.Music == 8 && defaults.Effects == 8 && defaults.CameraShake, "설정 기본값");
        var menu = new SettingsMenu(defaults);
        check(menu.RequestClose(), "바꾼 게 없으면 확인 없이 닫음");
        menu.Select(SettingsRow.Music);
        menu.Adjust(1);
        check(menu.Current.Music == 9 && menu.IsDirty && defaults.Music == 8, "값은 사본에 미리 적용");
        for (int i = 0; i < 5; i++) menu.Adjust(1);
        check(menu.Current.Music == 10, "볼륨 상한 10");
        check(!menu.RequestClose() && menu.IsConfirming && menu.ConfirmYes, "바꾼 채 닫으면 확인, 처음 커서 예");
        menu.Adjust(1);
        check(menu.Current.Music == 10, "확인 중 값 고정");
        check(menu.Answer(false).Music == 8, "아니오면 원래 값");
        var shake = new SettingsMenu(new GameSettings());
        shake.Move(1); shake.Move(1); shake.Move(1);
        check(shake.Row == SettingsRow.CameraShake, "줄 이동");
        shake.Adjust(1);
        check(!shake.Current.CameraShake, "흔들림 켜기·끄기");
        shake.RequestClose();
        check(!shake.Answer(true).CameraShake, "예면 바꾼 값");

        var navigation = new SettingsMenu(defaults);
        check(navigation.Row == SettingsRow.Master, "설정 진입 시 마스터 먼저 선택");
        navigation.Adjust(-1);
        check(navigation.Current.Master == 9 && navigation.IsDirty, "선택된 마스터 좌우 조절");
        navigation.Move(1);
        check(navigation.Row == SettingsRow.Music, "마스터 다음 배경음 선택");
        navigation.Move(-1);
        navigation.Move(-1);
        check(navigation.Row == SettingsRow.Language, "설정 첫 행에서 위로 이동하면 언어 행 선택");
        navigation.Move(-1);
        check(navigation.Row == SettingsRow.KeyboardLayout, "언어 위에는 키보드 조작 행");
        navigation.Move(-1);
        check(navigation.Row == SettingsRow.CameraShake, "조작 방식 위에는 카메라 흔들림 행");
        navigation.Move(3);
        check(navigation.Row == SettingsRow.Master, "설정 마지막 행 뒤 마스터로 순환");
        var languageMenu = new SettingsMenu(defaults);
        languageMenu.Select(SettingsRow.Language); languageMenu.Adjust(1);
        check(languageMenu.Current.Language == LanguageCode.English && defaults.Language == LanguageCode.Korean, "언어 변경은 원본과 분리된 미리보기");
        check(!languageMenu.RequestClose() && languageMenu.Answer(false).Language == LanguageCode.Korean, "언어 선택 취소는 원래 값 복구");
        var languageCopy = languageMenu.Current.Clone();
        check(languageCopy.Language == LanguageCode.English && !languageCopy.SameAs(defaults), "언어 복사·변경 비교에 포함");
        var confirmedLanguage = new SettingsMenu(defaults);
        confirmedLanguage.Select(SettingsRow.Language); confirmedLanguage.Adjust(-1); confirmedLanguage.RequestClose();
        check(confirmedLanguage.Answer(true).Language == LanguageCode.English, "언어 선택 확인 결과 유지");
        bool invalidLanguage = false;
        try { languageCopy.SetLanguage((LanguageCode)99); } catch (ArgumentOutOfRangeException) { invalidLanguage = true; }
        check(invalidLanguage, "미지원 언어 선택 거부");
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
