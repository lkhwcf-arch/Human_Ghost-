using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    public enum CutsceneMedia { Image, Video }
    public enum CutscenePlaybackState { Idle, Preparing, Playing, Ended }
    public enum CutsceneEndReason { Completed, Skipped, Cancelled, Failed }

    // 정의는 리소스 주소와 재생 설정만 소유합니다. Unity 참조와 현재 재생 시간은 포함하지 않습니다.
    public sealed class CutsceneStep
    {
        public CutsceneMedia Media { get; }
        public string ResourceAddress { get; }
        public int DurationMs { get; }
        public int TextId { get; }
        public bool CanAdvance { get; }

        public CutsceneStep(CutsceneMedia media, string resourceAddress, int durationMs, int textId = 0, bool canAdvance = true)
        {
            if (!Enum.IsDefined(typeof(CutsceneMedia), media)) throw new ArgumentOutOfRangeException(nameof(media));
            if (string.IsNullOrWhiteSpace(resourceAddress)) throw new ArgumentException("컷씬 리소스 주소가 필요합니다.", nameof(resourceAddress));
            if (durationMs < 0 || (media == CutsceneMedia.Image && durationMs == 0))
                throw new ArgumentOutOfRangeException(nameof(durationMs), "그림 표시 시간은 양수 밀리초여야 합니다.");
            if (media == CutsceneMedia.Video && durationMs != 0)
                throw new ArgumentOutOfRangeException(nameof(durationMs), "영상은 종료 신호로 끝나므로 표시 시간은 0으로 둡니다.");
            if (textId < 0) throw new ArgumentOutOfRangeException(nameof(textId));
            Media = media;
            ResourceAddress = resourceAddress;
            DurationMs = durationMs;
            TextId = textId;
            CanAdvance = media == CutsceneMedia.Image && canAdvance;
        }
    }

    public sealed class CutsceneSequence
    {
        public string Key { get; }
        public IReadOnlyList<CutsceneStep> Steps { get; }
        public int SkipHoldMs { get; }

        public CutsceneSequence(string key, IEnumerable<CutsceneStep> steps, int skipHoldMs)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("컷씬 식별 키가 필요합니다.", nameof(key));
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            if (skipHoldMs <= 0) throw new ArgumentOutOfRangeException(nameof(skipHoldMs));
            var owned = new List<CutsceneStep>(steps);
            if (owned.Count == 0 || owned.Contains(null)) throw new ArgumentException("컷씬에는 유효한 재생 구간이 필요합니다.", nameof(steps));
            Key = key;
            Steps = owned.AsReadOnly();
            SkipHoldMs = skipHoldMs;
        }
    }
}
