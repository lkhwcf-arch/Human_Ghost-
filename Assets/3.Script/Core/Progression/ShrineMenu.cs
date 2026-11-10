using System;
using System.Linq;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    public enum ShrinePage { Main, Offering, Herbs }

    // 화면·입력 장치와 독립적인 선택 상태입니다. 구매 규칙은 기존 세션에 위임합니다.
    public class ShrineMenu
    {
        private readonly UpgradeSession _upgrades;
        private readonly Upgrade[] _herbs;
        public ShrineStatDefinition[] Stats { get; }
        public string[] HerbTargets { get; }
        public ShrinePage Page { get; private set; }
        public int Selected { get; private set; }
        public bool Closed { get; private set; }
        public UpgradePurchaseResult? Result { get; private set; }
        public int Count => Page == ShrinePage.Main ? 3 : Page == ShrinePage.Offering ? Stats.Length : HerbTargets.Length;
        public ShrineMenu(UpgradeSession upgrades, ShrineDefinitions definitions, Upgrade[] herbs)
        {
            _upgrades = upgrades; _herbs = herbs;
            Stats = definitions.Stats.Values.Where(row => row.Enabled && UpgradeSession.IsSupportedShrine(row.StatType)).OrderBy(row => row.TextId).ToArray();
            HerbTargets = herbs.Where(row => row.ContentEnabled && UpgradeSession.IsSupportedTable(row.Target))
                .OrderBy(row => row.ID).Select(row => row.Target).Distinct().ToArray();
        }
        public void Select(int index) { if (!Closed && index >= 0 && index < Count) { Selected = index; Result = null; } }
        public void Move(int direction) { if (Count > 0) Select((Selected + Math.Sign(direction) + Count) % Count); }
        public void Submit()
        {
            if (Closed) return;
            if (Page == ShrinePage.Main)
            {
                if (Selected == 2) { Closed = true; return; }
                Page = Selected == 0 ? ShrinePage.Offering : ShrinePage.Herbs;
                Selected = 0; Result = null; return;
            }
            if (Count == 0) return;
            if (Page == ShrinePage.Offering) Result = _upgrades.BuyShrine(Stats[Selected].StatType);
            else
            {
                Upgrade next = NextHerb();
                Result = next == null ? UpgradePurchaseResult.StepLimit : _upgrades.BuyUpgrade(next.ID);
            }
        }
        public void Back()
        {
            if (Closed) return;
            if (Page == ShrinePage.Main) Closed = true;
            else { Page = ShrinePage.Main; Selected = 0; Result = null; }
        }
        public Upgrade NextHerb() => Count == 0 || Page != ShrinePage.Herbs ? null : _herbs.FirstOrDefault(row =>
            row.Target == HerbTargets[Selected] && row.Step == _upgrades.GetTableStep(row.Target) + 1 && row.ContentEnabled);
        public int HerbMaximum(string target) => _herbs.Where(row => row.Target == target && row.ContentEnabled).Select(row => row.Step).DefaultIfEmpty(0).Max();
    }
}
