using HumanGhost.Data.Definitions;

// 계산식·상태 전이만을 위한 독립 예제입니다. 배포 수치의 정답으로 비교하지 않습니다.
internal static class TestDefinitions
{
    public static Player Player(int moveSpeed = 600, int jumpHeight = 300)
    {
        var unit = new UnitStat(1001, 30001, "Player", null, 100, 15, 0, 500, 1200, 6, 0, moveSpeed, 0);
        return new Player(unit, 2001, 1001, 100, 45, 500, 300, 500, 300, 20, jumpHeight, 300, 600, 500, 500, 200, false, 150, 6002);
    }

    public static Monster Monster(bool armored)
    {
        var unit = armored
            ? new UnitStat(1003, 30003, "Monster", 2, 40, 15, 2, 400, 0, 13, 50, 250, 0)
            : new UnitStat(1002, 30002, "Monster", 1, 35, 13, 0, 300, 0, 10, 40, 300, 0);
        return new Monster(unit, armored ? 3002 : 3001, unit.ID, 1000, 1500, 2000, 10, 1500,
            "KILL", 0, armored, armored ? "CHARGER" : "MELEE", 800, armored ? 600 : 150, true);
    }
}
