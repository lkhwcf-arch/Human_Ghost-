using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class MonsterAwarenessChecks
{
    // 공급 수치가 아닌 명시적 시험 정의입니다. 거리·높이는 tile_x100, 시간은 ms입니다.
    private static MonsterAggro Rules(int front = 500, int back = 100, int up = 100, int down = 0,
        int frontUp = 200, int backUp = 100, int frontDown = 100, int backDown = 100, int sameLevel = 10)
        => new MonsterAggro(1, 1, front, back, up, down, frontUp, backUp, frontDown, backDown,
            1100, 600, 2000, 1200, 1000, sameLevel);

    public static void Run(Action<bool, string> check)
    {
        var rules = Rules();
        bool Acquires(int x, int y, int facing = 1)
        {
            var observer = new MonsterAwareness(rules);
            observer.Observe(x, y, 0, true, facing); return observer.IsEngaged;
        }
        foreach (var item in new[] {
            (500, 10, true), (501, 0, false), (-100, 0, true), (-101, 0, false),
            (0, 100, true), (0, 101, false), (0, -11, false),
            (200, 200, true), (201, 200, false), (200, 201, false),
            (-100, 100, true), (-101, 100, false), (100, -100, true), (-100, -100, true),
            (50, 150, false), (51, 150, true), (50, -80, false), (51, -80, true) })
            check(Acquires(item.Item1, item.Item2) == item.Item3, "8방향·경계·0 방향 대체 금지: " + item);
        check(Acquires(-500, 0, -1) && !Acquires(101, 0, -1), "왼쪽을 보면 왼쪽이 정면·오른쪽이 후면");
        check(!Acquires(500, 11), "같은 높이 허용값 밖은 정면 대신 대각선 범위 사용");
        var adjusted = new MonsterAwareness(Rules(sameLevel: 20)); adjusted.Observe(500, 11, 0);
        check(adjusted.IsEngaged, "같은 높이 허용값 변경이 분류에 반영됨");
        adjusted = new MonsterAwareness(Rules(down: 150)); adjusted.Observe(0, -150, 0);
        check(adjusted.IsEngaged, "아래 범위를 공급하면 아래 방향 활성화");
        adjusted = new MonsterAwareness(Rules(0, 0, 0, 0, 0, 0, 0, 0)); adjusted.Observe(0, 0, 0);
        check(!adjusted.IsEngaged, "모든 방향 0이면 겹친 위치도 자동 인식하지 않음");
        adjusted.NotifyHit(); check(adjusted.IsEngaged, "피격은 방향 0 설정도 무시하고 인식");
        foreach (int sight in new[] {125, 950})
        {
            var driven = new MonsterAwareness(Rules(front: sight)); driven.Observe(sight + 1, 0, 0);
            check(!driven.IsEngaged, "공급한 정면 거리 밖은 차단: " + sight);
            driven.Observe(sight, rules.SameLevelHeight, 0);
            check(driven.IsEngaged, "공급한 정면 경계로 인식: " + sight);
        }
        foreach (var invalid in new[] {Rules(front: -1), Rules(front: 1101), Rules(up: 601),
            Rules(frontUp: 601), Rules(sameLevel: -1), Rules(sameLevel: 601)})
        {
            bool failed = false; try { new MonsterAwareness(invalid); } catch (ArgumentException) { failed = true; }
            check(failed, "음수·해제 범위보다 큰 인식·허용값 거부");
        }
        var awareness = new MonsterAwareness(rules); awareness.Observe(500, 0, 0);
        awareness.Observe(rules.LoseRange + 1, 0, 600);
        check(!awareness.Advance(rules.LoseTime - 1, false) && awareness.IsEngaged, "해제 시간 직전 유지");
        awareness.Observe(rules.LoseRange, 0, 600); awareness.Advance(1, false);
        awareness.Observe(rules.LoseRange + 1, 0, 600);
        check(!awareness.Advance(rules.LoseTime - 1, false), "복귀하면 해제 시간 초기화");
        check(awareness.Advance(1, false) && awareness.IsReturning && !awareness.IsEngaged, "해제 시간 도달 시 복귀");
        awareness.Observe(500, 0, 500); awareness.Advance(rules.ReacquireDelay - 1, false);
        check(!awareness.IsEngaged, "재인식 대기 직전 금지");
        awareness.Advance(1, false); check(awareness.IsEngaged && !awareness.IsReturning, "재인식 대기 후 방향 범위로 인식");
        awareness.Observe(500, 0, rules.LeashRange + 1);
        check(!awareness.Advance(1, true) && awareness.IsEngaged, "공격 보호 중 스폰 거리 해제 보류");
        check(awareness.Advance(0, false) && awareness.IsReturning, "보호 종료 뒤 스폰 거리 초과 복귀");
        awareness.NotifyHit(); check(awareness.IsEngaged, "피격은 재인식 대기 무시");
        check(awareness.LoseTarget() && !awareness.LoseTarget() && !awareness.IsEngaged, "사망 해제 반복 초기화 금지");
        awareness.NotifyHit(); check(!awareness.IsEngaged, "사망 대상을 피격으로 재인식하지 않음");
        awareness.Reset(); awareness.Observe(500, 0, 100); awareness.Observe(500, rules.LoseHeight + 1, 100);
        check(awareness.Advance(rules.LoseTime, false) && awareness.IsReturning, "해제 높이 초과도 시간 후 복귀");
        awareness.Reset(); awareness.Observe(50, 0, 500, false); awareness.NotifyHit(); awareness.Advance(10000, false);
        check(!awareness.IsEngaged, "유효하지 않은 대상은 피격·시간으로 인식하지 않음");

        var common = new UnitStat(1002, 0, "AwarenessFixture", null, 100, 15, 0, 0, 0, 0, 24, 200, 0);
        // 기존 SightRange=0이어도 주입한 8방향 표로 인식해야 합니다.
        var definition = new Monster(common, 1, 1002, 0, 0, 0, 0, 1000, "KILL", 0, false, "HUSUGWI", 0, 100, true);
        MonsterActor Enemy() => new MonsterActor(definition,
            new[] {new MonsterPattern(StateKey.Attack1, 100, new AttackSpec(100, 100, 100, 0, 0, 0, 0))},
            300, 500, (min, max) => min, keepHealth: false, aggro: rules);
        // 생성 뒤 첫 관측에서만 플레이어 쪽을 봅니다(704fde84 몬스터 스폰 방향). 이후 인식 전에는 돌지 않습니다.
        var enemy = Enemy(); enemy.Observe(2001, 101, -1, 0, 0, 0);
        check(enemy.HasAggro && enemy.Facing == -1, "생성 뒤 첫 관측은 플레이어 쪽을 보고 정면 거리로 인식");
        enemy = Enemy(); enemy.Observe(2001, 600, 1, 0, 0, 0); enemy.Observe(2001, 101, -1, 0, 0, 0);
        check(!enemy.HasAggro && enemy.Facing == 1, "인식 전에 돌아서 후면을 정면으로 바꾸지 않음");
        enemy.Observe(2001, 500, 1, 400, 0, 0);
        check(enemy.CurrentState == StateKey.Idle && enemy.Velocity == 0, "대각선 범위 밖은 대기");
        enemy.Observe(2001, 500, 1, 0, 600, -1);
        check(enemy.HasAggro && enemy.Velocity == 200, "Monster.SightRange 대신 8방향 표로 추적");
        enemy.ReceiveHit(4, poiseDamage: 5); enemy.Tick(300);
        enemy.Observe(2001, 1500, 1, 0, 600, -1); enemy.Tick(2000);
        check(enemy.IsReturning && enemy.Facing == -1 && enemy.Velocity == -200 && enemy.Health == 96 && enemy.Poise == 24,
            "복귀는 스폰 방향 걷기·HP 보존·체세 회복");
        enemy.Observe(2001, 1500, 1, 0, 0, 0);
        check(!enemy.IsReturning && enemy.Velocity == 0 && enemy.Facing == 1, "스폰 위치에서 대기·플레이어 쪽을 봄");
        foreach (int elapsed in new[] {0, 100, 199})
        {
            enemy = Enemy(); enemy.Observe(2001, 50, 1, 0, 500, -1); enemy.Tick(elapsed);
            enemy.Observe(2001, 50, 1, 0, 500, -1, false);
            check(!enemy.HasAggro && enemy.CurrentPattern == null && !enemy.IsAttackActive, "대상 비활성 중 공격 해제: " + elapsed);
        }
        enemy = Enemy(); enemy.Observe(2001, 50, 1, 0, 0, 0);
        enemy.Tick(499);
        check(enemy.HasAggro && enemy.CurrentPattern == null, "인식 후 500ms 전까지 공격하지 않음");
        enemy.Tick(1);
        check(enemy.CurrentPattern != null && enemy.AttackElapsed == 0, "첫 공격은 500ms 뒤 예비 동작부터 시작");

        enemy = Enemy(); enemy.Observe(2001, 50, 1, 0, 0, 0, targetOnReachableFloor: false);
        enemy.Tick(500);
        check(enemy.HasAggro && enemy.CurrentPattern == null, "같은 높이의 끊긴 발판에서는 추적하되 근접 공격하지 않음");
        enemy.Observe(2001, 50, 1, 51, 0, 0);
        check(enemy.CurrentPattern == null, "위층에서는 근접 공격하지 않음");
        enemy.Observe(2001, 50, 1, 50, 0, 0);
        check(enemy.CurrentPattern != null, "같은 층 허용 높이 경계에서는 공격함");

        enemy = Enemy();
        bool mayAttack = false;
        enemy.UseAttackAvailability(() => mayAttack);
        enemy.Observe(2001, 50, 1, 0, 0, 0);
        enemy.Tick(500);
        check(enemy.CurrentPattern == null && enemy.CurrentState == StateKey.Idle, "공격 자리가 없을 때 제자리 경계");
        mayAttack = true;
        enemy.Tick(249);
        check(enemy.CurrentPattern == null, "공격 자리 재확인 간격 유지");
        enemy.Tick(1);
        check(enemy.CurrentPattern != null, "공격 자리 확보 후 공격 재개");

        enemy = Enemy(); enemy.Observe(2001, 500, 1, 0, 600, -1); enemy.ReceiveHit(0, poiseDamage: 24);
        enemy.Observe(2001, 1500, 1, 700, 1300, -1); enemy.Tick(999);
        check(enemy.IsExposed && !enemy.IsReturning, "노출 중 복귀 보류");
        enemy.Tick(1); check(enemy.IsReturning && enemy.Poise == 24, "노출 종료 뒤 복귀");
    }
}
