using System;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 스킵 가능 여부의 명시적 변경에만 표시를 갱신합니다.
    public class SkipHint : MonoBehaviour
    {
        [SerializeField, InspectorName("안내 표시")] private Text _label = null;
        [SerializeField, InspectorName("안내 Language ID")] private int _textId = 30044;
        private ISkipTarget _target;
        private SkipScope _scope;
        private bool _blocked;

        public void Connect(ISkipTarget target, SkipScope scope, LanguageTextSource text, LanguageCode language)
        {
            Disconnect();
            if (_label == null || text == null || target == null)
                throw new InvalidOperationException("스킵 안내의 표시·대상·언어 공급자를 연결해 주세요.");
            _label.text = _textId == 30044 ? text.ReadUi(51511, language) : text.Read(_textId, language);
            _scope = scope;
            _target = target;
            _target.SkipAvailabilityChanged += Refresh;
            Refresh();
        }
        public void SetBlocked(bool blocked) { _blocked = blocked; Refresh(); }
        private void Refresh()
        {
            if (_label == null) return;
            bool alive = _target != null && (!(_target is UnityEngine.Object owner) || owner != null);
            bool visible = !_blocked && alive && _target.CanSkip(_scope);
            try
            {
                if (_label.gameObject.activeSelf != visible) _label.gameObject.SetActive(visible);
            }
            catch (Exception error)
            {
                ISkipTarget target = _target;
                _target = null;
                if (target != null) target.SkipAvailabilityChanged -= Refresh;
                Debug.LogException(new InvalidOperationException("스킵 안내를 표시하지 못했습니다.", error), this);
            }
        }
        public void Disconnect()
        {
            ISkipTarget target = _target;
            _target = null;
            if (target != null) target.SkipAvailabilityChanged -= Refresh;
            if (_label != null) _label.gameObject.SetActive(false);
        }
        protected virtual void Awake() { if (_label != null) _label.gameObject.SetActive(false); }
        protected virtual void OnDisable() => Disconnect();
    }
}
