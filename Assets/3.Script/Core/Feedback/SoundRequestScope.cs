using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // 로딩 완료가 늦게 도착해도 종료·정지·더 최신 배경음 요청을 덮어쓰지 않게 합니다.
    public class SoundRequestScope
    {
        public readonly struct Request
        {
            internal readonly int Life, Revision, Music;
            public int SoundId { get; }
            internal Request(int life, int revision, int music, int soundId)
            { Life = life; Revision = revision; Music = music; SoundId = soundId; }
        }
        private readonly Dictionary<int, int> _revisions = new Dictionary<int, int>();
        private int _life, _music;
        private bool _active;
        public void Open() { _active = true; ++_life; }
        public void Close() { _active = false; ++_life; _revisions.Clear(); }
        public void Stop(int soundId) => _revisions[soundId] = Revision(soundId) + 1;
        public Request Begin(int soundId, bool music)
        {
            if (!_active) throw new InvalidOperationException("활성화된 사운드 수명이 필요합니다.");
            return new Request(_life, Revision(soundId), music ? ++_music : 0, soundId);
        }
        public bool IsCurrent(Request request) => _active && request.Life == _life
            && request.Revision == Revision(request.SoundId) && (request.Music == 0 || request.Music == _music);
        private int Revision(int soundId) => _revisions.TryGetValue(soundId, out int revision) ? revision : 0;
    }
}
