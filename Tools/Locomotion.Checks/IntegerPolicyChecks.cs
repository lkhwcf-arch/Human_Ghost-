using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HumanGhost.Gameplay;

internal static class IntegerPolicyChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var clock = new MillisecondClock();
        check(clock.Advance(.0005) == 0 && clock.Advance(.0005) == 1, "프레임마다 반올림하지 않고 0.5ms 나머지 보존");
        int accumulated = 0;
        for (int i = 0; i < 60; i++) accumulated += clock.Advance(1d / 60);
        check(accumulated >= 999 && accumulated <= 1000, "60프레임 경과를 1020ms로 늘리지 않음");
        check(MillisecondClock.ToMilliseconds(.5) == 500 && MillisecondClock.ToMilliseconds(.0005) == 1,
            "외부 초 값의 단일 변환과 중간값 반올림");
        var signed = new MillisecondClock();
        check(signed.AdvanceSigned(.0005) == 0 && signed.AdvanceSigned(-.0005) == 0 && signed.AdvanceSigned(.001) == 1,
            "역방향 애니메이션에서도 양·음의 나머지를 보존");
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1d, int.MaxValue / 1000d + 1 })
        {
            bool rejected = false;
            try { MillisecondClock.ToMilliseconds(invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "외부 시간의 비정상 값·정수 범위 초과 거부");
        }
        var candidates = new[] { new InteractionCandidate(100, 200, true), new InteractionCandidate(-100, 0, true) };
        check(InteractionSelector.Pick(0, 0, candidates, 100, 200) == 0, "상호작용 범위 끝점 포함·동률은 등록 순서 보존");
        check(InteractionSelector.Pick(0, 0, new[] { new InteractionCandidate(101, 0, true) }, 100, 200) == -1,
            "정수 좌표 한 단위 밖은 상호작용 범위에서 제외");
        check(InteractionSelector.Pick(long.MinValue, 0, new[] { new InteractionCandidate(long.MaxValue, 0, true) }, long.MaxValue, 0) == -1,
            "극단 좌표 차이의 오버플로로 가까운 대상으로 오인하지 않음");
        bool badRange = false;
        try { InteractionSelector.Pick(0, 0, candidates, -1, 0); } catch (ArgumentOutOfRangeException) { badRange = true; }
        check(badRange, "음수 상호작용 범위 거부");
        check(IntegerDistance.TryWithinRadius(new IntegerPosition(3, 4), new IntegerPosition(0, 0), 5, out _), "원형 거리 끝점 3·4·5 포함");
        check(!IntegerDistance.TryWithinRadius(new IntegerPosition(3, 4), new IntegerPosition(0, 0), 4, out _), "축 범위 안이어도 원형 거리 밖은 제외");
        check(!IntegerDistance.TryWithinRadius(new IntegerPosition(long.MaxValue, long.MaxValue), new IntegerPosition(0, 0), long.MaxValue, out _),
            "큰 제곱 거리의 합도 오버플로 없이 비교");
        var random = new Random(17);
        for (int i = 0; i < 100; i++)
        {
            long x = random.NextInt64(), y = random.NextInt64(), radius = random.NextInt64();
            var squared = (System.Numerics.BigInteger)x * x + (System.Numerics.BigInteger)y * y;
            bool expected = squared <= (System.Numerics.BigInteger)radius * radius;
            check(IntegerDistance.TryWithinRadius(new IntegerPosition(x, y), new IntegerPosition(0, 0), radius, out _) == expected,
                "정수 원형 거리와 독립 큰 정수 기준 대조 " + i);
        }
        var speed = new GameSpeedState();
        foreach (int rate in new[] { 0, -1, 10001 })
        {
            bool rejected = false;
            try { speed.Critical(0, 1, rate); } catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "게임 속도 정수 비율 범위 검사");
        }
        speed.Critical(0, 2, 1); speed.Tick(1);
        check(speed.Scale == 1, "게임 속도 최소 정수 비율 1/10000 보존");
        speed.Tick(1); check(speed.Scale == 10000, "정수 슬로 종료 뒤 정상 비율 복귀");
        check(LoadingTip.Progress(10000, int.MaxValue, 1) == 10000, "큰 경과 시간의 로딩 비율 곱셈 오버플로 방지");
        check(LoadingTip.Progress(10000, 1999, 2000) == 9995 && LoadingTip.Progress(10000, 2000, 2000) == 10000,
            "로딩 최소 시간의 직전·완료 경계");
        foreach (string source in Directory.EnumerateFiles(Path.Combine(root, "Assets/3.Script/Core"), "*.cs", SearchOption.AllDirectories))
        {
            string code = File.ReadAllText(source);
            code = Regex.Replace(code, @"/\*.*?\*/|//[^\r\n]*|@?""(?:""""|\\.|[^""\\])*""", "", RegexOptions.Singleline);
            check(!Regex.IsMatch(code, @"\b(float|double|decimal)\b"), "코어 정수 계약 유지: " + Path.GetFileName(source));
        }
        var camera = File.ReadAllText(Path.Combine(root, "Assets/3.Script/Preview/PreviewFollowCamera.cs"));
        check(camera.Contains("int _lookDelayMilliseconds = 500") && camera.Contains("int _lookDistanceUnits = 300") && camera.Contains("int _lookDownDistanceUnits = 600"),
            "카메라 정수 설정 500ms·위3타일·아래6타일");
        foreach (string scene in new[] { "GameplayPreview", "MapDohwagolTest" })
        {
            string serialized = File.ReadAllText(Path.Combine(root, "Assets/1.Scene", scene + ".unity"));
            check(serialized.Contains("_jumpMilliseconds: 800") && serialized.Contains("_executionRangeUnits: 175") &&
                serialized.Contains("_bossRangeUnits: 250"), "기존 씬의 초·타일 설정값 보존: " + scene);
        }
        foreach (string asset in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".unity", StringComparison.Ordinal) || path.EndsWith(".prefab", StringComparison.Ordinal)))
        {
            string serialized = File.ReadAllText(asset);
            check(!Regex.IsMatch(serialized, @"[^\r\n]--- !u!"), "직렬화 문서 경계 보존: " + Path.GetFileName(asset));
        }
    }
}
