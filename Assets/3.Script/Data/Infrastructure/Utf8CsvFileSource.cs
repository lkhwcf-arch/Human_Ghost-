using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Csv;

namespace HumanGhost.Data.Infrastructure
{
    // 게임 엔진과 독립적인 데스크톱 파일 입력입니다. 문자 해석에 실패하면 오류를 전달합니다.
    public sealed class Utf8CsvFileSource : ICsvTextSource
    {
        public async Task<string> ReadTextAsync(string filePath, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes, 81920, token);
            token.ThrowIfCancellationRequested();
            return new UTF8Encoding(false, true).GetString(bytes.ToArray());
        }
    }
}
