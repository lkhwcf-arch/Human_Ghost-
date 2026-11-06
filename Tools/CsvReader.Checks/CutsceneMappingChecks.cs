using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Adapters;
using Step = HumanGhost.Data.Definitions.CutsceneStep;

internal static class CutsceneMappingChecks
{
    public static void Run(Action<bool> check)
    {
        var catalog = new DataCatalog();
        catalog.Register(new DataTable<Cutscene>(new[] { new Cutscene(18001, "Intro", 2000) }, row => row.ID));
        catalog.Register(new DataTable<Step>(new[] {
            new Step(19002, 18001, 2, "Image", "Cutscene/Ending/01", 3000, 30025, false),
            new Step(19001, 18001, 1, "Video", "Cutscene/Intro/Main", 0, null, false),
            new Step(19003, 18002, 1, "Image", "Cutscene/Other/01", 4000, null, true)
        }, row => row.ID));
        var sequence = CutsceneSequenceSource.Create(catalog, 18001);
        check(CutsceneSequenceSource.FindId(catalog, CutsceneSequenceSource.IntroKey) == 18001);
        check(CutsceneSequenceSource.FindId(catalog, CutsceneSequenceSource.LedgerKey) == 0
            && CutsceneSequenceSource.FindId(catalog, CutsceneSequenceSource.EndingKey) == 0);
        check(sequence.Key == "Intro" && sequence.SkipHoldMs == 2000 && sequence.Steps.Count == 2);
        check(sequence.Steps[0].ResourceAddress == "Cutscene/Intro/Main" && sequence.Steps[0].TextId == 0);
        check(sequence.Steps[1].TextId == 30025 && sequence.Steps[1].DurationMs == 3000 && !sequence.Steps[1].CanAdvance);
    }
}
