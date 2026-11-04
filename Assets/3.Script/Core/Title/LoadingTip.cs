using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // 로딩 툴팁 규칙(O07_1·O07_2)입니다. 번호대 안 행만 고르고 직전 것을 피하며 첫 ': ' 앞을 제목으로 나눕니다.
    public static class LoadingTip
    {
        public const int FirstId = 30101;
        public const int LastId = 30199;

        public static int Pick(IReadOnlyList<int> ids, int previous, Func<int, int, int> random)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var candidates = new List<int>();
            foreach (int id in ids) if (id >= FirstId && id <= LastId && id != previous) candidates.Add(id);
            if (candidates.Count == 0)
                foreach (int id in ids) if (id >= FirstId && id <= LastId) candidates.Add(id);
            return candidates.Count == 0 ? 0 : candidates[random(0, candidates.Count)];
        }

        // 쌍점까지 제목입니다. 쌍점이 없으면 제목은 빈 문자열입니다.
        public static void Split(string text, out string title, out string body)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            int colon = text.IndexOf(": ", StringComparison.Ordinal);
            if (colon < 0) { title = ""; body = text; return; }
            title = text.Substring(0, colon + 1);
            body = text.Substring(colon + 1);
        }

        // 로딩 진행 표시: 실제 진행과 최소 표시 시간 중 느린 쪽을 따릅니다.
        public static float Progress(float loaded, float elapsedSeconds, float minimumSeconds)
        {
            float time = minimumSeconds > 0f ? elapsedSeconds / minimumSeconds : 1f;
            return Math.Min(1f, Math.Max(0f, Math.Min(loaded, time)));
        }
    }
}
