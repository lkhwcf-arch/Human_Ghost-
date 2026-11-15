using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 전투 HUD(O11 B안)의 기력·약초·결박 가르기·보스 게이지·흐리기입니다. HP 바는 기존 체력 표시 모듈이 맡습니다. 그림은 공급 뒤 교체합니다.
    [DefaultExecutionOrder(1100)]
    public class HudScreen : MonoBehaviour
    {
        private const float _dimAlpha = 0.9f;
        private const int _fadeMilliseconds = 300;
        private const int _damageFlashMilliseconds = 250;
        private MillisecondClock _clock;
        [SerializeField] private CanvasGroup _group = null;
        [SerializeField] private RectTransform _staminaFill = null;
        [SerializeField] private Image _staminaBack = null;
        [SerializeField] private Text _herbs = null;
        [SerializeField] private Text _herbKey = null;
        [SerializeField] private Text _skill = null;
        [SerializeField] private Text _skillName = null;
        [SerializeField] private GameObject _skillIcon = null;
        [SerializeField] private Text _skillKey = null;
        [SerializeField] private GameObject _bossRoot = null;
        [SerializeField] private Text _bossName = null;
        [SerializeField] private RectTransform _bossHealthFill = null;
        [SerializeField] private RectTransform _bossHealthGhost = null;
        [SerializeField] private RectTransform _bossPoiseFill = null;
        [SerializeField] private Image _damageEdge = null;
        private CanvasGroup[] _extraGroups = new CanvasGroup[0];
        private PlayerActor _player;
        private CurrencyWallet _wallet;
        [SerializeField] private Text _soul = null, _coins = null;
        public void ConnectWallet(CurrencyWallet wallet) { Unsubscribe(); _wallet = wallet; if (isActiveAndEnabled) Subscribe(); }
        private LanguageTextSource _text;
        public void ConnectText(LanguageTextSource text) { _text = text; RefreshLanguage(); }
        private void RefreshLanguage() { _shownHerbs = -1; _shownCooldown = -1; _scope?.Invalidate(); }
        private string Ui(int id, string fallback) => _text == null ? fallback : _text.ReadUi(id);
        private BossPoise _boss;
        private int _bossMaxPoise;
        private float _bossGhost = 1f;
        private int _bossGhostHoldMilliseconds;
        private int _staminaFlashMilliseconds;
        private int _damageFlashMillisecondsRemaining;
        private bool _bright;
        private SubscriptionScope _scope;
        private float _bossHealth;
        private int _shownHerbs = -1, _shownCooldown = -1;
        private bool? _shownSkill;

        public void Connect(PlayerActor player, BossPoise boss, string bossName, int bossMaxPoise, bool showBossGauge, params CanvasGroup[] alsoFade)
        {
            Disconnect();
            _player = player; _boss = showBossGauge ? boss : null; _bossMaxPoise = bossMaxPoise;
            _shownHerbs = -1; _shownCooldown = -1; _shownSkill = null;
            _bossGhost = 1f; _bossGhostHoldMilliseconds = 500;
            SetWidth(_bossHealthGhost, _bossGhost);
            _extraGroups = alsoFade;
            _bossRoot.SetActive(_boss != null);
            if (_boss != null) { _bossName.text = bossName; _bossName.fontSize = Mathf.Max(42, _bossName.fontSize); }
            _damageEdge.enabled = false;
            _damageFlashMillisecondsRemaining = _staminaFlashMilliseconds = 0;
            _staminaBack.color = new Color(0.05f, 0.05f, 0.05f, 0.8f);
            if (isActiveAndEnabled) Subscribe();
        }

        public void SetBossName(string text) { if (_bossName != null) _bossName.text = text; }

        public void SetSkillName(string text) { if (_skillName != null) _skillName.text = text; }

        public void Disconnect() { Unsubscribe(); _player = null; _boss = null; _wallet = null; _text = null; _extraGroups = new CanvasGroup[0]; }
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
            _scope.Add(() => KeyboardControls.Changed -= invalidate); KeyboardControls.Changed += invalidate;
            _scope.Add(() => LanguageSettings.Changed -= RefreshLanguage); LanguageSettings.Changed += RefreshLanguage;
            if (_wallet != null)
            {
                CurrencyWallet wallet = _wallet;
                var scope = _scope;
                System.Action<int, int> currency = (id, value) => { if (!scope.IsDisposed) scope.Invalidate(); };
                _scope.Add(() => wallet.Changed -= currency); wallet.Changed += currency;
            }
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
            if (_bossGhost == _bossHealth) _bossGhostHoldMilliseconds = 500;
            _bossHealth = _boss.Definition.HP > 0 ? (float)_boss.Health / _boss.Definition.HP : 0f;
            SetWidth(_bossHealthFill, _bossHealth);
            SetWidth(_bossPoiseFill, _bossMaxPoise > 0 ? (float)_boss.Poise / _bossMaxPoise : 0f);
        }
        public void SetVisible(bool visible) => gameObject.SetActive(visible);
        public void SetBright(bool bright) => _bright = bright;
        public void FlashStamina() => _staminaFlashMilliseconds = 400;
        public void FlashDamage() { _damageFlashMillisecondsRemaining = _damageFlashMilliseconds; _bright = true; }

        private void LateUpdate()
        {
            if (_player == null) return;
            if (_scope != null && _scope.TryConsumeRefresh()) RefreshData();
            int milliseconds = _clock.Advance(Time.unscaledDeltaTime);
            float delta = milliseconds / 1000f;
            if (_staminaFlashMilliseconds > 0)
            {
                _staminaFlashMilliseconds = System.Math.Max(0, _staminaFlashMilliseconds - milliseconds);
                Color color = _staminaFlashMilliseconds > 0 && (_staminaFlashMilliseconds / 50) % 2 == 0 ? Color.white : new Color(0.05f, 0.05f, 0.05f, 0.8f);
                if (_staminaBack.color != color) _staminaBack.color = color;
            }
            UpdateBoss(milliseconds);
            if (_damageFlashMillisecondsRemaining > 0)
            {
                _damageFlashMillisecondsRemaining = System.Math.Max(0, _damageFlashMillisecondsRemaining - milliseconds);
                _damageEdge.enabled = _damageFlashMillisecondsRemaining > 0;
                if (_damageEdge.enabled) { Color edge = _damageEdge.color; edge.a = _damageFlashMillisecondsRemaining / (float)_damageFlashMilliseconds * 0.5f; _damageEdge.color = edge; }
            }
            float target = _bright ? 1f : _dimAlpha;
            float alpha = Mathf.MoveTowards(_group.alpha, target, milliseconds / (float)_fadeMilliseconds);
            if (_group.alpha != alpha) _group.alpha = alpha;
            foreach (CanvasGroup group in _extraGroups) if (group != null && group.alpha != alpha) group.alpha = alpha;
        }
        private void RefreshData()
        {
            if (_wallet != null)
            {
                if (_soul != null) _soul.text = _wallet.GetBalance(MonsterRewards.SoulCurrencyId).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (_coins != null) _coins.text = _wallet.GetBalance(MonsterRewards.CoinCurrencyId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            RefreshStamina(_player.Stamina.Current);
            RefreshBoss();
            if (_shownHerbs != _player.HerbCount) { _shownHerbs = _player.HerbCount; _herbs.text = (_herbKey == null ? (KeyboardControls.Layout == KeyboardLayout.Wasd ? "I  " : "Q  ") + Ui(51602, "약초") + " " : "") + _shownHerbs; }
            if (_herbKey != null) _herbKey.text = KeyboardControls.Layout == KeyboardLayout.Wasd ? "I" : "Q";
            if (_shownSkill != _player.HasSkill)
            {
                _shownSkill = _player.HasSkill; _skill.gameObject.SetActive(_player.HasSkill);
                if (_skillIcon != null) _skillIcon.SetActive(_player.HasSkill);
                if (_skillName != null) _skillName.gameObject.SetActive(false);
                _shownCooldown = -1;
            }
            if (_skillKey != null) _skillKey.transform.parent.gameObject.SetActive(KeyboardControls.Layout == KeyboardLayout.Wasd);
            int cooldown = _player.SkillCooldownSeconds;
            if (_player.HasSkill && cooldown != _shownCooldown)
            {
                _shownCooldown = cooldown;
                _skill.text = (_skillIcon == null ? (KeyboardControls.Layout == KeyboardLayout.Wasd ? "U  " : "Z  ") + Ui(51603, "결박 가르기") + " " : "") +
                    (cooldown > 0 ? string.Format(System.Globalization.CultureInfo.InvariantCulture, Ui(51604, "{0}초"), cooldown) : "");
            }
        }

        private void UpdateBoss(int milliseconds)
        {
            if (_boss == null || _bossGhost == _bossHealth) return;
            float health = _bossHealth;
            // 피해 잔상: 500ms 머문 뒤 줄어듭니다(C11).
            if (health < _bossGhost)
            {
                _bossGhostHoldMilliseconds -= milliseconds;
                if (_bossGhostHoldMilliseconds <= 0) _bossGhost = Mathf.MoveTowards(_bossGhost, health, milliseconds / 1000f);
            }
            else { _bossGhost = health; _bossGhostHoldMilliseconds = 500; }
            SetWidth(_bossHealthGhost, _bossGhost);
        }

        private static void SetWidth(RectTransform fill, float ratio)
        {
            HealthDisplay.SetFillRatio(fill, ratio);
        }
    }
}
