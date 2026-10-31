using System;
using System.Globalization;
using System.IO;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class MonsterChecks
{
    public static int Run(string root, Player player)
    {
        int checks = 0;
        void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
        void Expect<T>(Action action) where T : Exception
        { try { action(); } catch (T) { checks++; return; } throw new Exception("예외 누락 " + typeof(T).Name); }

        Monster husugwi = ReadMonster(root, 3001), jigegwi = ReadMonster(root, 3002);
        Check(husugwi.HP == 35 && husugwi.ATK == 20 && !husugwi.SuperArmor && husugwi.SightRange == 800 && husugwi.AttackRange == 150, "배포 허수귀 정의");
        Check(jigegwi.HP == 50 && jigegwi.DEF == 2 && jigegwi.SuperArmor && jigegwi.AttackRange == 600, "배포 지게귀 정의");
        // 설정 에셋 시작값과 같은 개발 값.
        MonsterPattern[] HusugwiPatterns() => new[]
        {
            new MonsterPattern(StateKey.Attack1, 150, new AttackSpec(400, 100, 400, 0, 0, 0, 0)),
            new MonsterPattern(StateKey.Skill1, 150, new AttackSpec(500, 100, 500, 0, 0, 0, 0), bindTime: 1000, bindPresses: 3)
        };
        MonsterPattern[] JigegwiPatterns() => new[]
        {
            new MonsterPattern(StateKey.Attack1, 150, new AttackSpec(500, 100, 500, 0, 0, 0, 0)),
            new MonsterPattern(StateKey.Skill1, 600, new AttackSpec(600, 1000, 400, 0, 5, 0, 0), chargeSpeed: 900, wallStun: 800)
        };
        bool pickLast = false;
        int lastMin = -1, lastMax = -1;
        int Random(int min, int max) { lastMin = min; lastMax = max; return pickLast ? max - 1 : min; }
        MonsterActor H() => new MonsterActor(husugwi, HusugwiPatterns(), 300, 500, Random);
        MonsterActor J() => new MonsterActor(jigegwi, JigegwiPatterns(), 300, 500, Random);
        AttackSpec[] PlayerAttacks() => new[]
        {
            new AttackSpec(120,80,250,200,0,10,12), new AttackSpec(120,80,250,200,0,10,12),
            new AttackSpec(200,100,400,0,10,20,12), new AttackSpec(100,80,200,0,-2,10,12)
        };
        PlayerActor P() => new PlayerActor(player, PlayerAttacks(), true, player.StaminaMax);

        // 배회·발견·공격 거리 경계
        var h = H();
        int patterns = 0;
        h.PatternStarted += p => patterns++;
        Check(h.CurrentState == StateKey.Idle && h.Health == 35 && h.Velocity == 0, "초기 대기·현재 HP");
        h.Observe(1, 801, 1);
        Check(h.CurrentState == StateKey.Idle, "시야 밖은 대기");
        h.Observe(1, 800, 1);
        Check(h.CurrentState == StateKey.Walk && h.Velocity == 300, "시야 경계 포함 추적");
        h.Observe(1, 151, -1);
        Check(h.Velocity == -300 && h.Facing == -1, "목표 방향 추적");
        h.ClearTarget();
        Check(h.CurrentState == StateKey.Idle && h.Velocity == 0, "목표 상실 시 대기");
        h.Observe(1, 150, -1);
        Check(h.CurrentState == StateKey.Attack1 && patterns == 1 && h.Velocity == 0, "공격 거리 경계 포함 패턴 시작");
        h.ClearTarget();
        Check(h.CurrentState == StateKey.Attack1, "패턴 중 목표 상실은 패턴 유지");
        h.Observe(1, 150, 1);
        Check(h.Facing == -1, "패턴 중 방향 고정");
        h.Tick(399);
        Check(!h.IsAttackActive, "선딜 경계 이전");
        h.Tick(1);
        Check(h.IsAttackActive, "판정 시작");
        var p1 = P();
        Check(h.TryHitPlayer(2001, p1, false, false) && p1.Health == 80, "CombatMath 피해 20");
        Check(!h.TryHitPlayer(2001, p1, false, false), "판정 창 대상 중복 방지");
        h.Tick(100);
        Check(!h.IsAttackActive && !h.TryRegisterTarget(5), "판정 종료");
        h.Tick(400);
        Check(h.CurrentState == StateKey.Idle && lastMin == 1000 && lastMax == 1501, "패턴 후 IdleMin~IdleMax 포함 대기");
        h.Tick(999);
        Check(h.CurrentState == StateKey.Idle && patterns == 1, "대기 중 패턴 없음");
        h.Tick(1);
        Check(h.CurrentState == StateKey.Attack1 && patterns == 2, "대기 종료 후 재공격");

        // 큰 Tick과 분할 Tick 동일
        var big = H(); var split = H();
        big.Observe(1, 100, 1); split.Observe(1, 100, 1);
        big.Tick(2350);
        for (int i = 0; i < 2350; i++) split.Tick(1);
        Check(big.CurrentState == split.CurrentState && big.IsAttackActive == split.IsAttackActive && big.CurrentState == StateKey.Attack1 && big.IsAttackActive,
            "큰 Tick·분할 Tick 결과 동일");

        // 개체 독립
        var a = H(); var b = H();
        a.Observe(1, 100, 1);
        Check(a.ReceiveHit(10) && a.Health == 25 && b.Health == 35 && b.CurrentState == StateKey.Idle, "같은 정의 개체별 HP·상태 독립");

        // 피격 경직·치명·사망
        Check(a.CurrentState == StateKey.Hit && !a.IsAttackActive, "피격이 패턴 중단");
        a.Tick(299);
        Check(a.CurrentState == StateKey.Hit, "피격 경직 유지");
        a.Tick(1);
        Check(a.CurrentState == StateKey.Attack1, "경직 종료 후 재판단");
        a.ReceiveHit(1, true);
        a.Tick(499);
        Check(a.CurrentState == StateKey.CritHit, "치명 피격 경직");
        Check(a.ReceiveHit(100) && a.IsDead && a.Health == 0 && a.CurrentState == StateKey.Dead, "HP0 사망");
        a.Observe(1, 100, 1); a.Tick(5000);
        Check(a.CurrentState == StateKey.Dead && !a.ReceiveHit(1) && !a.TryRegisterTarget(1), "사망 뒤 판단·피격·판정 거부");

        // 플레이어 → 일반몹 피해
        var target = H();
        var attacker = P();
        attacker.TryAttack(); attacker.Tick(120);
        Check(attacker.TryHitMonster(5, target, false, false) && target.Health == 23, "플레이어 평타 피해 12");
        Check(!attacker.TryHitMonster(5, target, false, false), "플레이어 창당 대상 중복 방지");
        Check(!attacker.TryHitMonster(6, target, true, false) && target.Health == 23, "회피 시 무피해");

        // 허수귀 잡기·연타 해제
        pickLast = true;
        var g = H(); var bound = P();
        g.Observe(1, 150, 1);
        Check(g.CurrentState == StateKey.Skill1, "잡기 패턴 선택");
        g.Tick(500);
        var rolling = P(); rolling.TryRoll();
        Check(!g.TryHitPlayer(3, rolling, false, false) && rolling.CurrentState == StateKey.Roll, "구르기 무적은 잡기 회피");
        int stamina = bound.Stamina.Current;
        Check(g.TryHitPlayer(2001, bound, false, false) && bound.CurrentState == StateKey.Bound && bound.BoundBy == g && bound.Health == 100,
            "잡기는 피해 없이 구속");
        Check(!g.IsAttackActive, "잡기 성공 후 판정 종료");
        bound.SetMove(1);
        Check(bound.Locomotion.HorizontalSpeed == 0 && !bound.TryJump() && !bound.TryRoll(), "구속 중 이동·점프·구르기 잠금");
        g.Tick(5000);
        Check(g.CurrentState == StateKey.Skill1, "구속 유지 동안 몬스터 패턴 정지");
        Check(bound.TryAttack() && bound.TryAttack() && bound.CurrentState == StateKey.Bound && bound.BindPressesRemaining == 1, "연타 누적");
        Check(bound.TryAttack() && bound.CurrentState == StateKey.Walk && bound.BoundBy == null && bound.Stamina.Current == stamina,
            "연타 해제·스태미나 소모 없음");
        g.Tick(499);
        Check(g.CurrentState == StateKey.Skill1, "해제 뒤 후딜");
        g.Tick(1);
        Check(g.CurrentState == StateKey.Idle, "후딜 후 대기");

        var timed = H(); var timedPlayer = P();
        timed.Observe(1, 150, 1); timed.Tick(500); timed.TryHitPlayer(2001, timedPlayer, false, false);
        timedPlayer.Tick(999);
        Check(timedPlayer.CurrentState == StateKey.Bound, "구속 시간 경계 이전");
        timedPlayer.Tick(1);
        Check(timedPlayer.CurrentState == StateKey.Idle && timedPlayer.BoundBy == null, "구속 시간 만료 해제");

        var killed = H(); var held = P();
        killed.Observe(1, 150, 1); killed.Tick(500); killed.TryHitPlayer(2001, held, false, false);
        killed.ReceiveHit(35);
        Check(killed.IsDead && held.CurrentState == StateKey.Idle && held.BoundBy == null, "잡은 몬스터 사망 시 구속 해제");

        var released = H(); var heldByReleased = P();
        released.Observe(1, 150, 1); released.Tick(500); released.TryHitPlayer(2001, heldByReleased, false, false);
        released.Release();
        Check(heldByReleased.BoundBy == null && released.CurrentState == StateKey.Idle && !released.IsAttackActive, "비활성화 시 구속·판정 정리");
        released.Tick(5000);
        Check(released.CurrentState == StateKey.Idle, "비활성화 뒤 목표 없음 유지");

        var other = H(); var hitWhileBound = P();
        other.Observe(1, 150, 1); other.Tick(500); other.TryHitPlayer(2001, hitWhileBound, false, false);
        Check(!H().TryHitPlayer(2001, hitWhileBound, false, false), "비판정 몬스터는 대상 등록 없음");
        hitWhileBound.ReceiveHit(5);
        Check(hitWhileBound.CurrentState == StateKey.Hit && hitWhileBound.BoundBy == null, "구속 중 피격은 구속 해제");
        Check(!hitWhileBound.ReleaseBind(other), "이미 풀린 구속 재해제 거부");
        other.Tick(500);
        Check(other.CurrentState == StateKey.Idle, "구속이 끊기면 몬스터 후딜 진행");

        // 잡힌 목표 상실·플레이어 비활성화: 플레이어 Tick 없이 양쪽 정리
        var lost = H(); var lostPlayer = P();
        lost.Observe(1, 150, 1); lost.Tick(500); lost.TryHitPlayer(2001, lostPlayer, false, false);
        lost.ClearTarget();
        Check(lostPlayer.BoundBy == null && lostPlayer.CurrentState == StateKey.Idle, "잡은 목표 상실 시 플레이어 Tick 없이 구속 해제");
        lost.ClearTarget();
        lost.Tick(500);
        Check(lost.CurrentState == StateKey.Idle && lost.CurrentPattern == null, "목표 상실 후 잡기 후딜 진행·중복 정리 안전");

        var holder = H(); var disabled = P();
        holder.Observe(1, 150, 1); holder.Tick(500); holder.TryHitPlayer(2001, disabled, false, false);
        Check(disabled.ReleaseBind(disabled.BoundBy) && disabled.BoundBy == null && !disabled.ReleaseBind(holder), "플레이어 쪽 구속 정리(비활성화 경로)·재정리 거부");
        holder.Tick(500);
        Check(holder.CurrentState == StateKey.Idle && holder.CurrentPattern == null, "플레이어 정리 후 몬스터 후딜 종료·보유 해제");
        Check(disabled.CurrentState != StateKey.Bound, "재활성 시 이전 구속 없음");

        // BindTime 0 = 연타로만 해제
        var mashOnly = new MonsterActor(husugwi, new[] { new MonsterPattern(StateKey.Skill1, 150, new AttackSpec(500, 100, 500, 0, 0, 0, 0), bindTime: 0, bindPresses: 3) },
            300, 500, Random);
        var mashPlayer = P();
        mashOnly.Observe(1, 150, 1); mashOnly.Tick(500);
        Check(mashOnly.CurrentPattern.IsGrab && mashOnly.TryHitPlayer(2001, mashPlayer, false, false), "구속 상한0 잡기");
        mashPlayer.Tick(10000);
        Check(mashPlayer.CurrentState == StateKey.Bound, "구속 상한0은 시간 만료 해제 없음");
        mashPlayer.TryAttack(); mashPlayer.TryAttack(); mashPlayer.TryAttack();
        Check(mashPlayer.BoundBy == null && mashPlayer.CurrentState == StateKey.Idle, "구속 상한0은 연타로 해제");

        // 지게귀 돌진·슈퍼아머·벽 충돌 허점
        pickLast = false;
        var j = J(); var victim = P();
        j.Observe(1, 600, 1);
        Check(j.CurrentState == StateKey.Skill1 && !j.IsSuperArmor && j.Velocity == 0, "AttackRange 안 원거리는 돌진만 선택·선딜 정지");
        j.Tick(600);
        Check(j.IsCharging && j.IsSuperArmor && j.Velocity == 900, "돌진 판정 중 이동·슈퍼아머");
        Check(j.ReceiveHit(5) && j.Health == 45 && j.CurrentState == StateKey.Skill1 && j.IsCharging, "슈퍼아머는 HP만 깎이고 Hit 없음");
        Check(j.TryHitPlayer(2001, victim, false, false) && victim.Health == 69, "돌진 피해 26+5");
        Check(!j.TryHitPlayer(2001, victim, false, false), "돌진 창 중복 피해 방지");
        Check(j.NotifyWallHit() && j.CurrentState == StateKey.Stun && j.Velocity == 0 && !j.IsSuperArmor, "벽 충돌 허점·슈퍼아머 해제");
        Check(j.ReceiveHit(5) && j.CurrentState == StateKey.Stun, "허점 중 피해는 허점 유지");
        j.Tick(799);
        Check(j.CurrentState == StateKey.Stun, "허점 경계 이전");
        j.Tick(1);
        Check(j.CurrentState == StateKey.Idle && !j.NotifyWallHit(), "허점 종료 후 대기·돌진 외 벽 충돌 무시");

        var miss = J();
        miss.Observe(1, 400, 1); miss.Tick(600 + 1000);
        Check(miss.CurrentState == StateKey.Skill1 && !miss.IsCharging && miss.Velocity == 0, "벽 없이 돌진 종료 후 후딜");
        miss.Tick(400);
        Check(miss.CurrentState == StateKey.Idle, "돌진 후딜 종료");

        var near = J();
        near.Observe(1, 100, 1);
        Check(near.CurrentState == StateKey.Attack1 && !near.IsSuperArmor, "근접 거리 내려찍기");
        near.ReceiveHit(1);
        Check(near.CurrentState == StateKey.Hit, "내려찍기 중 슈퍼아머 없음");

        var charging = J();
        charging.Observe(1, 600, 1); charging.Tick(600); charging.Release();
        Check(!charging.IsSuperArmor && charging.Velocity == 0, "비활성화 시 돌진·슈퍼아머 해제");

        // 입력·설정 경계
        Expect<ArgumentOutOfRangeException>(() => H().Observe(0, 1, 1));
        Expect<ArgumentOutOfRangeException>(() => H().Observe(1, -1, 1));
        Expect<ArgumentOutOfRangeException>(() => H().Observe(1, 1, 2));
        Expect<ArgumentOutOfRangeException>(() => H().Tick(-1));
        Expect<ArgumentException>(() => new MonsterActor(husugwi, new MonsterPattern[0], 300, 500, Random));
        Expect<ArgumentNullException>(() => new MonsterActor(husugwi, HusugwiPatterns(), 300, 500, null));
        Expect<ArgumentOutOfRangeException>(() => new MonsterPattern(StateKey.Skill1, 150, new AttackSpec(1, 1, 0, 0, 0, 0, 0), bindTime: 1000));
        Expect<ArgumentOutOfRangeException>(() => new MonsterPattern(StateKey.Attack3, 150, new AttackSpec(1, 1, 0, 0, 0, 0, 0)));
        Expect<InvalidOperationException>(() => new MonsterActor(husugwi, HusugwiPatterns(), 300, 500, (min, max) => max).Observe(1, 100, 1));
        return checks;
    }

    private static Monster ReadMonster(string root, int id)
    {
        var parser = new CsvParser();
        CsvDocument monsters = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/Monster.csv")));
        CsvDocument units = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/UnitStat.csv")));
        int I(CsvRow row, string column) => int.Parse(row.Get(column), CultureInfo.InvariantCulture);
        CsvRow m = null, u = null;
        foreach (CsvRow row in monsters.Rows) if (I(row, "ID") == id) m = row;
        if (m == null) throw new Exception("Monster " + id + " 누락");
        foreach (CsvRow row in units.Rows) if (I(row, "ID") == I(m, "UnitID")) u = row;
        if (u == null) throw new Exception("Monster.UnitID 참조 누락");
        var common = new UnitStat(I(u, "ID"), I(u, "TextID"), u.Get("Type"), u.Get("Tier").Length == 0 ? (int?)null : I(u, "Tier"),
            I(u, "HP"), I(u, "ATK"), I(u, "DEF"), I(u, "Dodge"), I(u, "Crit"), I(u, "CritDamage"), I(u, "Poise"), I(u, "MoveSpeed"), I(u, "ArmorPen"));
        return new Monster(common, I(m, "ID"), I(m, "UnitID"), I(m, "IdleMin"), I(m, "IdleMax"), I(m, "PoiseRegenDelay"), I(m, "PoiseRegen"),
            I(m, "ExposeTime"), m.Get("ExecuteType"), I(m, "ExecuteDamage"), m.Get("SuperArmor") == "Y", m.Get("AIType"), I(m, "SightRange"), I(m, "AttackRange"));
    }
}
