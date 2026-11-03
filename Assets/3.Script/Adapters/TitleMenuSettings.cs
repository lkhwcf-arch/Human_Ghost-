using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 타이틀 메뉴에 필요한 정책만 보유합니다. 저장 상태·메뉴 선택은 런타임이 소유합니다.
    [CreateAssetMenu(menuName = "인귀/타이틀/메뉴 설정")]
    public sealed class TitleMenuSettings : ScriptableObject
    {
        [SerializeField, InspectorName("종료 메뉴 표시")] private bool _showQuit = true;
        [SerializeField, InspectorName("저장 있을 때 새 게임 확인")] private bool _confirmNewGameWithSave = true;
        [SerializeField, InspectorName("최초 이어하기 우선 선택")] private bool _preferContinue = true;

        public bool ShowQuit => _showQuit;
        public bool ConfirmNewGameWithSave => _confirmNewGameWithSave;
        public bool PreferContinue => _preferContinue;
    }
}
