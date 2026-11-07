using System;
using System.IO;
using System.Text;

namespace HumanGhost.Gameplay.Adapters
{
    // 기록 완료 전에는 기존 파일을 건드리지 않습니다. 설정과 슬롯이 같은 교체 계약을 사용합니다.
    public static class AtomicTextFile
    {
        private static readonly UTF8Encoding _encoding = new UTF8Encoding(false, true);
        public static void Write(string path, string text)
        {
            string destination = Path.GetFullPath(path);
            byte[] bytes = _encoding.GetBytes(text ?? throw new ArgumentNullException(nameof(text)));
            string directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, ".write-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally { File.Delete(temporary); }
        }
    }
}
