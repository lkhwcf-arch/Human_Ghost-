using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Presentation;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 준비된 텍스트 에셋을 한 번 읽고 공용 대사 화면과 실행 상태를 연결합니다.
    // 결과가 필요한 소유자(명부 조사)는 요청 번호를 받고, 다음 실행 순서에서 TryTakeResult로 결과를 가져갑니다.
    public class DialogueRunner : IDisposable
    {
        private readonly DialogueSession _session;
        private readonly TextboxScreen _screen;
        private readonly LanguageTextSource _text;
        private readonly Action<bool> _pause;
        private readonly Action<Exception> _failed;
        private readonly LanguageCode? _language;
        private LanguageCode Language => _language ?? LanguageSettings.Current;
        private readonly DialogueRequests _requests = new DialogueRequests();
        private bool? _pending;
        private bool _disposed;
        public bool IsRunning => !_disposed && _session.IsRunning;
        public bool CanPlay(int id) => !_disposed && _session.CanBegin(id);
        public event Action AvailabilityChanged;
        public string ReadText(int id) => _text.Read(id, Language);
        public DialogueRunner(IReadOnlyDataCatalog data, IReadOnlyList<TextAsset> assets, TextboxScreen screen, Action<bool> pause, Action<Exception> failed,
            LanguageCode? language = null)
        {
            _screen = screen != null ? screen : throw new ArgumentNullException(nameof(screen));
            _text = new LanguageTextSource(data); _pause = pause ?? throw new ArgumentNullException(nameof(pause));
            _failed = failed ?? throw new ArgumentNullException(nameof(failed));
            _language = language;
            var files = assets.Where(asset => asset != null).ToDictionary(asset => asset.name, StringComparer.Ordinal);
            var lines = new Dictionary<int, IReadOnlyList<DialogueLine>>();
            foreach (Dialogue row in data.GetTable<Dialogue>().All)
            {
                JObject json = JObject.Parse(files[Path.GetFileNameWithoutExtension(row.LineFile)].text);
                lines.Add(row.ID, Array.AsReadOnly(json["Lines"].Select(line =>
                    new DialogueLine((int)line["SpeakerTextID"], (int)line["TextID"])).ToArray()));
            }
            _session = new DialogueSession(data.GetTable<Dialogue>().All, id => lines[id]);
        }
        // 결과를 받지 않는 기존 요청(NPC 말 걸기·보스 대사)입니다.
        public bool TryPlay(int id, object owner) => TryPlay(id, owner, false, out _);
        // 종료 결과가 필요한 요청입니다. 성공하면 requestId로 TryTakeResult를 호출합니다.
        public bool TryPlay(int id, object owner, out int requestId)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            return TryPlay(id, owner, true, out requestId);
        }
        // true: 결과 확정. false: 아직 재생 중. 실행기 정리·교체로 사라진 요청은 Cancelled입니다.
        public bool TryTakeResult(object owner, int requestId, out DialogueEndReason reason) => _requests.TryTake(owner, requestId, out reason);
        private bool TryPlay(int id, object owner, bool tracked, out int requestId)
        {
            requestId = 0;
            if (_disposed || _screen == null || !_screen.isActiveAndEnabled || _screen.IsOpen || !_session.TryBegin(id, out var lines)) return false;
            requestId = _requests.Begin(owner ?? this, tracked); _pending = null;
            try
            {
                bool paused = _session.Current.PauseGame;
                _pause(paused);
                _screen.Open(lines, _text, Language, paused, () =>
                {
                    if (!_disposed) _pending = _screen != null && _screen.LastPlaybackCompleted;
                });
                return true;
            }
            catch (Exception error)
            {
                // 시작 실패는 반환값으로 알리므로 요청 결과를 남기지 않습니다.
                requestId = 0; _requests.Drop();
                Dispose(); _failed(error); return false;
            }
        }
        public void Step()
        {
            if (_disposed || !_pending.HasValue) return;
            bool completed = _pending.Value; _pending = null;
            Dialogue definition = _session.Current;
            bool first = _session.End(completed);
            _requests.End(completed ? DialogueEndReason.Completed : DialogueEndReason.Cancelled);
            _pause(false);
            AvailabilityChanged?.Invoke();
            if (first && definition.ObjectiveTextID != 0 && _screen != null)
                _screen.ShowObjective(ReadText(definition.ObjectiveTextID));
        }
        public void Cancel(object owner)
        {
            // 소유자가 직접 취소하면 자기 요청 결과도 더 이상 필요 없으므로 함께 폐기합니다.
            _requests.Discard(owner);
            if (_disposed || !_session.IsRunning || !_requests.IsCurrent(owner)) return;
            try { if (_screen != null) _screen.Close(); }
            finally { _pending = null; _requests.Drop(); _session.End(false); _pause(false); AvailabilityChanged?.Invoke(); }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (_session.IsRunning && _screen != null) _screen.Close(); }
            finally { _pending = null; _requests.Clear(); _session.End(false); _pause(false); AvailabilityChanged = null; }
        }
    }
}
