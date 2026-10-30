using System;
using System.Globalization;
using System.IO;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.States;

internal static class Program
{
    private static int _checks;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1) throw new ArgumentException("Unity 프로젝트 루트가 필요합니다.");
            Player definition = ReadPlayer(args[0]);
            CheckLocomotion(definition);
            Player changed = ReadPlayer(args[0], checked(definition.MoveSpeed + 1), checked(definition.JumpHeight + 1));
            var changedActor = new PlayerLocomotion(changed, true);
            changedActor.SetMove(-1);
            Check(changedActor.HorizontalSpeed == -changed.MoveSpeed, "데이터 속도 변경 반영");
            Check(changedActor.TryJump() && changedActor.JumpHeight == changed.JumpHeight, "데이터 점프 높이 변경 반영");
            CheckStateLifetime();
            CheckStamina(definition);
            _checks += CombatChecks.Run(definition);
            Console.WriteLine($"PASS: {_checks} checks; deployed Player {definition.ID}, Unit {definition.UnitID}");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
        _checks++;
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { _checks++; return; }
        throw new Exception("예상 예외 누락: " + typeof(T).Name);
    }

    private static void CheckLocomotion(Player definition)
    {
        var first = new PlayerLocomotion(definition, true);
        var second = new PlayerLocomotion(definition, true);
        int changes = 0;
        Action<StateKey> Observe = key => changes++;
        first.StateChanged += Observe;
        first.SetMove(1);
        Check(first.CurrentState == StateKey.Walk && first.HorizontalSpeed == definition.MoveSpeed, "오른쪽 이동 요청");
        first.SetMove(-1);
        Check(first.CurrentState == StateKey.Walk && first.HorizontalSpeed == -definition.MoveSpeed, "반대 방향 전환");
        Check(changes == 1, "동일 상태 반복 요청은 전환 이벤트를 중복 발행하지 않음");
        Check(second.CurrentState == StateKey.Idle && second.HorizontalSpeed == 0, "공유 정의와 개체 입력 독립");
        Expect<ArgumentOutOfRangeException>(() => first.SetMove(2));
        Check(first.Direction == -1 && first.HorizontalSpeed == -definition.MoveSpeed, "잘못된 입력은 기존 요청 보존");
        Check(first.TryJump() && !first.IsGrounded && first.CurrentState == StateKey.Jump, "지상 점프 승인");
        Check(first.JumpHeight == definition.JumpHeight, "배포된 점프 높이 사용");
        Check(!first.TryJump(), "공중 재점프 거부");
        first.SetMove(1);
        Check(first.CurrentState == StateKey.Jump && first.HorizontalSpeed == definition.MoveSpeed, "공중 이동 요청");
        first.SetGrounded(false);
        Check(!first.TryJump(), "공중 관측 반복으로 재점프 허용하지 않음");
        first.SetGrounded(true);
        Check(first.CurrentState == StateKey.Walk && first.IsGrounded, "착지 후 현재 이동 입력 복구");
        first.SetMove(0);
        Check(first.CurrentState == StateKey.Idle && first.HorizontalSpeed == 0, "이동 해제");
        first.SetGrounded(false);
        Check(first.CurrentState == StateKey.Jump && !first.TryJump(), "발판 이탈도 공중 상태");
        first.SetGrounded(true);
        Check(first.TryJump(), "새 착지 관측 후 점프 재승인");
        Check(second.IsGrounded && second.CurrentState == StateKey.Idle, "첫 개체 점프가 두 번째 개체에 영향 없음");
        first.StateChanged -= Observe;
        int before = changes;
        first.SetGrounded(true);
        Check(changes == before, "구독 해제");
        var airborne = new PlayerLocomotion(definition, false);
        Check(airborne.CurrentState == StateKey.Jump && !airborne.TryJump(), "공중 생성 시 지상으로 가정하지 않음");
        Expect<ArgumentNullException>(() => new PlayerLocomotion(null, true));
        Expect<ArgumentOutOfRangeException>(() => first.Tick(-1));
        Check(definition.MoveSpeed == second.Definition.MoveSpeed, "공유 정의 불변");
    }

    private static void CheckStateLifetime()
    {
        string order = "";
        int elapsed = 0;
        var idle = new IdleState(() => order += "I+", exit: () => order += "I-");
        var machine = new ActorStateMachine(idle);
        machine.Call(StateKey.Jump, () => order += "J+", ms => elapsed += ms, () => order += "J-");
        machine.Tick(16);
        machine.Tick(17);
        Check(elapsed == 33 && order == "I+I-J+", "개별 함수 진입·정수 Tick 전달");
        Check(!machine.Call(StateKey.Jump, () => order += "duplicate"), "현재 행동 중복 진입 방지");
        machine.Change(idle);
        machine.Tick(100);
        Check(order == "I+I-J+J-I+" && elapsed == 33, "이전 함수 종료 후 Tick 호출 제거");
        Expect<ArgumentOutOfRangeException>(() => machine.Call((StateKey)(-1)));
        Check(machine.Current == StateKey.Idle, "미등록 상태는 현재 상태를 바꾸지 않음");
        Expect<ArgumentNullException>(() => machine.Change(null));
        Expect<InvalidOperationException>(() => machine.Call(StateKey.Skill1, () => machine.Change(idle)));
        Check(machine.Change(idle), "수명 주기 예외 뒤 전환 잠금 해제");
        machine.Call(StateKey.Jump, tick: ms => machine.Change(idle));
        machine.Tick(0);
        Check(machine.Current == StateKey.Idle, "Tick에서 정상 전환 가능");
        Expect<ArgumentOutOfRangeException>(() => machine.Tick(-1));
    }

    private static void CheckStamina(Player definition)
    {
        int max = definition.StaminaMax, regen = definition.StaminaRegen, delay = definition.StaminaRegenDelay;
        Check(max > 0 && regen > 0 && delay > 0 && definition.RollStamina > 0 && definition.RollStamina <= max, "배포 스태미나 값 검사 전제");
        int Regen(long ms) => (int)Math.Min(max, regen * ms / 1000);

        var fresh = new PlayerStamina(definition, 0);
        fresh.Tick(1000);
        Check(fresh.Current == Regen(1000), "소모 이력 없는 초기 현재량은 딜레이 없이 회복");

        var bounded = new PlayerStamina(definition, max);
        int events = 0, last = -1;
        Action<int> Observe = value => { events++; last = value; };
        bounded.Changed += Observe;
        Check(bounded.TryConsumeRoll() && bounded.Current == max - definition.RollStamina, "구르기 소모");
        Check(events == 1 && last == bounded.Current, "소모 변경 이벤트");
        Check(bounded.TryConsume(bounded.Current) && bounded.Current == 0 && events == 2, "남은 전량 소모");
        bounded.Tick(delay - 1);
        bounded.Tick(1);
        Check(bounded.Current == 0 && events == 2, "딜레이 경계까지 회복 없음");
        bounded.Tick(999);
        Check(bounded.Current == Regen(999) && events == 3 && last == bounded.Current, "딜레이 이후 구간만 회복");

        var whole = new PlayerStamina(definition, max);
        whole.TryConsume(max);
        whole.Tick(delay + 999);
        Check(whole.Current == bounded.Current, "큰 Tick이 딜레이 구간을 제외");

        var split = new PlayerStamina(definition, max);
        split.TryConsume(max);
        int splitEvents = 0;
        split.Changed += value => splitEvents++;
        int remaining = delay + 1999;
        while (remaining > 0) { int step = Math.Min(7, remaining); split.Tick(step); remaining -= step; }
        var single = new PlayerStamina(definition, max);
        single.TryConsume(max);
        single.Tick(delay + 1999);
        Check(split.Current == single.Current && single.Current == Regen(1999), "분할 Tick 나머지 보존");
        Check(splitEvents == split.Current, "분할 Tick은 실제 증가 시에만 이벤트");

        single.Tick(int.MaxValue);
        Check(single.Current == max, "큰 Tick 최대치 제한·오버플로 없음");
        int atMax = events;
        bounded.Tick(int.MaxValue);
        int afterFull = events;
        bounded.Tick(1000);
        Check(bounded.Current == max && events == afterFull && afterFull == atMax + 1, "최대치에서 추가 이벤트 없음");

        var poor = new PlayerStamina(definition, max);
        poor.TryConsumeRoll();
        poor.Tick(delay);
        int poorEvents = 0;
        poor.Changed += value => poorEvents++;
        Check(!poor.TryConsume(max) && poor.Current == max - definition.RollStamina && poorEvents == 0, "부족 시 거부·현재량·이벤트 불변");
        poor.Tick(1000);
        Check(poor.Current == Math.Min(max, max - definition.RollStamina + regen), "부족 실패는 회복 딜레이를 재시작하지 않음");
        int afterRegen = poorEvents;
        Check(poor.TryConsume(0) && poorEvents == afterRegen, "비용 0은 변경·이벤트 없음");
        Expect<ArgumentOutOfRangeException>(() => poor.TryConsume(-1));
        Expect<ArgumentOutOfRangeException>(() => poor.Tick(-1));
        int before = poor.Current;
        poor.Tick(0);
        Check(poor.Current == before && poorEvents == afterRegen, "Tick 0·잘못된 입력은 변경 없음");

        var other = new PlayerStamina(definition, max);
        Check(other.Current == max && fresh.Current == Regen(1000), "공유 정의와 개체 자원 독립");
        bounded.Changed -= Observe;
        bounded.TryConsume(1);
        Check(events == afterFull, "구독 해제");

        Expect<ArgumentNullException>(() => new PlayerStamina(null, 0));
        Expect<ArgumentOutOfRangeException>(() => new PlayerStamina(definition, -1));
        Expect<ArgumentOutOfRangeException>(() => new PlayerStamina(definition, max + 1));
        Expect<ArgumentException>(() => new PlayerStamina(WithStamina(definition, -1, regen, delay, definition.RollStamina), 0));
        Expect<ArgumentException>(() => new PlayerStamina(WithStamina(definition, max, -1, delay, definition.RollStamina), 0));
        var noDelay = new PlayerStamina(WithStamina(definition, max, regen, 0, definition.RollStamina), max);
        noDelay.TryConsume(max);
        noDelay.Tick(1000);
        Check(noDelay.Current == Regen(1000), "딜레이 0 정의는 소모 직후 회복");
        Check(definition.StaminaMax == max, "공유 정의 불변");
    }

    // 검사 전용: 정의 검증 경계를 위해 스태미나 값만 바꾼 Player를 만듭니다.
    private static Player WithStamina(Player p, int max, int regen, int delay, int roll)
    {
        var common = new UnitStat(p.UnitID, 0, "Player", null, p.HP, p.ATK, p.DEF, p.Dodge, p.Crit,
            p.CritDamage, p.Poise, p.MoveSpeed, p.ArmorPen);
        return new Player(common, p.ID, p.UnitID, max, regen, delay, p.RollDistance, p.RollTime, p.RollIFrame,
            roll, p.JumpHeight, p.HitStunTime, p.HitIFrame, p.CritHitStunTime, p.LandFallHeight, p.LandTime,
            p.HangAuto, p.InputBuffer, p.StartWeapon);
    }

    // 검사 전용 조립입니다. 운영용 로더·조회 저장소가 아닙니다.
    private static Player ReadPlayer(string root, int? moveSpeed = null, int? jumpHeight = null)
    {
        var parser = new CsvParser();
        CsvDocument players = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/Player.csv")));
        CsvDocument units = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/UnitStat.csv")));
        CsvRow p = players.Rows[0];
        int I(CsvRow row, string column) => int.Parse(row.Get(column), CultureInfo.InvariantCulture);
        CsvRow u = null;
        foreach (CsvRow row in units.Rows) if (I(row, "ID") == I(p, "UnitID")) u = row;
        if (u == null) throw new Exception("Player.UnitID 참조 누락");
        var common = new UnitStat(I(u, "ID"), I(u, "TextID"), u.Get("Type"),
            u.Get("Tier").Length == 0 ? (int?)null : I(u, "Tier"),
            I(u, "HP"), I(u, "ATK"), I(u, "DEF"), I(u, "Dodge"), I(u, "Crit"),
            I(u, "CritDamage"), I(u, "Poise"), moveSpeed ?? I(u, "MoveSpeed"), I(u, "ArmorPen"));
        return new Player(common, I(p, "ID"), I(p, "UnitID"), I(p, "StaminaMax"), I(p, "StaminaRegen"),
            I(p, "StaminaRegenDelay"), I(p, "RollDistance"), I(p, "RollTime"), I(p, "RollIFrame"),
            I(p, "RollStamina"), jumpHeight ?? I(p, "JumpHeight"), I(p, "HitStunTime"), I(p, "HitIFrame"),
            I(p, "CritHitStunTime"), I(p, "LandFallHeight"), I(p, "LandTime"), p.Get("HangAuto") == "Y",
            I(p, "InputBuffer"), I(p, "StartWeapon"));
    }
}
