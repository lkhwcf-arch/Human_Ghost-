using System;
using System.Linq;

namespace HumanGhost.Gameplay
{
    public class ErrorPopupDetails
    {
        public static string Format(Exception error, int count, string scene, string state, DateTimeOffset time, string logLabel, string logPath)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            return Format(error.GetType().Name, error.Message, error.StackTrace, count, scene, state, time, logLabel, logPath);
        }

        public static string Format(string kind, string message, string sourceStack, int count, string scene, string state, DateTimeOffset time, string logLabel, string logPath)
        {
            string stack = string.Join("\n", (sourceStack ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Take(3));
            return $"종류: {kind}\n횟수: {count}\n메시지: {message}\n스택: {stack}\n씬: {scene}\nState: {state}\n시간: {time:yyyy-MM-dd HH:mm:ss zzz}\n{logLabel}: {logPath}";
        }
    }
}
