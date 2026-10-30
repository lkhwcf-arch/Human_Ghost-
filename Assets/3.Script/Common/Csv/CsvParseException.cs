using System;

namespace HumanGhost.Data.Csv
{
    public enum CsvErrorCode
    {
        MissingHeader = 1, InvalidQuote = 2, UnclosedQuote = 3,
        EmptyHeader = 4, DuplicateHeader = 5, FieldCountMismatch = 6
    }

    public sealed class CsvParseException : FormatException
    {
        public CsvErrorCode Code
        {
            get;
        }
        public string SourceName
        {
            get;
        }
        public int LineNumber
        {
            get;
        }
        public int FieldNumber
        {
            get;
        }
        public CsvParseException(CsvErrorCode code, string sourceName, int lineNumber, int fieldNumber)
            : base($"CSV 읽기 오류: {Describe(code)}, {sourceName}, {lineNumber}행 {fieldNumber}열")
        {
            Code = code;
            SourceName = sourceName;
            LineNumber = lineNumber;
            FieldNumber = fieldNumber;
        }

        private static string Describe(CsvErrorCode code)
        {
            return code switch
            {
                CsvErrorCode.MissingHeader => "헤더가 없습니다.",
                CsvErrorCode.InvalidQuote => "따옴표 사용이 올바르지 않습니다.",
                CsvErrorCode.UnclosedQuote => "따옴표가 닫히지 않았습니다.",
                CsvErrorCode.EmptyHeader => "빈 헤더가 있습니다.",
                CsvErrorCode.DuplicateHeader => "중복된 헤더가 있습니다.",
                CsvErrorCode.FieldCountMismatch => "행의 열 수가 헤더와 다릅니다.",
                _ => "알 수 없는 읽기 오류입니다."
            };
        }
    }
}
