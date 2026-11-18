using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 체력·체세를 따로 표시합니다. 단색 사각형을 쓰며 회복·노출 규칙은 코어에 맡깁니다.
    public sealed class MonsterPoiseView : MonoBehaviour
    {
        private MonsterRuntime _monster;
        private SpriteRenderer _body, _healthBack, _healthFill, _poiseBack, _poiseFill;
        private Transform _root;
        // [정수화 미적용 이유] 체세 막대 Transform 폭 표시값입니다.
        private float _width;
        public void Connect(MonsterRuntime monster, SpriteRenderer body, Sprite rectangle)
        {
            if (_root != null) throw new System.InvalidOperationException("적 막대가 이미 연결됐습니다.");
            if (monster == null || monster.Actor == null || body == null || rectangle == null)
                throw new System.ArgumentException("적 막대의 대상·몸체·단색 사각형을 연결해 주세요.");
            _monster = monster; _body = body; _width = monster.ToWorld(IntegerPosition.UnitsPerTile) * .9f;
            _root = new GameObject("MonsterVitals").transform;
            _root.gameObject.SetActive(false); _root.SetParent(transform, false);
            _healthBack = Make("HealthBack", rectangle, new Color(.05f,.02f,.02f,.95f), body.sortingOrder + 20);
            _healthFill = Make("Health", rectangle, new Color(.9f,.08f,.08f,1f), body.sortingOrder + 21);
            _poiseBack = Make("PoiseBack", rectangle, new Color(.05f,.05f,.05f,.95f), body.sortingOrder + 20);
            _poiseFill = Make("Poise", rectangle, Color.white, body.sortingOrder + 21);
        }
        private SpriteRenderer Make(string name, Sprite sprite, Color color, int order)
        {
            var child = new GameObject(name, typeof(SpriteRenderer)); child.transform.SetParent(_root, false);
            var renderer = child.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color;
            renderer.sortingLayerID = _body.sortingLayerID; renderer.sortingOrder = order; return renderer;
        }
        private void LateUpdate()
        {
            if (_root == null || _monster == null || _body == null) return;
            var actor = _monster.Actor;
            bool show = actor != null && !actor.IsDead && !actor.IsInvulnerable &&
                (actor.HasAggro || actor.IsExposed || actor.Health < actor.Definition.HP || actor.Poise < actor.Definition.Poise);
            _root.gameObject.SetActive(show); if (!show) return;
            float tile = _monster.ToWorld(IntegerPosition.UnitsPerTile);
            _root.position = new Vector3(_body.bounds.center.x, _body.bounds.max.y + .25f * tile, _body.transform.position.z);
            Draw(_healthBack, _healthFill, actor.Health / (float)actor.Definition.HP, .10f * tile, .075f * tile);
            Draw(_poiseBack, _poiseFill, actor.Definition.Poise > 0 ? actor.Poise / (float)actor.Definition.Poise : 0f, 0f, .055f * tile);
        }
        private void Draw(SpriteRenderer back, SpriteRenderer fill, float ratio, float y, float height)
        {
            ratio = Mathf.Clamp01(ratio); var size = back.sprite.bounds.size; var scale = _root.lossyScale;
            back.transform.localPosition = new Vector3(0f, y / scale.y, 0f);
            fill.transform.localPosition = new Vector3(-_width * (1f-ratio) * .5f / scale.x, y / scale.y, -.001f);
            back.transform.localScale = new Vector3(_width / size.x / scale.x, height / size.y / scale.y, 1f);
            fill.transform.localScale = new Vector3(_width * ratio / size.x / scale.x, height / size.y / scale.y, 1f);
        }
        private void OnDisable() { if (_root != null) _root.gameObject.SetActive(false); }
    }
}
