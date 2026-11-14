using System.Collections.Generic;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // Observe the completed bind state. This view never starts, extends or releases a bind.
    [DefaultExecutionOrder(20)]
    public sealed class BossChainView : MonoBehaviour
    {
        [SerializeField] private BossRuntime _boss = null;
        [SerializeField] private SpriteRenderer _body = null;
        [SerializeField] private SpriteRenderer _source = null;
        private PlayerRuntime _player;
        private SpriteRenderer _playerBody;
        private Mesh _mesh;
        private Material _material;
        private MeshRenderer _renderer;
        private readonly List<Vector3> _vertices = new List<Vector3>(1536);
        private readonly List<Color> _colors = new List<Color>(1536);
        private readonly List<int> _triangles = new List<int>(4608);
        public void Connect(PlayerRuntime player, SpriteRenderer body) { _player = player; _playerBody = body; }
        private void Awake()
        {
            var root = new GameObject("ChainTether");
            root.transform.SetParent(transform, false);
            _mesh = new Mesh { name = "BossChainLinks" };
            _mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = root.AddComponent<MeshRenderer>();
            _material = new Material(Shader.Find("Sprites/Default"));
            _renderer.sharedMaterial = _material;
            _renderer.enabled = false;
        }
        private void LateUpdate()
        {
            bool visible = _boss != null && _boss.Actor != null && !_boss.Actor.IsDead &&
                _boss.Combat?.CurrentPattern?.IsChain == true && _player?.Actor?.IsChained == true && _playerBody != null;
            _renderer.enabled = visible;
            if (!visible) return;
            _renderer.sortingLayerID = _source.sortingLayerID;
            _renderer.sortingOrder = _source.sortingOrder + 1;
            Vector3 from = _body.bounds.center + new Vector3(_boss.Combat.Facing * .7f, .3f, -.1f);
            Vector3 to = _playerBody.bounds.center + new Vector3(0, .15f, -.1f);
            Vector3 direction = (to - from).normalized;
            Vector3 normal = new Vector3(-direction.y, direction.x, 0);
            float distance = Vector3.Distance(from, to);
            int count = Mathf.Clamp(Mathf.CeilToInt(distance / .18f), 1, 96);
            float spacing = distance / count;
            _vertices.Clear(); _colors.Clear(); _triangles.Clear();
            for (int link = 0; link < count; link++)
            {
                Vector3 center = Vector3.Lerp(from, to, (link + .5f) / count);
                float halfLength = spacing * .66f, halfWidth = link % 2 == 0 ? .065f : .035f;
                int first = _vertices.Count;
                for (int side = 0; side < 8; side++)
                {
                    float angle = side * Mathf.PI / 4;
                    Vector3 outer = direction * (Mathf.Cos(angle) * halfLength) + normal * (Mathf.Sin(angle) * halfWidth);
                    Vector3 inner = direction * (Mathf.Cos(angle) * Mathf.Max(.01f, halfLength - .025f)) +
                        normal * (Mathf.Sin(angle) * Mathf.Max(.008f, halfWidth - .025f));
                    _vertices.Add(_renderer.transform.InverseTransformPoint(center + outer));
                    _vertices.Add(_renderer.transform.InverseTransformPoint(center + inner));
                    Color color = side < 4 ? new Color(.67f, .53f, .34f) : new Color(.23f, .18f, .13f);
                    _colors.Add(color); _colors.Add(color);
                }
                for (int side = 0; side < 8; side++)
                {
                    int a = first + side * 2, b = first + ((side + 1) % 8) * 2;
                    _triangles.Add(a); _triangles.Add(b); _triangles.Add(a + 1);
                    _triangles.Add(a + 1); _triangles.Add(b); _triangles.Add(b + 1);
                }
            }
            _mesh.Clear(); _mesh.SetVertices(_vertices); _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0); _mesh.RecalculateBounds();
        }
        private void OnDisable() { if (_renderer != null) _renderer.enabled = false; }
        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
