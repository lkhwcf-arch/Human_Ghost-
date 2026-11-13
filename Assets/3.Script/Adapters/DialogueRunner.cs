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
    public class DialogueRunner : IDisposable
    {
        private readonly DialogueSession _session;
        private readonly TextboxScreen _screen;
        private readonly LanguageTextSource _text;
        private readonly Action<bool> _pause;
        private readonly Action<Exception> _failed;
        private readonly LanguageCode _language;
        private object _owner;
        private bool? _pending;
        private bool _disposed;
        public bool IsRunning => !_disposed && _session.IsRunning;
        public bool CanPlay(int id) => !_disposed && _session.CanBegin(id);
        public event Action AvailabilityChanged;
        public string ReadText(int id) => _text.Read(id, _language);
        public DialogueRunner(IReadOnlyDataCatalog data, IReadOnlyList<TextAsset> assets, TextboxScreen screen, Action<bool> pause, Action<Exception> failed,
            LanguageCode language = LanguageCode.Korean)
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
        public bool TryPlay(int id, object owner)
        {
            if (_disposed || _screen == null || !_screen.isActiveAndEnabled || _screen.IsOpen || !_session.TryBegin(id, out var lines)) return false;
            _owner = owner ?? this; _pending = null;
            try
            {
                bool paused = _session.Current.PauseGame;
                _pause(paused);
                _screen.Open(lines, _text, _language, paused, () =>
                {
                    if (!_disposed) _pending = _screen != null && _screen.LastPlaybackCompleted;
                });
                return true;
            }
            catch (Exception error)
            {
                Dispose(); _failed(error); return false;
            }
        }
        public void Step()
        {
            if (_disposed || !_pending.HasValue) return;
            bool completed = _pending.Value; _pending = null;
            Dialogue definition = _session.Current;
            bool first = _session.End(completed); _owner = null;
            _pause(false);
            AvailabilityChanged?.Invoke();
            if (first && definition.ObjectiveTextID != 0 && _screen != null)
                _screen.ShowObjective(ReadText(definition.ObjectiveTextID));
        }
        public void Cancel(object owner)
        {
            if (_disposed || !_session.IsRunning || !ReferenceEquals(_owner, owner)) return;
            try { if (_screen != null) _screen.Close(); }
            finally { _pending = null; _owner = null; _session.End(false); _pause(false); AvailabilityChanged?.Invoke(); }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (_session.IsRunning && _screen != null) _screen.Close(); }
            finally { _pending = null; _owner = null; _session.End(false); _pause(false); AvailabilityChanged = null; }
        }
    }
}
