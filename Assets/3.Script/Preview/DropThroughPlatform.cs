using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 기획 K18의 허용 목록을 발판 프리팹에 명시합니다.
    public sealed class DropThroughPlatform : MonoBehaviour
    {
        [SerializeField, InspectorName("아래 점프 허용")] private bool _allowed = false;
        public bool Allowed => _allowed;
    }
}
