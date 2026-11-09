using System;

namespace HumanGhost.Gameplay.Adapters
{
    // 화면 종류와 관계없이 설정 결과의 적용·저장·실패 복구를 같은 순서로 연결합니다.
    public class SettingsApplication
    {
        private readonly Action<GameSettings> _apply, _save;
        private readonly Action<Exception> _report;
        private readonly Action<KeyboardLayout> _applyLayout;

        public SettingsApplication(Action<GameSettings> apply, Action<GameSettings> save, Action<Exception> report, Action<KeyboardLayout> applyLayout = null)
        {
            _apply = apply ?? throw new ArgumentNullException(nameof(apply));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _applyLayout = applyLayout;
        }

        public bool Complete(GameSettings original, GameSettings result)
        {
            if (original == null || result == null) throw new ArgumentNullException(nameof(result), "원래 설정과 편집 결과가 필요합니다.");
            try
            {
                // 외부 소비자가 전달값을 편집해도 원래 설정과 확정 결과는 보존합니다.
                Apply(result);
                if (!original.SameAs(result)) _save(result.Clone());
                return true;
            }
            catch (Exception error)
            {
                Exception failure = error;
                try { Apply(original); }
                catch (Exception rollback) { failure = new AggregateException("설정 적용 실패 뒤 원래 값 복구도 실패했습니다.", error, rollback); }
                _report(new InvalidOperationException("설정을 적용하거나 저장하지 못했습니다. 원래 값으로 복구를 시도했습니다.", failure));
                return false;
            }
        }

        private void Apply(GameSettings settings)
        {
            _applyLayout?.Invoke(settings.KeyboardLayout);
            _apply(settings.Clone());
        }
    }
}
