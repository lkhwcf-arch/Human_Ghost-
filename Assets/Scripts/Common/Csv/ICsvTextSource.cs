using System.Threading;
using System.Threading.Tasks;

namespace HumanGhost.Data.Csv
{
    public interface ICsvTextSource
    {
        Task<string> ReadTextAsync(string sourceName, CancellationToken token);
    }
}
