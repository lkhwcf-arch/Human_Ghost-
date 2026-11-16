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
        RunRequests(check);
        RunLedger(check);
    }

    private static void RunRequests(Action<bool, string> check)
    {
        var requests = new DialogueRequests();
        var ledger = new object(); var npc = new object();
        int first = requests.Begin(ledger, true);
        check(!requests.TryTake(ledger, first, out _), "재생 중인 요청은 결과 없음");
        check(requests.TryTake(npc, first, out var other) && other == DialogueEndReason.Cancelled, "다른 소유자는 남의 요청 결과를 진행으로 받지 않음");
        requests.End(DialogueEndReason.Completed);
        check(requests.TryTake(ledger, first, out var reason) && reason == DialogueEndReason.Completed, "요청별 정상 완료 결과");
        check(requests.TryTake(ledger, first, out reason) && reason == DialogueEndReason.Cancelled, "결과는 한 번만 가져감");
        int talk = requests.Begin(npc, false); requests.End(DialogueEndReason.Completed);
        check(talk != first && requests.TryTake(npc, talk, out reason) && reason == DialogueEndReason.Cancelled, "결과를 요청하지 않은 대화는 쌓지 않음");
        int cancelled = requests.Begin(ledger, true); requests.End(DialogueEndReason.Cancelled);
        check(requests.TryTake(ledger, cancelled, out reason) && reason == DialogueEndReason.Cancelled, "중간 취소 결과");
        int stale = requests.Begin(ledger, true); requests.End(DialogueEndReason.Completed);
        int next = requests.Begin(ledger, true);
        check(!requests.TryTake(ledger, next, out _), "새 요청은 이전 결과와 섞이지 않음");
        requests.Clear();
        check(requests.TryTake(ledger, stale, out reason) && reason == DialogueEndReason.Cancelled, "실행기 정리 뒤 늦은 결과는 취소");
        bool rejected = false;
        requests.Begin(ledger, true);
        try { requests.Begin(npc, false); } catch (InvalidOperationException) { rejected = true; }
        check(rejected, "동시에 두 요청을 시작하지 않음");
    }

    private static void RunLedger(Action<bool, string> check)
    {
        const int stage = 8005;
        Dialogue Row(int id, string trigger, int target) => new Dialogue(id, trigger, target, "ONCE", true, 0, id + ".json");
        Skill Unlock(int id, string type, int target, int condition = 1)
            => new Skill(id, 1, 1, 1, 1, 1, 1, type, "CUT_BIND", true, condition, target, 1);
        var dialogues = new[] { Row(24001, "TALK", 0), Row(24005, LedgerInvestigationDefinition.TriggerType, stage) };
        var skills = new[] { Unlock(7000, "NONE", 0, 0), Unlock(7001, LedgerInvestigationDefinition.SkillUnlockType, stage) };
        var definition = LedgerInvestigationDefinition.Resolve(stage, dialogues, skills);
        check(definition.DialogueId == 24005 && definition.Skill.ID == 7001, "조사 종류·Stage로 대화와 해금 Skill 결정");
        bool Fails(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
        check(Fails(() => LedgerInvestigationDefinition.Resolve(stage, new[] { dialogues[0] }, skills)), "명부 대화 누락을 허용하지 않음");
        check(Fails(() => LedgerInvestigationDefinition.Resolve(stage, new[] { dialogues[1], Row(24006, LedgerInvestigationDefinition.TriggerType, stage) }, skills)), "명부 대화 중복을 허용하지 않음");
        check(Fails(() => LedgerInvestigationDefinition.Resolve(stage, dialogues, new[] { skills[0] })), "해금 Skill 누락을 허용하지 않음");
        check(Fails(() => LedgerInvestigationDefinition.Resolve(stage, dialogues, new[] { Unlock(7001, LedgerInvestigationDefinition.SkillUnlockType, stage, 0) })), "클리어 조건이 아닌 해금 Skill을 허용하지 않음");

        var progress = new StageProgress();
        var investigation = new LedgerInvestigation(definition, progress);
        int unlocked = 0;
        Func<Skill, bool> enable = skill => { unlocked++; return true; };
        investigation.Wait(1);
        check(investigation.Finish(1, DialogueEndReason.Cancelled, enable) == LedgerOutcome.Ignored && !progress.IsCleared(stage) && unlocked == 0, "취소는 조사·해금하지 않음");
        check(!investigation.IsWaiting, "취소 뒤 다시 조사 가능");
        investigation.Wait(2);
        check(investigation.Finish(1, DialogueEndReason.Completed, enable) == LedgerOutcome.Ignored && investigation.IsWaiting, "오래된 요청의 늦은 결과는 폐기");
        check(investigation.Finish(2, DialogueEndReason.Failed, enable) == LedgerOutcome.Ignored && !progress.IsCleared(stage), "실패는 조사하지 않음");
        investigation.Wait(3);
        check(investigation.Finish(3, DialogueEndReason.Completed, enable) == LedgerOutcome.Unlocked && progress.IsCleared(stage) && unlocked == 1, "정상 완료 뒤 조사 완료·해금");
        check(investigation.IsInvestigated, "완료 뒤 재조사 상태");
        investigation.Wait(4);
        check(investigation.Finish(4, DialogueEndReason.Completed, enable) == LedgerOutcome.AlreadyInvestigated && unlocked == 1, "중복 적용하지 않음");
        var disabled = new LedgerInvestigation(definition, new StageProgress());
        disabled.Wait(5);
        check(disabled.Finish(5, DialogueEndReason.Completed, skill => false) == LedgerOutcome.Investigated, "기획 설정으로 해금이 꺼져도 조사 완료는 기록");
        var broken = new StageProgress();
        var failing = new LedgerInvestigation(definition, broken);
        failing.Wait(6);
        try { failing.Finish(6, DialogueEndReason.Completed, skill => throw new InvalidOperationException("검사")); } catch (InvalidOperationException) { }
        check(broken.IsCleared(stage) && !failing.IsWaiting, "해금 예외에도 진행은 남아 다음 맵 준비에서 다시 해금");
    }
}
