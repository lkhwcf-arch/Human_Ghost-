using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 개체의 체세를 표시합니다. 전투 수치·피격·처형 입력은 바꾸지 않습니다.
    public sealed class MonsterPoiseView : MonoBehaviour
    {
        private MonsterRuntime _monster;
        private SpriteRenderer _body, _back, _fill;
        private Transform _root;
        private float _width;

        public void Connect(MonsterRuntime monster, SpriteRenderer body)
        {
            if (_root != null) throw new System.InvalidOperationException("체세 표시가 이미 연결됐습니다.");
            if (monster == null || monster.Actor == null || body == null || body.sprite == null)
                throw new System.ArgumentException("체세 표시 대상과 기준 몸체를 연결해 주세요.");
            _monster = monster; _body = body; _width = monster.ToWorld(IntegerPosition.UnitsPerTile) * .9f;
            _root = new GameObject("MonsterPoise").transform;
            _root.gameObject.SetActive(false);
            _root.SetParent(transform, false);
            _back = Make("Back", body.sprite, new Color(.06f,.04f,.03f,.9f), body.sortingOrder + 20);
            _fill = Make("Fill", body.sprite, new Color(.85f,.65f,.25f,1f), body.sortingOrder + 21);
            _root.gameObject.SetActive(false);
        }
        private SpriteRenderer Make(string name, Sprite sprite, Color color, int order)
        {
            var child = new GameObject(name, typeof(SpriteRenderer));
            child.transform.SetParent(_root, false);
            var renderer = child.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = color;
            renderer.sortingLayerID = _body.sortingLayerID; renderer.sortingOrder = order;
            return renderer;
        }
        private void LateUpdate()
        {
            if (_root == null || _monster == null || _body == null) return;
            var actor = _monster.Actor;
            bool show = actor != null && !actor.IsDead && !actor.IsInvulnerable && actor.Definition.Poise > 0 &&
                (actor.HasAggro || actor.IsExposed || actor.Poise < actor.Definition.Poise);
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;
            _root.position = new Vector3(_body.bounds.center.x, _body.bounds.max.y + .25f, _body.transform.position.z);
            float ratio = Mathf.Clamp01(actor.Poise / (float)actor.Definition.Poise);
            var size = _back.sprite.bounds.size;
            var scale = _root.lossyScale;
            float height = _monster.ToWorld(IntegerPosition.UnitsPerTile) * .06f;
            _back.transform.localScale = new Vector3(_width / size.x / scale.x, height / size.y / scale.y, 1f);
            _fill.transform.localScale = new Vector3(_width * ratio / size.x / scale.x, height / size.y / scale.y, 1f);
            _fill.transform.position = _root.position + Vector3.left * _width * (1f-ratio) * .5f;
        }
        private void OnDisable() { if (_root != null) _root.gameObject.SetActive(false); }
    }
}
