using System;
using HumanGhost.Gameplay;

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
        check(StageExitRules.CanLeave(8001, 3, true, false) && !StageExitRules.CanLeave(8001, 0, false, true), "맵 1 출구는 명부 조사 기준");
        check(StageExitRules.CanLeave(8002, 0, false, false) && !StageExitRules.CanLeave(8002, 1, true, false), "다른 맵 출구는 남은 몬스터 기준");
        check(StageExitRules.CanLeave(8003, 4, false, true), "클리어한 맵은 잡몹이 다시 나와도 출구 유지");

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

        var defaults = new GameSettings();
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
