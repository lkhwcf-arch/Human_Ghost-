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
        check(text.Read(popup.BodyTextID, LanguageCode.Korean) == "오류가 발생했습니다");
        check(text.Read(popup.BodyTextID, LanguageCode.English) == "An error occurred");
        check(text.Read(popup.Button1TextID, LanguageCode.Korean) == "복사");
        check(text.Read(popup.Button2TextID, LanguageCode.English) == "Close");
        check(popup.DefaultButton == 1 && popup.CancelButton == 2 && popup.Button3TextID == 0);
        check(text.Read(30214, LanguageCode.English) == "Copied");
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
