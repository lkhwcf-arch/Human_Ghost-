using System;
using UnityEngine;
using HumanGhost.Gameplay.Presentation;

namespace HumanGhost.Gameplay.Adapters
{
    // 독립 화면은 이 입력을 사용하고 기존 조립자는 같은 SkipKeyInput을 재사용합니다.
    public class SkipInput : MonoBehaviour
    {
        [SerializeField, InspectorName("스킵 대상 컴포넌트")] private MonoBehaviour _target = null;
        [SerializeField, InspectorName("스킵 범위")] private SkipScope _scope = SkipScope.Presentation;
        [SerializeField, InspectorName("스킵 안내")] private SkipHint _hint = null;
        private LanguageTextSource _text;
        private LanguageCode _language;
        private SkipKeyInput _input;
        private bool _blocked;
        public void ConnectText(LanguageTextSource text, LanguageCode language)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _language = language;
            if (_hint == null || _target == null || !(_target is ISkipTarget target))
                throw new InvalidOperationException("독립 스킵 입력의 안내와 대상을 연결해 주세요.");
            _hint.Connect(target, _scope, text, language);
            _hint.SetBlocked(_blocked);
        }
        public void SetBlocked(bool blocked) { _blocked = blocked; if (_hint != null) _hint.SetBlocked(blocked); }
        protected virtual void OnEnable()
        {
            if (_target == null || !(_target is ISkipTarget))
                throw new InvalidOperationException("스킵 대상을 구현한 컴포넌트를 연결해 주세요.");
            if (_text != null) ConnectText(_text, _language);
            _input = new SkipKeyInput(CanSkip, Execute);
        }
        private bool CanSkip() => !_blocked && _target != null && _target.isActiveAndEnabled && ((ISkipTarget)_target).CanSkip(_scope);
        private void Execute()
        {
            try { ((ISkipTarget)_target).TrySkip(_scope); }
            catch (Exception error) { Debug.LogException(error, this); }
        }
        protected virtual void OnDisable() { _input?.Dispose(); _input = null; if (_hint != null) _hint.Disconnect(); }
    }
}
