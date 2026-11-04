using System;
using Newtonsoft.Json;

namespace HumanGhost.Gameplay.Adapters
{
    // 배치 JSON을 읽습니다. 형식 오류는 호출자에게 전달합니다.
    public static class StageLayoutReader
    {
        private const int _supportedVersion = 2;

        public static StageLayout Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("배치 JSON이 비어 있습니다.", nameof(json));
            StageLayout layout = JsonConvert.DeserializeObject<StageLayout>(json) ?? throw new InvalidOperationException("배치 JSON을 해석하지 못했습니다.");
            if (layout.FormatVersion != _supportedVersion)
                throw new InvalidOperationException($"지원하지 않는 배치 JSON 버전입니다: {layout.FormatVersion}");
            return layout;
        }
    }
}
