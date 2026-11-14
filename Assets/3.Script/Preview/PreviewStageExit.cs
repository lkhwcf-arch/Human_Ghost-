using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 위치 변경은 접촉만 갱신하도록 예약하고 이동 명령은 기존 실행 순서에서 소비합니다.
    internal sealed class PreviewStageExit : IDisposable
    {
        private class Entry
        {
            public StageExit Definition;
            public StageExitContact Contact;
            public Transform Anchor;
        }
        private readonly PlayerRuntime _player;
        private readonly long _minimumX, _maximumX;
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Func<StageExit, bool> _canUse;
        private readonly Action<StageExit> _travel;
        private readonly Action<int> _showBlocked;
        private bool _dirty = true, _disposed;
        public PreviewStageExit(PlayerRuntime player, Bounds area, IEnumerable<StageExit> definitions, SpawnAnchors anchors,
            Func<StageExit, bool> canUse, Action<StageExit> travel, Action<int> showBlocked)
        {
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _canUse = canUse; _travel = travel; _showBlocked = showBlocked;
            _minimumX = player.ToInteger(area.min.x); _maximumX = player.ToInteger(area.max.x);
            foreach (StageExit definition in definitions)
            {
                if (!definition.ContentEnabled || definition.TriggerType == "Interact") continue;
                var entry = new Entry { Definition = definition, Contact = new StageExitContact(definition),
                    Anchor = definition.TriggerType == "Area" ? anchors.Get(definition.AnchorName) : null };
                Observe(entry);
                _entries.Add(entry);
            }
            player.SpatialChanged += RequestRefresh;
        }
        private void RequestRefresh() => _dirty = true;
        private bool Observe(Entry entry)
        {
            long x = 0, y = 0;
            if (entry.Definition.TriggerType == "Area")
            {
                if (entry.Anchor == null) throw new InvalidOperationException("출구 앵커의 수명이 끝났습니다. 출구 ID: " + entry.Definition.ID);
                x = _player.ToInteger(entry.Anchor.position.x); y = _player.ToInteger(entry.Anchor.position.y);
            }
            return entry.Contact.Observe(_player.Position.X, _player.Position.Y, _minimumX, _maximumX, x, y);
        }
        public void Step()
        {
            if (_disposed || !_dirty || _player == null || !_player.isActiveAndEnabled || _player.Actor.IsDead) return;
            _dirty = false;
            StageExit selected = null;
            foreach (Entry entry in _entries)
                if (Observe(entry))
                {
                    if (selected != null) throw new InvalidOperationException("출구 영역이 겹칩니다. 앵커·범위를 구분해 주세요. 출구 ID: " + selected.ID + ", " + entry.Definition.ID);
                    selected = entry.Definition;
                }
            if (selected == null) return;
            if (_canUse(selected)) _travel(selected);
            else if (selected.BlockedTextID != 0) _showBlocked?.Invoke(selected.BlockedTextID);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _player.SpatialChanged -= RequestRefresh;
            _entries.Clear();
        }
    }
}
