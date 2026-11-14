using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

// 매달리기·보스 사슬·짐 투척 거리·불붙은 짚 코어 검사입니다.
internal static class MovementPatternChecks
{
    public static void Run(Player player, Action<bool, string> check)
    {
        AttackSpec[] Attacks() => new[]
        {
            new AttackSpec(120,80,250,200,0,8,12), new AttackSpec(120,80,250,200,0,8,12),
            new AttackSpec(120,80,250,200,0,8,12), new AttackSpec(200,100,400,0,10,16,12),
            new AttackSpec(100,80,200,0,-2,8,12)
        };
        var grounded = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        check(!grounded.TryHang() && !grounded.IsHanging, "지상에서는 매달리기 거부");
        var hanging = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        hanging.TryJump();
        check(hanging.TryHang() && hanging.CurrentState == StateKey.Hang, "공중에서 매달리기");
        int stamina = hanging.Stamina.Current;
        check(!hanging.TryAttack() && !hanging.TryRoll() && hanging.Stamina.Current == stamina, "매달린 중 공격·구르기 불가, 스태미나 소모 없음");
        hanging.SetMove(1);
        check(hanging.Locomotion.HorizontalSpeed == 0, "매달린 중 좌우 이동 없음");
        check(!hanging.TryJump(), "매달린 중 점프는 코어에서 거부");
        hanging.Locomotion.SetGrounded(true);
        check(hanging.EndHang() && hanging.CurrentState == StateKey.Walk, "올라가면 접지 상태로 복귀");
        check(!hanging.EndHang(), "매달리지 않으면 종료 거부");
        var dropped = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        dropped.TryJump(); dropped.TryHang();
        check(dropped.EndHang() && dropped.CurrentState == StateKey.Jump, "놓으면 공중 상태로 복귀");
        var struck = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        struck.TryJump(); struck.TryHang();
        struck.ReceiveHit(1);
        check(!struck.IsHanging, "매달린 채 맞으면 떨어짐");
        struck.Tick(player.HitStunTime);
        check(struck.CurrentState == StateKey.Jump, "피격 뒤 공중 상태");

        var common = new UnitStat(1006,0,"Boss",null,420,28,4,300,500,14,120,250,0);
        var phase = new BossState(4001,1006,1500,1500,2000,15,2000,"DAMAGE",1800,false,0,"Idle",10000);
        var boss = new BossPoise(new Boss(common,new[]{phase}),phase);
        var chain = new BossPattern(StateKey.Attack3, new[] { new AttackSpec(900,150,900,0,-6,0,0) }, 0, 0, 0, 800, 900, 3, 900);
        var combat = new BossCombat(boss, new[] { chain }, (min, max) => min);
        var target = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        combat.Face(-1);
        check(combat.TryStart(0) && boss.CurrentState == StateKey.Attack3 && combat.PullVelocity == 0, "사슬 예비 동작");
        combat.Tick(900);
        int before = target.Health;
        check(combat.TryHitPlayer(2001, target, false, false) && target.IsChained, "사슬 명중 시 구속");
        check(target.Health < before, "사슬 피해 적용");
        check(combat.PullVelocity == 900, "보스 쪽으로 끌기");
        check(target.TryAttack() && target.TryAttack() && target.IsChained && target.BindPressesRemaining == 1, "연타 2회는 유지");
        check(target.TryAttack() && !target.IsChained && combat.PullVelocity == 0, "연타 3회로 풀림");
        var held = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        var boss2 = new BossPoise(new Boss(common,new[]{phase}),phase);
        var combat2 = new BossCombat(boss2, new[] { chain }, (min, max) => min);
        combat2.TryStart(0); combat2.Tick(900);
        combat2.TryHitPlayer(2001, held, false, false);
        combat2.Release();
        check(!held.IsChained, "패턴 정리 시 사슬 해제");
        try { new BossPattern(StateKey.Attack3, new[] { new AttackSpec(1,1,1,0,0,0,0) }); check(false, "사슬 설정 누락 거부"); }
        catch (ArgumentOutOfRangeException) { check(true, "사슬 설정 누락 거부"); }

        var throwPattern = new MonsterPattern(StateKey.Attack2, 600, new AttackSpec(500,700,500,0,-4,0,0), 0, 0, 0, 0, 300, true);
        check(throwPattern.IsProjectile && throwPattern.MinRange == 300, "투척 최소 거리");
        try { new MonsterPattern(StateKey.Attack2, 200, new AttackSpec(1,1,1,0,0,0,0), 0, 0, 0, 0, 300); check(false, "최소 거리 범위 거부"); }
        catch (ArgumentOutOfRangeException) { check(true, "최소 거리 범위 거부"); }

        var skilled = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        check(!skilled.TryUseSkill(), "해금 전 결박 가르기 거부");
        skilled.ConfigureSkill(new AttackSpec(150,120,350,0,6,30,0), 8000, 200);
        int rollDistance = 0; skilled.SkillStarted += (d, distance, t) => rollDistance = distance;
        int staminaBefore = skilled.Stamina.Current;
        check(skilled.TryUseSkill() && skilled.CurrentState == StateKey.Skill1 && rollDistance == 200 && skilled.Stamina.Current == staminaBefore, "결박 가르기 시작·이동 요청·스태미나 없음");
        check(!skilled.TryUseSkill(), "쿨타임 중 재사용 거부");
        skilled.Tick(150);
        check(skilled.IsAttackActive, "선딜 150 뒤 판정");
        var skillBoss = new BossPoise(new Boss(common,new[]{phase}),phase);
        check(skilled.TryHitBoss(4001, skillBoss, true, false) && skillBoss.Poise == 90, "회피 입력을 무시하고 체세 30 피해");
        skilled.Tick(470);
        check(skilled.CurrentState == StateKey.Idle && skilled.SkillCooldownRemaining > 0, "스킬 끝난 뒤 복귀·쿨타임 유지");
        skilled.Tick(8000);
        check(skilled.SkillCooldownRemaining == 0, "쿨타임 8000 뒤 사용 가능");
        var chainCut = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        chainCut.ConfigureSkill(new AttackSpec(150,120,350,0,6,30,0), 8000, 200);
        var boss3 = new BossPoise(new Boss(common,new[]{phase}),phase);
        var combat3 = new BossCombat(boss3, new[] { chain }, (min, max) => min);
        combat3.TryStart(0); combat3.Tick(900);
        combat3.TryHitPlayer(2001, chainCut, false, false);
        check(chainCut.IsChained && chainCut.TryUseSkill() && !chainCut.IsChained && chainCut.CurrentState == StateKey.Skill1 && combat3.PullVelocity == 0, "결박 가르기로 사슬 끊기");
        var hangSkill = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        hangSkill.ConfigureSkill(new AttackSpec(150,120,350,0,6,30,0), 8000, 200);
        hangSkill.TryJump(); hangSkill.TryHang();
        check(!hangSkill.TryUseSkill(), "매달리기 중 결박 가르기 불가");

        var lander = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        check(!lander.NotifyLanded(player.LandFallHeight - 1) && lander.CurrentState == StateKey.Idle, "낮은 낙하는 착지 모션 없음");
        check(lander.NotifyLanded(player.LandFallHeight) && lander.CurrentState == StateKey.Land && lander.Health == player.HP, "5타일 낙하 착지 모션·피해 없음");
        check(lander.Locomotion.HorizontalSpeed == 0, "착지 중 이동 없음");
        lander.Tick(player.LandTime);
        check(lander.CurrentState == StateKey.Idle, "착지 시간 뒤 복귀");

        var faller = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        check(!faller.ApplyFall(10) && faller.Health == player.HP - 10, "구덩이 낙하 HP 10 감소");
        faller.ReceiveHit(player.HP - 20);
        faller.Tick(player.HitIFrame + player.HitStunTime);
        check(faller.Health == 10 && faller.ApplyFall(10) && faller.IsDead && faller.CurrentState == StateKey.Dead, "HP 10 이하 낙사");

        var carried = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        carried.ConfigureHerbs(40, 800, 3);
        carried.RestoreVitals(37, 1);
        check(carried.Health == 37 && carried.HerbCount == 1 && !carried.IsDead && carried.CurrentState == StateKey.Idle, "맵 이동 뒤 HP·약초 유지");
        bool rejected = false;
        try { carried.RestoreVitals(0, 1); } catch (System.ArgumentOutOfRangeException) { rejected = true; }
        check(rejected && carried.Health == 37, "HP 0은 이어받지 않음");

        var healer = new PlayerActor(player, Attacks(), true, player.StaminaMax);
        check(!healer.TryUseHerb(), "약초 미연결이면 거부");
        healer.ConfigureHerbs(40, 800, 3);
        check(!healer.TryUseHerb() && healer.HerbCount == 3, "체력이 가득하면 약초 쓰지 않음");
        healer.ReceiveHit(60);
        healer.Tick(player.HitIFrame + player.HitStunTime);
        int hurt = healer.Health;
        healer.SetMove(1);
        check(healer.TryUseHerb() && healer.CurrentState == StateKey.Heal && healer.HerbCount == 2, "약초 시작 시 개수 감소");
        check(healer.HealMoveSpeed == 0 && healer.HorizontalMoveSpeed == 0 && healer.Locomotion.HorizontalSpeed == 0, "약초 사용 중 수평 이동 차단");
        check(!healer.TryAttack() && !healer.TryRoll(), "사용 중 공격·구르기 불가");
        healer.Tick(799);
        check(healer.Health == hurt && healer.HorizontalMoveSpeed == 0, "799ms까지 회복·이동 없음");
        healer.Tick(1);
        check(healer.Health == System.Math.Min(player.HP, hurt + 40) && healer.CurrentState == StateKey.Walk && healer.HorizontalMoveSpeed == player.MoveSpeed, "800ms 뒤 회복·이동 복구");
        healer.ReceiveHit(30);
        healer.Tick(player.HitIFrame + player.HitStunTime);
        int beforeCancel = healer.Health;
        healer.TryUseHerb();
        healer.Tick(100);
        healer.ReceiveHit(1);
        healer.Tick(1000);
        check(healer.Health == beforeCancel - 1 && healer.HerbCount == 1, "맞으면 취소되고 개수는 줄어듦");
        healer.TryUseHerb(); healer.Tick(800);
        check(healer.HerbCount == 0, "개수 소진");
        healer.ReceiveHit(5);
        healer.Tick(player.HitIFrame + player.HitStunTime);
        check(!healer.TryUseHerb(), "약초 없으면 거부");

        var near = new[] { new InteractionCandidate(5f, 0f, true), new InteractionCandidate(1.5f, 0f, true), new InteractionCandidate(0.5f, 0f, false), new InteractionCandidate(1f, 3f, true) };
        check(InteractionSelector.Pick(1f, 0f, near, 1f, 2f) == 1, "범위 안 CanInteract 중 가장 가까운 대상");
        check(InteractionSelector.Pick(50f, 0f, near, 1f, 2f) == -1, "범위 밖이면 대상 없음");
        check(InteractionSelector.Pick(0.5f, 0f, new[] { near[2] }, 1f, 2f) == -1, "꺼진 대상은 고르지 않음");
        var box = new TextboxPlayback(new[] { "가나다", "라" }, 30);
        box.Tick(60);
        check(box.VisibleCharacters == 2 && !box.IsLineComplete, "글자당 30ms");
        box.Advance();
        check(box.VisibleCharacters == 3 && box.IsLineComplete && box.CurrentLine == "가나다", "출력 중 넘기면 그 줄 전체");
        box.Advance();
        check(box.CurrentLine == "라" && box.VisibleCharacters == 0, "다 나온 뒤 넘기면 다음 줄");
        box.Tick(30); box.Advance();
        check(box.IsFinished, "마지막 줄 뒤 넘기면 끝");
        var progress = new StageProgress();
        check(progress.Clear(8005) && !progress.Clear(8005) && progress.IsCleared(8005) && !progress.IsCleared(8001), "클리어 기록은 처음만 참");

        var tips = new[] { 30011, 30101, 30102, 30200 };
        check(LoadingTip.Pick(tips, 30101, (min, max) => min) == 30102, "툴팁은 번호대 안에서 직전 제외");
        check(LoadingTip.Pick(new[] { 30101 }, 30101, (min, max) => min) == 30101, "툴팁이 하나면 반복 허용");
        check(LoadingTip.Pick(new[] { 30011 }, 0, (min, max) => min) == 0, "번호대에 없으면 툴팁 없음");
        LoadingTip.Split("구르기: 구르는 동안: 무적", out string tipTitle, out string tipBody);
        check(tipTitle == "구르기:" && tipBody == " 구르는 동안: 무적", "첫 쌍점 앞만 제목");
        LoadingTip.Split("쌍점 없음", out tipTitle, out tipBody);
        check(tipTitle == "" && tipBody == "쌍점 없음", "쌍점이 없으면 전체 본문");
        check(LoadingTip.Progress(1f, 1f, 2f) == 0.5f, "빠른 로딩도 최소 2초에 걸쳐 진행");
        check(LoadingTip.Progress(0.25f, 5f, 2f) == 0.25f, "느린 로딩은 실제 진행을 따름");

        var fire = new GroundFire(3000, 5, 1000);
        check(fire.Tick(999) == 0 && fire.IsActive, "불 첫 주기 전 피해 없음");
        check(fire.Tick(1) == 1, "불 1초마다 피해");
        check(fire.Tick(2000) == 2 && !fire.IsActive, "불 3초 총 3회 뒤 종료");
        check(fire.Tick(1000) == 0, "꺼진 불 피해 없음");
    }
}
