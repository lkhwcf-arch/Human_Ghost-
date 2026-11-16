using System.Collections.Generic;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 전투 피드백(C11)입니다. 수치는 기획의 제안값이며 위치·표시는 임시입니다. 이펙트 원본이 오면 표시만 교체합니다.
    internal class PreviewCombatFeedback : System.IDisposable
    {
        private const int _normalStop = 50, _criticalStop = 80, _criticalSlow = 200, _executeStop = 150, _bossHitStop = 50;
        private const int _criticalSlowRate = 5000, _flashMilliseconds = 50;
        private MillisecondClock _scaledClock, _unscaledClock;
        private struct Flash { public SpriteRenderer Target; public Color Color; public int Remaining; }
        private readonly List<Flash> _flashes = new List<Flash>();
        private readonly GameSpeedController _speed;
        private readonly PreviewFollowCamera _camera;
        private readonly GameAudio _audio;
        private readonly TextMesh _textPrefab;
        private string _dodgeText;
        private readonly SpriteClipEffect _hitPrefab;
        private readonly List<(SpriteClipEffect Effect, Stack<SpriteClipEffect> Pool)> _impacts = new();
        private readonly Dictionary<SpriteClipEffect, Stack<SpriteClipEffect>> _impactPools = new();
        private const int _maxRetainedImpacts = 16;
        private class FloatingText
        {
            public GameObject Root { get; set; }
            public TextMesh Label { get; set; }
            public int Remaining { get; set; }
        }
        private readonly List<FloatingText> _texts = new List<FloatingText>();
        private readonly Stack<FloatingText> _textPool = new Stack<FloatingText>();
        // 동시 표시는 제한하지 않고 대기 중인 표시만 최대 16개까지 보관합니다.
        private const int _maxRetainedTexts = 16;

        public PreviewCombatFeedback(GameSpeedController speed, PreviewFollowCamera camera, GameAudio audio, TextMesh textPrefab, string dodgeText, SpriteClipEffect hitPrefab = null)
        {
            _speed = speed; _camera = camera; _audio = audio; _textPrefab = textPrefab; _dodgeText = dodgeText; _hitPrefab = hitPrefab;
        }

        public void SetDodgeText(string text) => _dodgeText = text;

        // 플레이어가 적을 때린 결과입니다. 회피는 히트스톱이 없습니다.
        public void OnPlayerHit(HitOutcome outcome, SpriteRenderer target, Vector3 position, int direction = 1, SpriteClipEffect impactPrefab = null)
        {
            switch (outcome)
            {
                case HitOutcome.Dodged:
                    ShowText(_dodgeText, position);
                    _audio?.PlayEffect("SFX_MISS");
                    break;
                case HitOutcome.Hit:
                    _speed?.State.HitStop(_normalStop);
                    _camera?.Shake(0.06f, 80);
                    AddFlash(target, Color.white);
                    ShowImpact(target, position, direction, impactPrefab);
                    break;
                case HitOutcome.Critical:
                    _speed?.State.Critical(_criticalStop, _criticalSlow, _criticalSlowRate);
                    _camera?.Shake(0.14f, 140);
                    AddFlash(target, new Color(1f, 0.15f, 0.15f));
                    ShowImpact(target, position, direction, impactPrefab);
                    break;
            }
        }

        public void OnExecute()
        {
            _speed?.State.ClearSlow();
            _speed?.State.HitStop(_executeStop);
            _camera?.Shake(0.25f, 250);
        }
        public void OnParry(int hitStop)
        {
            _speed?.State.HitStop(hitStop);
            _camera?.Shake(0.08f, 100);
            _audio?.PlayEffect("SFX_PARRY");
        }

        public void OnPlayerDamaged(bool byBoss, bool knockedDown = false, SpriteRenderer target = null)
        {
            if (knockedDown) _speed?.State.Critical(_bossHitStop, _criticalSlow, _criticalSlowRate);
            else if (byBoss) _speed?.State.HitStop(_bossHitStop);
            if (knockedDown && target != null) ShowImpact(target, target.bounds.center, 1);
            _camera?.Shake(knockedDown ? .25f : .15f, knockedDown ? 250 : 200);
        }

        // 다른 표시가 색을 정한 뒤 마지막에 덮습니다.
        public void Step(float unscaledSeconds, float seconds)
        {
            int realMilliseconds = _unscaledClock.AdvanceFrame(unscaledSeconds);
            int milliseconds = _scaledClock.AdvanceFrame(seconds);
            for (int i = _impacts.Count - 1; i >= 0; i--)
            {
                var (impact, pool) = _impacts[i];
                impact.Step(unscaledSeconds);
                if (impact.IsPlaying) continue;
                _impacts.RemoveAt(i);
                if (pool.Count < _maxRetainedImpacts) pool.Push(impact);
                else Object.Destroy(impact.gameObject);
            }
            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                Flash flash = _flashes[i];
                if (flash.Target != null) flash.Target.color = flash.Color;
                flash.Remaining -= realMilliseconds;
                if (flash.Remaining <= 0) _flashes.RemoveAt(i); else _flashes[i] = flash;
            }
            for (int i = _texts.Count - 1; i >= 0; i--)
            {
                FloatingText text = _texts[i];
                text.Remaining -= milliseconds;
                if (text.Remaining > 0) continue;
                _texts.RemoveAt(i);
                text.Root.SetActive(false);
                text.Label.text = "";
                if (_textPool.Count < _maxRetainedTexts) _textPool.Push(text);
                else Object.Destroy(text.Root);
            }
        }

        private void ShowImpact(SpriteRenderer target, Vector3 position, int direction, SpriteClipEffect prefab = null)
        {
            if (prefab == null) prefab = _hitPrefab;
            if (prefab == null) return;
            if (!_impactPools.TryGetValue(prefab, out var pool))
            {
                pool = new Stack<SpriteClipEffect>();
                _impactPools.Add(prefab, pool);
            }
            SpriteClipEffect impact = null;
            try
            {
                impact = pool.Count > 0 ? pool.Pop() : Object.Instantiate(prefab);
                var source = prefab.GetComponent<SpriteRenderer>();
                var renderer = impact.GetComponent<SpriteRenderer>();
                // 원본이 오른쪽으로 펼쳐지는 이펙트는 프리팹의 Flip X로 구분합니다.
                renderer.flipX = (direction > 0) != source.flipX;
                if (target != null) { renderer.sortingLayerID = target.sortingLayerID; renderer.sortingOrder = target.sortingOrder + 1; }
                else { renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder; }
                impact.Play((target != null ? target.bounds.center : position) + Vector3.right * (direction * 0.2f));
                _impacts.Add((impact, pool));
            }
            catch (System.Exception error) { Debug.LogException(error); if (impact != null) Object.Destroy(impact.gameObject); }
        }

        private void AddFlash(SpriteRenderer target, Color color)
        {
            if (target != null) _flashes.Add(new Flash { Target = target, Color = color, Remaining = _flashMilliseconds });
        }

        private void ShowText(string text, Vector3 position)
        {
            if (_textPrefab == null) return;
            FloatingText item;
            if (_textPool.Count > 0) item = _textPool.Pop();
            else
            {
                var root = new GameObject("CombatFeedbackText");
                root.SetActive(false);
                item = new FloatingText { Root = root, Label = Object.Instantiate(_textPrefab, root.transform) };
                item.Label.transform.localPosition = Vector3.zero;
                item.Label.transform.localRotation = Quaternion.identity;
            }
            item.Root.transform.position = position + Vector3.up * 1.5f;
            item.Label.text = text;
            item.Remaining = 600;
            _texts.Add(item);
            item.Root.SetActive(true);
        }
        public void Dispose()
        {
            foreach (var (impact, _) in _impacts) if (impact != null) Object.Destroy(impact.gameObject);
            foreach (var pool in _impactPools.Values)
                foreach (SpriteClipEffect impact in pool) if (impact != null) Object.Destroy(impact.gameObject);
            _impacts.Clear(); _impactPools.Clear();
            foreach (FloatingText text in _texts) if (text.Root != null) Object.Destroy(text.Root);
            foreach (FloatingText text in _textPool) if (text.Root != null) Object.Destroy(text.Root);
            _texts.Clear(); _textPool.Clear(); _flashes.Clear();
        }
    }
}
