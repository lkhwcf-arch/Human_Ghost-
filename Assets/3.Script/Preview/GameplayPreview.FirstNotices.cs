using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
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
            // 재화별 첫 안내 문구는 Currency.FirstNoticeTextID가 정합니다(11/19 데이터 이전).
            foreach (Currency currency in _catalog.GetTable<Currency>().All)
                if (_progress.Rewards.Wallet.GetBalance(currency.ID) > 0) QueueFirstNotice(CurrencyFirstNotice(currency));
            if (_actor.HasSkill) QueueFirstNotice(30366);
        }
        private static int CurrencyFirstNotice(Currency currency) => currency.ContentEnabled ? currency.FirstNoticeTextID ?? 0 : 0;
        // 이 안내 문구를 첫 안내로 쓰는 재화의 그림 키(Currency.NoticeIcon)입니다. 없으면 빈 문자열(그림 없는 목표 안내 띠)입니다.
        private string FirstNoticeIcon(int textId)
        {
            foreach (Currency currency in _catalog.GetTable<Currency>().All)
                if (CurrencyFirstNotice(currency) == textId) return currency.NoticeIcon;
            return "";
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
                string text = new LanguageTextSource(_catalog).Read(next, LanguageSettings.Current);
                Action<bool> closed = scope.Guard<bool>(completed => _firstNoticeCompleted = completed);
                // 재화 첫 안내처럼 데이터가 그림을 정한 안내는 보상 안내로, 나머지는 목표 안내 띠로 보여 줍니다.
                string icon = FirstNoticeIcon(next);
                bool shown = icon.Length > 0 ? _textbox.TryShowReward(text, icon, closed) : _textbox.TryShowObjective(text, closed);
                if (!shown)
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
