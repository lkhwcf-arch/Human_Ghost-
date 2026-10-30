using System.Threading;
using System.Threading.Tasks;

namespace HumanGhost.Data.Text
{
    public interface ITextSource
    {
        Task<string> ReadTextAsync(string sourceName, CancellationToken token);
    }
}
