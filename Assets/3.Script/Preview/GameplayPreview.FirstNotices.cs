using System;
using System.Collections.Generic;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        private readonly Queue<int> _firstNotices = new Queue<int>();
        private readonly HashSet<int> _queuedFirstNotices = new HashSet<int>();
        private int _showingFirstNotice;
        private bool? _firstNoticeCompleted;
        private bool _noticePersistPending, _noticePersisting, _noticePersistFailed;

        private void QueueFirstNotice(int id)
        {
            if (id > 0 && !_progress.HasViewedNotice(id) && _queuedFirstNotices.Add(id)) _firstNotices.Enqueue(id);
        }
        private void QueueAvailableFirstNotices()
        {
            if (_progress.Rewards.Wallet.GetBalance(MonsterRewards.CoinCurrencyId) > 0) QueueFirstNotice(30364);
            if (_progress.Rewards.Wallet.GetBalance(MonsterRewards.SoulCurrencyId) > 0) QueueFirstNotice(30365);
            if (_actor.HasSkill) QueueFirstNotice(30366);
        }
        private void StepFirstNotices()
        {
            if (_interactor != null && _interactor.IsShrinePromptVisible) QueueFirstNotice(30363);
            if (_firstNoticeCompleted.HasValue)
            {
                int id = _showingFirstNotice;
                bool completed = _firstNoticeCompleted.Value;
                _firstNoticeCompleted = null; _showingFirstNotice = 0;
                if (completed)
                {
                    _queuedFirstNotices.Remove(id);
                    _progress.MarkNoticeViewed(id);
                    _noticePersistPending = true; _noticePersistFailed = false;
                }
                else _firstNotices.Enqueue(id);
            }
            if (_noticePersistPending && !_noticePersisting && !_noticePersistFailed && _saveSession != null && !_saveSession.IsSaving)
                PersistFirstNotices();
            if (!_subscribed || _showingFirstNotice != 0 || _firstNotices.Count == 0 || _textbox == null || _textbox.IsOpen || _textbox.IsObjectiveOpen || _textbox.IsNoticeOpen ||
                _speed == null || !CanPause() || HasPendingDialogue || (_cutscenes != null && _cutscenes.IsBusy) ||
                (_shrineScreen != null && _shrineScreen.IsOpen) || (_pause != null && _pause.IsOpen)) return;
            int next = _firstNotices.Dequeue();
            if (_progress.HasViewedNotice(next)) { _queuedFirstNotices.Remove(next); return; }
            var scope = _progressScope;
            if (scope == null || scope.IsDisposed) return;
            _showingFirstNotice = next;
            try
            {
                if (!_textbox.TryShowObjective(new LanguageTextSource(_catalog).Read(next, LanguageSettings.Current),
                    scope.Guard<bool>(completed => _firstNoticeCompleted = completed)))
                { _showingFirstNotice = 0; _firstNotices.Enqueue(next); }
            }
            catch
            {
                _showingFirstNotice = 0; _firstNotices.Enqueue(next);
                throw;
            }
        }
        private async void PersistFirstNotices()
        {
            var lifetime = _saveLifetime; var session = _saveSession;
            int[] notices = _progress.ViewedNotices;
            _noticePersisting = true; _noticePersistPending = false;
            try
            {
                bool saved = await session.SaveNoticeHistoryAsync(notices);
                if (this != null && ReferenceEquals(lifetime, _saveLifetime) && !saved) _noticePersistPending = true;
            }
            catch (Exception error)
            {
                if (this == null || !ReferenceEquals(lifetime, _saveLifetime)) return;
                _noticePersistPending = true; _noticePersistFailed = true;
                Debug.LogException(new InvalidOperationException("첫 안내 확인 기록 저장에 실패했습니다. 기존 진행 기록은 유지합니다.", error), this);
            }
            finally { if (this != null && ReferenceEquals(lifetime, _saveLifetime)) _noticePersisting = false; }
        }
        private void DisconnectFirstNotices()
        {
            bool showing = _showingFirstNotice != 0;
            _showingFirstNotice = 0; _firstNoticeCompleted = null;
            _firstNotices.Clear(); _queuedFirstNotices.Clear();
            _noticePersisting = false; _noticePersistPending = false; _noticePersistFailed = false;
            if (showing && _textbox != null) _textbox.CancelObjective();
        }
    }
}
