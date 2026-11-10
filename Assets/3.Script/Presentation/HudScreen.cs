using HumanGhost.Data.Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 전투 HUD(O11 B안)의 기력·약초·결박 가르기·보스 게이지·흐리기입니다. HP 바는 기존 체력 표시 모듈이 맡습니다. 그림은 공급 뒤 교체합니다.
    [DefaultExecutionOrder(1100)]
    public class HudScreen : MonoBehaviour
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
        private SubscriptionScope _scope;
        private float _bossHealth;
        private int _shownHerbs = -1, _shownCooldown = -1;
        private bool? _shownSkill;
        private KeyboardLayout? _shownLayout;

        public void Connect(PlayerActor player, BossPoise boss, string bossName, int bossMaxPoise, bool showBossGauge, params CanvasGroup[] alsoFade)
        {
            Disconnect();
            _player = player; _boss = showBossGauge ? boss : null; _bossMaxPoise = bossMaxPoise;
            _shownHerbs = -1; _shownCooldown = -1; _shownSkill = null;
            _bossGhost = 1f; _bossGhostHold = 0.5f;
            SetWidth(_bossHealthGhost, _bossGhost);
            _extraGroups = alsoFade;
            _bossRoot.SetActive(_boss != null);
            if (_boss != null) _bossName.text = bossName;
            _damageEdge.enabled = false;
            _damageFlash = _staminaFlash = 0f;
            _staminaBack.color = new Color(0.05f, 0.05f, 0.05f, 0.8f);
            if (isActiveAndEnabled) Subscribe();
        }

        public void Disconnect() { Unsubscribe(); _player = null; _boss = null; _extraGroups = new CanvasGroup[0]; }
        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Disconnect();

        private void Subscribe()
        {
            if (_scope != null || _player == null) return;
            _scope = new SubscriptionScope();
            System.Action invalidate = _scope.Invalidate;
            PlayerActor player = _player;
            _scope.Add(() => player.DisplayChanged -= invalidate);
            player.DisplayChanged += invalidate;
            _scope.Add(() => HumanGhost.Gameplay.Adapters.KeyboardControls.Changed -= invalidate);
            HumanGhost.Gameplay.Adapters.KeyboardControls.Changed += invalidate;
            if (_boss != null)
            {
                BossPoise boss = _boss;
                _scope.Add(() => boss.Changed -= invalidate); boss.Changed += invalidate;
            }
            _scope.Invalidate();
        }

        private void Unsubscribe()
        {
            _scope?.Dispose(); _scope = null;
        }

        private void RefreshStamina(int value) => SetWidth(_staminaFill, _player.Stamina.Max > 0 ? (float)value / _player.Stamina.Max : 0f);
        private void RefreshBoss()
        {
            if (_boss == null) return;
            if (_bossGhost == _bossHealth) _bossGhostHold = 0.5f;
            _bossHealth = _boss.Definition.HP > 0 ? (float)_boss.Health / _boss.Definition.HP : 0f;
            SetWidth(_bossHealthFill, _bossHealth);
            SetWidth(_bossPoiseFill, _bossMaxPoise > 0 ? (float)_boss.Poise / _bossMaxPoise : 0f);
        }
        public void SetVisible(bool visible) => gameObject.SetActive(visible);
        public void SetBright(bool bright) => _bright = bright;
        public void FlashStamina() => _staminaFlash = 0.4f;
        public void FlashDamage() { _damageFlash = _damageFlashSeconds; _bright = true; }

        private void LateUpdate()
        {
            if (_player == null) return;
            if (_scope != null && _scope.TryConsumeRefresh()) RefreshData();
            float delta = Time.unscaledDeltaTime;
            if (_staminaFlash > 0f)
            {
                _staminaFlash = Mathf.Max(0f, _staminaFlash - delta);
                Color color = _staminaFlash > 0f && Mathf.FloorToInt(_staminaFlash * 20f) % 2 == 0 ? Color.white : new Color(0.05f, 0.05f, 0.05f, 0.8f);
                if (_staminaBack.color != color) _staminaBack.color = color;
            }
            UpdateBoss(delta);
            if (_damageFlash > 0f)
            {
                _damageFlash = Mathf.Max(0f, _damageFlash - delta);
                _damageEdge.enabled = _damageFlash > 0f;
                if (_damageEdge.enabled) { Color edge = _damageEdge.color; edge.a = _damageFlash / _damageFlashSeconds * 0.5f; _damageEdge.color = edge; }
            }
            float target = _bright ? 1f : _dimAlpha;
            float alpha = Mathf.MoveTowards(_group.alpha, target, delta / _fadeSeconds);
            if (_group.alpha != alpha) _group.alpha = alpha;
            foreach (CanvasGroup group in _extraGroups) if (group != null && group.alpha != alpha) group.alpha = alpha;
        }
        private void RefreshData()
        {
            bool wasd = HumanGhost.Gameplay.Adapters.KeyboardControls.Layout == KeyboardLayout.Wasd;
            if (_shownLayout != HumanGhost.Gameplay.Adapters.KeyboardControls.Layout)
            {
                _shownLayout = HumanGhost.Gameplay.Adapters.KeyboardControls.Layout;
                _shownHerbs = _shownCooldown = -1;
            }
            RefreshStamina(_player.Stamina.Current);
            RefreshBoss();
            if (_shownHerbs != _player.HerbCount) { _shownHerbs = _player.HerbCount; _herbs.text = (wasd ? "I" : "Q") + "  약초 " + _shownHerbs; }
            if (_shownSkill != _player.HasSkill) { _shownSkill = _player.HasSkill; _skill.gameObject.SetActive(_player.HasSkill); _shownCooldown = -1; }
            int cooldown = _player.SkillCooldownSeconds;
            if (_player.HasSkill && cooldown != _shownCooldown)
            {
                _shownCooldown = cooldown;
                _skill.text = (wasd ? "U" : "Z") + "  결박 가르기 " + (cooldown > 0 ? cooldown + "초" : "");
            }
        }

        private void UpdateBoss(float delta)
        {
            if (_boss == null || _bossGhost == _bossHealth) return;
            float health = _bossHealth;
            // 피해 잔상: 500ms 머문 뒤 줄어듭니다(C11).
            if (health < _bossGhost)
            {
                _bossGhostHold -= delta;
                if (_bossGhostHold <= 0f) _bossGhost = Mathf.MoveTowards(_bossGhost, health, delta);
            }
            else { _bossGhost = health; _bossGhostHold = 0.5f; }
            SetWidth(_bossHealthGhost, _bossGhost);
        }

        private static void SetWidth(RectTransform fill, float ratio)
        {
            HealthDisplay.SetFillRatio(fill, ratio);
        }
    }
}
