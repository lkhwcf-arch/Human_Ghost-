using System;
using System.IO;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Mapping;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;

internal static class ErrorPopupDataChecks
{
    public static void Run(Action<bool> check)
    {
        var catalog = new DataCatalog();
        catalog.Register(Read<Language>("Language", row => row.ID));
        catalog.Register(Read<Popup>("Popup", row => row.ID));
        Popup popup = catalog.GetTable<Popup>().Get(15007);
        var text = new LanguageTextSource(catalog);
        foreach (var code in new[] { LanguageCode.Korean, LanguageCode.English })
        {
            foreach (int id in new[] { popup.BodyTextID, popup.Button1TextID, popup.Button2TextID, 30214 })
                check(!string.IsNullOrWhiteSpace(text.Read(id, code)));
            string format = text.Read(30330, code);
            check(format.Contains("{0}"));
            foreach (int id in new[] { 30331, 30332, 30333, 30334, 30335, 30336, 30341, 30342, 30343, 30344, 30345, 30346, 30347 })
            {
                string feature = text.Read(id, code);
                check(!string.IsNullOrWhiteSpace(feature) && string.Format(format, feature).Contains(feature));
            }
        }
        check(popup.DefaultButton >= 1 && popup.DefaultButton <= 2 && popup.CancelButton >= 0 && popup.CancelButton <= 2 && popup.Button3TextID == 0);
        bool failed = false;
        try { text.Read(popup.BodyTextID, (LanguageCode)99); } catch (ArgumentOutOfRangeException) { failed = true; }
        check(failed);
        string details = ErrorPopupDetails.Format(new ReportException(), 2, "TestScene", "Idle",
            new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.FromHours(9)), "기록 파일", "test.log");
        check(details.Contains("테스트 오류") && details.Contains("횟수: 2") && details.Contains("TestScene") && details.Contains("Idle"));
        check(details.Contains("첫째") && details.Contains("셋째") && !details.Contains("넷째"));
        check(details.Contains("2026-11-05 12:00:00 +09:00") && details.Contains("기록 파일: test.log"));
    }

    private static DataTable<T> Read<T>(string name, Func<T, int> id)
    {
        var document = new CsvParser().Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name + ".csv")));
        return new DataTable<T>(new CsvTableMapper<T>(new CsvDefinitionMapper<T>()).Map(document), id);
    }

    private class ReportException : Exception
    {
        public ReportException() : base("테스트 오류") { }
        public override string StackTrace => "첫째\r\n둘째\n셋째\n넷째";
    }
}
