using System.Collections.Generic;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 전투 피드백(C11)입니다. 수치는 기획의 제안값이며 위치·표시는 임시입니다. 이펙트 원본이 오면 표시만 교체합니다.
    internal class PreviewCombatFeedback : System.IDisposable
    {
        private const int _normalStop = 50, _criticalStop = 80, _criticalSlow = 200, _executeStop = 150, _bossHitStop = 90;
        private const float _criticalSlowRate = 0.5f, _flashSeconds = 0.05f;
        private struct Flash { public SpriteRenderer Target; public Color Color; public float Remaining; }
        private readonly List<Flash> _flashes = new List<Flash>();
        private readonly GameSpeedController _speed;
        private readonly PreviewFollowCamera _camera;
        private readonly GameAudio _audio;
        private readonly TextMesh _textPrefab;
        private readonly string _dodgeText;
        private class FloatingText
        {
            public GameObject Root { get; set; }
            public TextMesh Label { get; set; }
            public float Remaining { get; set; }
        }
        private readonly List<FloatingText> _texts = new List<FloatingText>();
        private readonly Stack<FloatingText> _textPool = new Stack<FloatingText>();
        // 동시 표시는 제한하지 않고 대기 중인 표시만 최대 16개까지 보관합니다.
        private const int _maxRetainedTexts = 16;

        public PreviewCombatFeedback(GameSpeedController speed, PreviewFollowCamera camera, GameAudio audio, TextMesh textPrefab, string dodgeText)
        {
            _speed = speed; _camera = camera; _audio = audio; _textPrefab = textPrefab; _dodgeText = dodgeText;
        }

        // 플레이어가 적을 때린 결과입니다. 회피는 히트스톱이 없습니다.
        public void OnPlayerHit(HitOutcome outcome, SpriteRenderer target, Vector3 position)
        {
            switch (outcome)
            {
                case HitOutcome.Dodged:
                    ShowText(_dodgeText, position);
                    _audio?.PlayEffect("SFX_MISS");
                    break;
                case HitOutcome.Hit:
                    _speed?.State.HitStop(_normalStop);
                    _camera?.Shake(0.06f, 0.08f);
                    AddFlash(target, Color.white);
                    break;
                case HitOutcome.Critical:
                    _speed?.State.Critical(_criticalStop, _criticalSlow, _criticalSlowRate);
                    _camera?.Shake(0.14f, 0.14f);
                    AddFlash(target, new Color(1f, 0.15f, 0.15f));
                    _audio?.PlayEffect("SFX_CRIT");
                    break;
            }
        }

        public void OnExecute()
        {
            _speed?.State.ClearSlow();
            _speed?.State.HitStop(_executeStop);
            _camera?.Shake(0.25f, 0.25f);
        }
        public void OnParry(int hitStop)
        {
            _speed?.State.HitStop(hitStop);
            _camera?.Shake(0.08f, 0.1f);
            _audio?.PlayEffect("SFX_PARRY");
        }

        public void OnPlayerDamaged(bool byBoss)
        {
            if (byBoss) _speed?.State.HitStop(_bossHitStop);
            _camera?.Shake(0.15f, 0.2f);
        }

        // 다른 표시가 색을 정한 뒤 마지막에 덮습니다.
        public void Step(float unscaledSeconds, float seconds)
        {
            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                Flash flash = _flashes[i];
                if (flash.Target != null) flash.Target.color = flash.Color;
                flash.Remaining -= unscaledSeconds;
                if (flash.Remaining <= 0f) _flashes.RemoveAt(i); else _flashes[i] = flash;
            }
            for (int i = _texts.Count - 1; i >= 0; i--)
            {
                FloatingText text = _texts[i];
                text.Remaining -= seconds;
                if (text.Remaining > 0f) continue;
                _texts.RemoveAt(i);
                text.Root.SetActive(false);
                text.Label.text = "";
                if (_textPool.Count < _maxRetainedTexts) _textPool.Push(text);
                else Object.Destroy(text.Root);
            }
        }

        private void AddFlash(SpriteRenderer target, Color color)
        {
            if (target != null) _flashes.Add(new Flash { Target = target, Color = color, Remaining = _flashSeconds });
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
            item.Remaining = 0.6f;
            _texts.Add(item);
            item.Root.SetActive(true);
        }
        public void Dispose()
        {
            foreach (FloatingText text in _texts) if (text.Root != null) Object.Destroy(text.Root);
            foreach (FloatingText text in _textPool) if (text.Root != null) Object.Destroy(text.Root);
            _texts.Clear(); _textPool.Clear(); _flashes.Clear();
        }
    }
}
