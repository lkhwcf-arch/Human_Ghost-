using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class DialogueChecks
{
    public static void Run(Action<bool, string> check)
    {
        var original = new[] { new DialogueLine(100, 200), new DialogueLine(101, 201) };
        foreach (string mode in new[] { "ALL", "LAST_LINE", "ONCE" })
        {
            var definition = new Dialogue(1, "TALK", 0, mode, true, 0, "1.json");
            var session = new DialogueSession(new[] { definition }, id => original);
            check(session.TryBegin(1, out var lines) && lines.Count == 2, "첫 대화 전체 재생");
            check(!session.TryBegin(1, out _), "실행 중 중복 대화 차단");
            check(!session.End(false) && session.TryBegin(1, out lines) && lines.Count == 2, "중단은 정상 완료로 기록하지 않음");
            check(session.End(true), "최초 정상 완료 기록");
            bool repeat = session.TryBegin(1, out lines);
            check(mode == "ONCE" ? !repeat : repeat && lines.Count == (mode == "LAST_LINE" ? 1 : 2), "표의 반복 규칙 적용");
            session.End(true);
            check(original.Length == 2 && original[0].TextId == 200, "원본 대사 줄 보존");
        }
        var playback = new TextboxPlayback(original, id => id.ToString(), 1);
        check(playback.CurrentSpeakerTextId == 100 && playback.CurrentTextId == 200, "첫 화자·대사 ID 연결");
        playback.SkipLine();
        check(playback.CurrentSpeakerTextId == 101 && playback.CurrentTextId == 201, "Enter 한 번에 다음 줄·화자 연결");
        playback.SkipLine(); check(playback.IsFinished, "마지막 줄 스킵 뒤 완료");
        var speed = new GameSpeedState();
        var first = new object(); var second = new object();
        speed.SetPauseOwner(first, true); speed.SetPauseOwner(second, true); speed.SetPauseOwner(first, false);
        check(speed.IsPaused, "대화 정지 해제가 다른 소유자의 정지를 풀지 않음");
        speed.SetPauseOwner(second, false); check(!speed.IsPaused, "마지막 정지 소유자 해제 뒤 진행 재개");
    }
}
