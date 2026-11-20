using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class ContentAccessChecks
{
    public static int Run()
    {
        int checks = 0;
        int queries = 0;
        var cleared = new HashSet<int>();
        var access = new ContentAccess(id => { queries++; return cleared.Contains(id); });
        void Check(bool result)
        {
            if (!result) throw new Exception("콘텐츠 잠금 판단 검사 실패: " + (checks + 1));
            checks++;
        }
        Stage CreateStage(bool enabled, int condition = 1) =>
            new Stage(8002, 30012, 3, 8003, null, null, "", true, enabled, condition, 8005, 1);

        Check(access.Evaluate(CreateStage(false)) == ContentAccessResult.DisabledByPlanner && queries == 0);
        Check(access.Evaluate(CreateStage(true)) == ContentAccessResult.UnlockRequired);
        cleared.Add(8005);
        Check(access.Evaluate(CreateStage(true)) == ContentAccessResult.Allowed);
        Check(access.Evaluate(CreateStage(false)) == ContentAccessResult.DisabledByPlanner);
        cleared.Remove(8005);
        Check(access.Evaluate(CreateStage(true)) == ContentAccessResult.UnlockRequired);
        int before = queries;
        Check(access.Evaluate(CreateStage(true, 0)) == ContentAccessResult.Allowed && queries == before);
        var skill = new Skill(7001, 30010, 6002, 10009, 6, 30, 8000, "ROOM_LEDGER", "CUT_BIND", true, 1, 8005, 1);
        Check(access.Evaluate(skill) == ContentAccessResult.UnlockRequired);
        cleared.Add(8005);
        Check(access.Evaluate(skill) == ContentAccessResult.Allowed);
        Check(access.Evaluate(new Item(6002, 30008, "Weapon", "", 7001, "START", "", false)) == ContentAccessResult.DisabledByPlanner);
        Check(access.Evaluate(new Item(6002, 30008, "Weapon", "", 7001, "START", "", true)) == ContentAccessResult.Allowed);
        bool failed = false;
        try { access.Evaluate(CreateStage(true, 99)); }
        catch (NotSupportedException) { failed = true; }
        Check(failed);
        failed = false;
        try { access.Evaluate((Stage)null); }
        catch (ArgumentNullException) { failed = true; }
        Check(failed);

        // 몬스터: 기획자가 끈 정의는 클리어 조회 없이 거부하고, 켠 정의만 허용합니다(조건 규칙 감사 3.4).
        Monster enabledMonster = TestDefinitions.Monster(false);
        var disabledMonster = new Monster(new UnitStat(1002, 30002, "Monster", 1, 35, 13, 0, 300, 0, 10, 40, 300, 0), 3001, 1002,
            1000, 1500, 2000, 10, 1500, "KILL", 0, false, "MELEE", 800, 150, false);
        var noQuery = new ContentAccess(_ => throw new InvalidOperationException("몬스터 판정이 클리어 기록을 조회했습니다."));
        Check(noQuery.Evaluate(enabledMonster) == ContentAccessResult.Allowed);
        Check(noQuery.Evaluate(disabledMonster) == ContentAccessResult.DisabledByPlanner);
        return checks;
    }
}
