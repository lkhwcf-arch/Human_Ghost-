using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace HumanGhost.Gameplay.Presentation
{
    // 같은 목록·상세 화면으로 혼 강화와 약초 강화를 표시합니다. 구매·저장 규칙은 복제하지 않습니다.
    public class ShrineScreen : MonoBehaviour
    {
        [SerializeField] private Sprite _pipOn = null;
        [SerializeField] private Sprite _pipOff = null;
        [SerializeField, InspectorName("공용 안내 팝업 프리팹")] private ConfirmationPopupScreen _noticePrefab = null;
        [SerializeField, InspectorName("재화 부족 Language ID")] private int _insufficientTextId = 30225;
        [SerializeField, InspectorName("닫기 Language ID")] private int _dismissTextId = 30052;
        private ConfirmationPopupScreen _notice;
        private GameObject _noticeRoot;
        private EventSystem _events;
        private bool _noticeOpen;
        private readonly Dictionary<string, Transform> _nodes = new Dictionary<string, Transform>();
        private ShrineMenu _menu;
        private GameProgressState _progress;
        private PlayerActor _player;
        private IReadOnlyDataTable<Language> _language;
        private Action _closed;
        private Action<Exception> _failed;
        private SubscriptionScope _scope;
        private bool _connected, _blocked, _commanding;
        private int _openedFrame;
        private int _soulCurrencyId;
        public bool IsOpen => _menu != null;
        public void SetBlocked(bool blocked)
        {
            // 오류창을 닫은 결정 입력이 곧바로 강화 구매로 이어지지 않게 합니다.
            if (_blocked && !blocked) _openedFrame = Time.frameCount;
            _blocked = blocked;
        }

        public void Open(IReadOnlyDataCatalog data, GameProgressState progress, PlayerActor player, Action closed, Action<Exception> failed, EventSystem events = null)
        {
            if (IsOpen) throw new InvalidOperationException("서낭당 화면이 이미 열려 있습니다.");
            _progress = progress; _player = player; _closed = closed; _failed = failed;
            _events = events;
            _language = data.GetTable<Language>();
            _soulCurrencyId = data.GetDefinition<ShrineDefinitions>().Level.SoulCurrencyId;
            _menu = new ShrineMenu(progress.Upgrades, data.GetDefinition<ShrineDefinitions>(), data.GetTable<Upgrade>().All.ToArray());
            _openedFrame = Time.frameCount;
            _scope = new SubscriptionScope();
            try
            {
                if (_pipOn == null || _pipOff == null) throw new InvalidOperationException("서낭당 단계 표시 이미지를 연결해 주세요.");
                ConnectPointer();
                Action changed = _scope.Guard(() => { if (!_commanding) RefreshSafely(); });
                progress.Upgrades.EffectsChanged += changed;
                _scope.Add(() => progress.Upgrades.EffectsChanged -= changed);
                Action<int, int> walletChanged = (id, amount) => changed();
                progress.Rewards.Wallet.Changed += walletChanged;
                _scope.Add(() => progress.Rewards.Wallet.Changed -= walletChanged);
                gameObject.SetActive(true);
                Render();
                ScreenVisibility.RequireVisible(GetComponent<Canvas>());
            }
            catch { Hide(); throw; }
        }
        private Transform Node(string path)
        {
            if (!_nodes.TryGetValue(path, out var node))
            {
                node = transform.Find(path);
                if (node == null) throw new InvalidOperationException($"서낭당 화면의 {path} 연결이 없습니다.");
                _nodes.Add(path, node);
            }
            return node;
        }
        private void Label(string path, string value)
        {
            Text text = Node(path).GetComponent<Text>();
            if (text == null) throw new InvalidOperationException($"서낭당 화면의 {path}에 글자 표시가 없습니다.");
            text.text = value;
        }
        private string Text(int id) => _language.Get(id).KO;
        private void ConnectPointer()
        {
            if (_connected) return;
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Pointer($"MenuView/Items/Item_{i}/Label", () => Select(index), () => { Select(index); Submit(); });
            }
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                Pointer($"OfferView/List/Row_{i}/Name", () => Select(index), () => Select(index));
            }
            Pointer("OfferView/Hints/Hint_Confirm/Label", null, Submit);
            foreach (string view in new[] { "MenuView", "OfferView" })
                Pointer(view + "/Hints/Hint_Back/Label", null, Back);
            _connected = true;
        }
        private void Pointer(string path, Action hover, Action click)
        {
            Node(path).GetComponent<Text>().raycastTarget = true;
            MenuText.Connect(Node(path).gameObject, hover, click);
        }
        private void Select(int index) => Execute(() => { if (!_noticeOpen) _menu.Select(index); });
        public void Move(int direction) => Execute(() => { if (_noticeOpen) _notice.Move(direction); else _menu.Move(direction); });
        public void Submit() => Execute(() =>
        {
            if (_noticeOpen) { _notice.Submit(); return; }
            _menu.Submit();
            if (_menu.Result == UpgradePurchaseResult.InsufficientFunds) ShowInsufficientFunds();
        });
        public void Back() => Execute(() => { if (_noticeOpen) _notice.RequestClose(); else _menu.Back(); });
        private void ShowInsufficientFunds()
        {
            if (_noticeOpen) return;
            if (_events == null || !_events.isActiveAndEnabled) throw new InvalidOperationException("서낭당 안내 팝업의 입력 시스템을 연결해 주세요.");
            if (_notice == null)
            {
                _notice = UiPrefabScope.Create(_noticePrefab, transform, out _noticeRoot);
                _noticeRoot.name = "ShrineNoticeScope";
                _notice.gameObject.SetActive(false);
                _notice.Accepted += CloseNotice;
                _notice.CloseRequested += CloseNotice;
                _noticeRoot.SetActive(true);
            }
            _notice.SetNotice(Text(_insufficientTextId), Text(_dismissTextId));
            _notice.FitNoticeWithin(((RectTransform)Node("Panel")).rect.size * 0.8f);
            _noticeOpen = true;
            _notice.Show(_events, 1);
            _openedFrame = Time.frameCount;
        }
        private void CloseNotice()
        {
            if (!_noticeOpen || _blocked || Time.frameCount == _openedFrame) return;
            _noticeOpen = false;
            if (_notice != null) _notice.Hide();
            _openedFrame = Time.frameCount;
        }
        private void Execute(Action command)
        {
            if (!IsOpen || _blocked || _commanding || Time.frameCount == _openedFrame) return;
            try
            {
                _commanding = true;
                command();
                if (_menu.Closed) { Action closed = _closed; Hide(); closed?.Invoke(); }
                else Render();
            }
            catch (Exception error) { Fail(error); }
            finally { _commanding = false; }
        }
        private void RefreshSafely() { try { Render(); } catch (Exception error) { Fail(error); } }
        private void Fail(Exception error)
        {
            Action<Exception> failed = _failed;
            Action closed = _closed;
            Hide(); closed?.Invoke(); failed?.Invoke(error);
        }
        private void Render()
        {
            if (!IsOpen) return;
            bool main = _menu.Page == ShrinePage.Main;
            Node("MenuView").gameObject.SetActive(main);
            Node("OfferView").gameObject.SetActive(!main);
            Label("MenuView/Wallet/HonCount", _progress.Rewards.Wallet.GetBalance(MonsterRewards.SoulCurrencyId).ToString());
            Label("MenuView/Wallet/YeopjeonCount", _progress.Rewards.Wallet.GetBalance(MonsterRewards.CoinCurrencyId).ToString());
            for (int i = 0; i < 3; i++) Node($"MenuView/Items/Item_{i}/Cursor").gameObject.SetActive(main && i == _menu.Selected);
            string key = KeyboardControls.Layout == KeyboardLayout.Wasd ? "K" : "C";
            foreach (string view in new[] { "MenuView", "OfferView" }) Label(view + "/Hints/Hint_Confirm/KeyCap/KeyText", key);
            if (main) return;
            bool offering = _menu.Page == ShrinePage.Offering;
            Label("OfferView/Title", offering ? Text(30222) : "약초 강화");
            Label("OfferView/Wallet/Label", offering ? "지닌 혼" : "지닌 엽전");
            Label("OfferView/Hints/Hint_Confirm/Label", offering ? "바치기" : "강화하기");
            for (int i = 0; i < 5; i++)
            {
                string row = $"OfferView/List/Row_{i}";
                Node(row).gameObject.SetActive(i < _menu.Count);
                if (i >= _menu.Count) continue;
                string target = offering ? _menu.Stats[i].StatType : _menu.HerbTargets[i];
                int step = offering ? _progress.Upgrades.GetShrineStep(target) : _progress.Upgrades.GetTableStep(target);
                Label(row + "/Name", offering ? Text(_menu.Stats[i].TextId) : Text(target == "HERB_CHARGES" ? 30223 : 30224));
                Node(row + "/Select").gameObject.SetActive(i == _menu.Selected);
                Node(row + "/Cursor").gameObject.SetActive(i == _menu.Selected);
                Pips(row + "/Pips", step, offering ? _menu.Stats[i].MaxStep : _menu.HerbMaximum(target));
            }
            if (_menu.Count == 0)
            {
                Label("OfferView/Wallet/Count", _progress.Rewards.Wallet.GetBalance(offering ? _soulCurrencyId : MonsterRewards.CoinCurrencyId).ToString());
                Label("OfferView/Message", "사용 가능한 강화가 없습니다.");
                Node("OfferView/Detail").gameObject.SetActive(false); Node("OfferView/Cost").gameObject.SetActive(false); return;
            }
            Node("OfferView/Detail").gameObject.SetActive(true); Node("OfferView/Cost").gameObject.SetActive(true);
            Upgrade next = offering ? null : _menu.NextHerb();
            ShrineStatDefinition stat = offering ? _menu.Stats[_menu.Selected] : null;
            string selected = offering ? stat.StatType : _menu.HerbTargets[_menu.Selected];
            int currentStep = offering ? _progress.Upgrades.GetShrineStep(selected) : _progress.Upgrades.GetTableStep(selected);
            int currency = offering ? _soulCurrencyId : next?.CurrencyID ?? MonsterRewards.CoinCurrencyId;
            int? cost = offering ? _progress.Upgrades.GetShrineCost() : next?.Price;
            bool limit = offering ? currentStep >= stat.MaxStep || !cost.HasValue : next == null;
            if (limit) cost = null;
            int balance = _progress.Rewards.Wallet.GetBalance(currency);
            Sprite icon = Node(currency == MonsterRewards.SoulCurrencyId ? "MenuView/Wallet/HonIcon" : "MenuView/Wallet/YeopjeonIcon").GetComponent<Image>().sprite;
            foreach (string path in new[] { "OfferView/Wallet/Icon", "OfferView/Cost/CostIcon", "OfferView/Cost/RemainIcon" })
                Node(path).GetComponent<Image>().sprite = icon;
            int increment = offering && stat.Steps.TryGetValue(currentStep + 1, out int value) ? value : next?.Value ?? 0;
            int current = CurrentValue(selected);
            Label("OfferView/Wallet/Count", balance.ToString());
            Label("OfferView/Detail/StatName", offering ? Text(stat.TextId) : Text(selected == "HERB_CHARGES" ? 30223 : 30224));
            Label("OfferView/Detail/StepText", $"{currentStep} / {(offering ? stat.MaxStep : _menu.HerbMaximum(selected))} 단계");
            Pips("OfferView/Detail/StepPips", currentStep, offering ? stat.MaxStep : _menu.HerbMaximum(selected));
            Label("OfferView/Detail/NowValue", Format(selected, current));
            Label("OfferView/Detail/NextValue", limit ? "—" : Format(selected, checked(current + increment)));
            Label("OfferView/Cost/CostLabel", offering ? "바칠 혼" : "필요 엽전");
            Label("OfferView/Cost/RemainLabel", offering ? "남는 혼" : "남는 엽전");
            Label("OfferView/Cost/CostValue", cost?.ToString() ?? "—");
            Label("OfferView/Cost/RemainValue", cost.HasValue && balance >= cost.Value ? (balance - cost.Value).ToString() : "—");
            Label("OfferView/Message", _menu.Result == UpgradePurchaseResult.InsufficientFunds || (!limit && cost > balance)
                ? Text(30225) : limit ? Text(30226) : _menu.Result.HasValue && _menu.Result != UpgradePurchaseResult.Purchased
                    ? "지금은 강화할 수 없습니다." : "");
        }
        private int CurrentValue(string key) => key switch
        {
            "Crit" => _player.CombatStats.Crit, "Dodge" => _player.CombatStats.Dodge,
            "StaminaRegen" => checked(_player.Definition.StaminaRegen + _progress.Upgrades.GetShrineValue(key)),
            "AttackSpeed" => _progress.Upgrades.GetShrineValue(key), "ParryStamina" => _player.ParryStaminaCost,
            "HERB_CHARGES" => _player.HerbCapacity, "HERB_HEAL" => _player.HerbHealing, _ => 0
        };
        private static string Format(string key, int value) => key is "Crit" or "Dodge" or "AttackSpeed"
            ? (key == "AttackSpeed" ? "시간 -" : "") + $"{Math.Max(0, Math.Min(10000, value)) / 100m:0.##}%" : Math.Max(0, value).ToString();
        private void Pips(string path, int step, int maximum)
        {
            Transform root = Node(path);
            while (root.childCount < maximum)
            {
                Transform pip = Instantiate(root.GetChild(0), root);
                pip.name = "Pip_" + (root.childCount - 1);
                var rect = (RectTransform)pip;
                rect.anchoredPosition += new Vector2((root.childCount - 1) * 28f, 0f);
            }
            for (int i = 0; i < root.childCount; i++)
            {
                root.GetChild(i).gameObject.SetActive(i < maximum);
                root.GetChild(i).GetComponent<Image>().sprite = i < step ? _pipOn : _pipOff;
            }
        }
        public void Hide()
        {
            _noticeOpen = false;
            if (_notice != null) _notice.Hide();
            _events = null;
            _scope?.Dispose(); _scope = null; _menu = null; _progress = null; _player = null;
            _closed = null; _failed = null; _language = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
        private void OnDisable() { if (IsOpen) { Action closed = _closed; Hide(); closed?.Invoke(); } }
        private void OnDestroy()
        {
            if (_notice != null) { _notice.Accepted -= CloseNotice; _notice.CloseRequested -= CloseNotice; }
        }
    }
}
