using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 리그 하위 뼈·표시 순서의 기본값입니다. 클립마다 애니메이션하는 속성이 달라 직전 클립 값이 남지 않도록 샘플 전에 되돌립니다.
    internal sealed class RigBindPose
    {
        private readonly Transform[] _bones;
        private readonly Vector3[] _positions, _scales;
        private readonly Quaternion[] _rotations;
        private readonly SpriteRenderer[] _renderers;
        private readonly int[] _orders;

        public RigBindPose(GameObject rig)
        {
            var all = rig.GetComponentsInChildren<Transform>(true);
            _bones = new Transform[all.Length - 1];
            for (int i = 1; i < all.Length; i++) _bones[i - 1] = all[i];
            _positions = new Vector3[_bones.Length]; _rotations = new Quaternion[_bones.Length]; _scales = new Vector3[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            { _positions[i] = _bones[i].localPosition; _rotations[i] = _bones[i].localRotation; _scales[i] = _bones[i].localScale; }
            _renderers = rig.GetComponentsInChildren<SpriteRenderer>(true);
            _orders = new int[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _orders[i] = _renderers[i].sortingOrder;
        }

        public void Restore()
        {
            for (int i = 0; i < _bones.Length; i++)
                _bones[i].SetLocalPositionAndRotation(_positions[i], _rotations[i]);
            for (int i = 0; i < _bones.Length; i++) _bones[i].localScale = _scales[i];
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].sortingOrder = _orders[i];
        }
    }
}
