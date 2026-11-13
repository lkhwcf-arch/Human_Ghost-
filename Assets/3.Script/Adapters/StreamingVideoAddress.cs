using System;
using System.IO;

namespace HumanGhost.Gameplay.Adapters
{
    // 테이블의 명시적 접두어만 로컬 영상으로 해석합니다. 일반 주소는 기존 주소 로더가 담당합니다.
    public static class StreamingVideoAddress
    {
        public const string Prefix = "streaming:";
        public static bool TryResolve(string address, string root, out string path)
        {
            path = null;
            if (address == null || !address.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            string relative = address.Substring(Prefix.Length);
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(":"))
                throw new ArgumentException("영상 경로는 StreamingAssets 내부 상대 경로여야 합니다.");
            string directory = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(directory, relative));
            if (!candidate.StartsWith(directory, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetExtension(candidate), ".mp4", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("StreamingAssets 내부 MP4 경로만 사용할 수 있습니다.");
            path = candidate;
            return true;
        }
    }
}
