using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 일반몹 이름표. 정의 수신 후 실행 코어의 현재 상태·HP를 표시합니다.
    public sealed class MonsterPreviewLabel : MonoBehaviour
    {
        [SerializeField] private MonsterRuntime _runtime = null;
        [SerializeField] private TextMesh _label = null;
        [SerializeField] private string _displayName = "일반 몬스터";

        private void OnEnable()
        {
            if (_runtime == null || _label == null)
            {
                Debug.LogError("일반몹 데이터 수신부와 한글 이름표를 연결하세요.", this);
                enabled = false;
                return;
            }
            _runtime.Initialized += RefreshLabel;
            if (_runtime.IsInitialized) RefreshLabel(_runtime.Definition);
        }

        private void OnDisable()
        {
            if (_runtime != null) _runtime.Initialized -= RefreshLabel;
        }

        private void LateUpdate()
        {
            if (_runtime.IsInitialized) RefreshLabel(_runtime.Definition);
        }

        // 실행 코어의 현재 상태·현재 HP를 표시합니다.
        private void RefreshLabel(Monster definition)
        {
            MonsterActor actor = _runtime.Actor;
            string text = _displayName + "\n몬스터 " + definition.ID + "\n" + GameplayPreview.GetStateLabel(actor.CurrentState) +
                (actor.IsSuperArmor ? " (슈퍼아머)" : "") + "\n체력 " + actor.Health;
            if (_label.text != text) _label.text = text;
        }
    }
}
