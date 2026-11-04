using HumanGhost.Data.Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 전투 HUD(O11 B안)의 기력·약초·결박 가르기·보스 게이지·흐리기입니다. HP 바는 기존 체력 표시 모듈이 맡습니다. 그림은 공급 뒤 교체합니다.
    public sealed class HudScreen : MonoBehaviour
    {
        private const float _dimAlpha = 0.25f;
        private const float _fadeSeconds = 0.3f;
        private const float _damageFlashSeconds = 0.25f;
        [SerializeField] private CanvasGroup _group = null;
        [SerializeField] private RectTransform _staminaFill = null;
        [SerializeField] private Image _staminaBack = null;
        [SerializeField] private Text _herbs = null;
        [SerializeField] private Text _skill = null;
        [SerializeField] private GameObject _bossRoot = null;
        [SerializeField] private Text _bossName = null;
        [SerializeField] private RectTransform _bossHealthFill = null;
        [SerializeField] private RectTransform _bossHealthGhost = null;
        [SerializeField] private RectTransform _bossPoiseFill = null;
        [SerializeField] private Image _damageEdge = null;
        private CanvasGroup[] _extraGroups = new CanvasGroup[0];
        private PlayerActor _player;
        private BossPoise _boss;
        private int _bossMaxPoise;
        private float _bossGhost = 1f;
        private float _bossGhostHold;
        private float _staminaFlash;
        private float _damageFlash;
        private bool _bright;

        public void Connect(PlayerActor player, BossPoise boss, string bossName, int bossMaxPoise, bool showBossGauge, params CanvasGroup[] alsoFade)
        {
            _player = player; _boss = showBossGauge ? boss : null; _bossMaxPoise = bossMaxPoise;
            _extraGroups = alsoFade;
            _bossRoot.SetActive(_boss != null);
            if (_boss != null) _bossName.text = bossName;
            _damageEdge.enabled = false;
        }

        public void Disconnect() { _player = null; _boss = null; }
        public void SetVisible(bool visible) => gameObject.SetActive(visible);
        public void SetBright(bool bright) => _bright = bright;
        public void FlashStamina() => _staminaFlash = 0.4f;
        public void FlashDamage() { _damageFlash = _damageFlashSeconds; _bright = true; }

        private void Update()
        {
            if (_player == null) return;
            float delta = Time.unscaledDeltaTime;
            SetWidth(_staminaFill, _player.Stamina.Max > 0 ? (float)_player.Stamina.Current / _player.Stamina.Max : 0f);
            _staminaFlash = Mathf.Max(0f, _staminaFlash - delta);
            _staminaBack.color = _staminaFlash > 0f && Mathf.FloorToInt(_staminaFlash * 20f) % 2 == 0 ? Color.white : new Color(0.05f, 0.05f, 0.05f, 0.8f);
            _herbs.text = "Q  약초 " + _player.HerbCount;
            _skill.gameObject.SetActive(_player.HasSkill);
            if (_player.HasSkill) _skill.text = "Z  결박 가르기 " + (_player.SkillCooldownRemaining > 0 ? Mathf.CeilToInt(_player.SkillCooldownRemaining / 1000f) + "초" : "");
            UpdateBoss(delta);
            _damageFlash = Mathf.Max(0f, _damageFlash - delta);
            _damageEdge.enabled = _damageFlash > 0f;
            if (_damageEdge.enabled) { Color edge = _damageEdge.color; edge.a = _damageFlash / _damageFlashSeconds * 0.5f; _damageEdge.color = edge; }
            float target = _bright ? 1f : _dimAlpha;
            float alpha = Mathf.MoveTowards(_group.alpha, target, delta / _fadeSeconds);
            _group.alpha = alpha;
            foreach (CanvasGroup group in _extraGroups) if (group != null) group.alpha = alpha;
        }

        private void UpdateBoss(float delta)
        {
            if (_boss == null) return;
            float health = _boss.Definition.HP > 0 ? (float)_boss.Health / _boss.Definition.HP : 0f;
            SetWidth(_bossHealthFill, health);
            // 피해 잔상: 500ms 머문 뒤 줄어듭니다(C11).
            if (health < _bossGhost)
            {
                _bossGhostHold -= delta;
                if (_bossGhostHold <= 0f) _bossGhost = Mathf.MoveTowards(_bossGhost, health, delta);
            }
            else { _bossGhost = health; _bossGhostHold = 0.5f; }
            SetWidth(_bossHealthGhost, _bossGhost);
            SetWidth(_bossPoiseFill, _bossMaxPoise > 0 ? (float)_boss.Poise / _bossMaxPoise : 0f);
        }

        private static void SetWidth(RectTransform fill, float ratio) => fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
    }
}
