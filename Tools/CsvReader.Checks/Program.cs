using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Infrastructure;

internal static class Program
{
    private static int _checks;

    private static void Check(bool result)
    {
        if (!result) throw new Exception($"검증 실패: {_checks + 1}");
        _checks++;
    }

    private static async Task Main()
    {
        var parser = new CsvParser();
        var document = parser.Read("\uFEFF ID ,Text\r\n001,\"가,나\r\n\"\"다\"\"\"\r\n\r\n,\"\"\r\n");
        Check(document.Headers[0] == "ID");
        Check(document.Rows.Count == 2);
        Check(document.Rows[0].Get("ID") == "001");
        Check(document.Rows[0].Get("Text") == "가,나\r\n\"다\"");
        Check(document.Rows[1].Get("ID") == "");
        Check(document.Rows[0].SourceLine == 2 && document.Rows[1].SourceLine == 5);
        Check(parser.Read("ID,Text\n1, value ").Rows[0].Get("Text") == " value ");
        foreach (var item in new (string text, CsvErrorCode code)[]
        {
            ("", CsvErrorCode.MissingHeader), ("ID,ID", CsvErrorCode.DuplicateHeader),
            ("ID,", CsvErrorCode.EmptyHeader), ("ID,Text\n1", CsvErrorCode.FieldCountMismatch),
            ("ID\n\"x", CsvErrorCode.UnclosedQuote), ("ID\na\"b", CsvErrorCode.InvalidQuote)
        })
        {
            try { parser.Read(item.text, "bad"); throw new Exception("오류가 누락되었습니다."); }
            catch (CsvParseException error)
            {
                Check(error.Code == item.code && error.SourceName == "bad" && error.LineNumber > 0 && error.FieldNumber > 0);
            }
        }

        string path = Path.Combine(AppContext.BaseDirectory, "input.csv");
        try
        {
            File.WriteAllText(path, "ID,Text\n001,한글", new UTF8Encoding(true));
            var loader = new CsvLoader(new Utf8CsvFileSource(), parser);
            Check((await loader.LoadAsync(path, CancellationToken.None)).Rows[0].Get("Text") == "한글");
            File.WriteAllBytes(path, new byte[] { 0xff });
            try { await loader.LoadAsync(path, CancellationToken.None); throw new Exception("UTF-8 오류가 누락되었습니다."); }
            catch (DecoderFallbackException) { _checks++; }
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try { await loader.LoadAsync(path, cancellation.Token); throw new Exception("취소가 누락되었습니다."); }
            catch (OperationCanceledException) { _checks++; }
        }
        finally { File.Delete(path); }
        Console.WriteLine($"PASS: {_checks} checks");
    }
}
