using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Preview;
using UnityEditor;
using UnityEngine;

namespace HumanGhost.EditorTools
{
    // 저장·통계를 연결하지 않고 실제 프리팹과 코어의 종료·이전 콜백을 검증합니다.
    public static class LifetimeAuditVerification
    {
        private const BindingFlags _flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static int _checks;
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, _flags)
            ?? throw new MissingFieldException(target.GetType().Name, name);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static object Call(object target, string name, params object[] args)
            => target.GetType().GetMethod(name, _flags).Invoke(target, args);
        private static void Check(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
            _checks++; Debug.Log("LIFETIME_AUDIT_PASS: " + message);
        }
        private static GameObject Spawn(string name, Transform parent)
            => UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/2.Model/Prefabs/MOP/" + name + ".prefab"), parent);

        public static async void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 Unity 배치 프로세스에서 실행해 주세요.");
            GameObject root = null;
            try
            {
                _checks = 0;
                GameplayPreview.SetTestAttackScale(1.23456f, 10f);
                Check(Mathf.Abs(GameplayPreview.AttackLengthScale - 1.2346f) < .00001f && GameplayPreview.AttackHeightScale == 3f,
                    "시험 공격 배율의 만분율 정수화·상한 보존");
                bool invalidScale = false;
                try { GameplayPreview.SetTestAttackScale(float.NaN, 1f); } catch (ArgumentOutOfRangeException) { invalidScale = true; }
                Check(invalidScale && GameplayPreview.AttackHeightScale == 3f, "비정상 시험 배율은 기존 설정을 손상시키지 않음");
                typeof(GameplayPreview).GetMethod("ResetAttackScale", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                Check(GameplayPreview.AttackLengthScale == 1f && GameplayPreview.AttackHeightScale == 1f, "Play 시작 공격 배율 초기화");
                var assets = AssetDatabase.LoadAssetAtPath<GameDataAssets>("Assets/Data/GameDataAssets.asset");
                var data = await new DataBootstrapper(assets.CreateSource(), path => path).InitializeAsync(CancellationToken.None);
                root = new GameObject("LifetimeAuditScope"); root.SetActive(false);
                var player = Spawn("PlayerGameplay", root.transform).GetComponent<PlayerRuntime>();
                player.Initialize(data, 100, true);
                var body = player.GetComponentsInChildren<SpriteRenderer>(true).First(renderer => renderer.name == "Body");
                var attack = new GameObject("AuditAttack", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                attack.transform.SetParent(root.transform, false);
                var monster = Spawn("JigegwiThrowerGameplay", root.transform).GetComponent<MonsterRuntime>();
                monster.Initialize(data);
                var driverType = typeof(GameplayPreview).Assembly.GetType("HumanGhost.Gameplay.Preview.PreviewMonsterDriver");
                var driver = (IDisposable)Activator.CreateInstance(driverType, _flags, null,
                    new object[] { monster, player, body, attack, 2, 1, 1f, null }, null);
                var oldAttack = (Action<bool>)Field(monster.Actor, "AttackWindowChanged").GetValue(monster.Actor);
                Check(oldAttack != null, "일반몹 판정 이벤트 실제 연결");
                driver.Dispose(); driver.Dispose();
                oldAttack(true); oldAttack(false);
                Check(!(bool)driverType.GetProperty("NeedsStep").GetValue(driver), "종료된 일반몹의 보관 콜백·중복 종료·Step 차단");

                var boss = Spawn("BossGameplay", root.transform).GetComponent<BossRuntime>(); boss.Initialize(data);
                var bossBody = boss.GetComponentsInChildren<SpriteRenderer>(true).First(renderer => renderer.name == "Body");
                var bossDriverType = typeof(GameplayPreview).Assembly.GetType("HumanGhost.Gameplay.Preview.PreviewBossDriver");
                var bossDriver = (IDisposable)Activator.CreateInstance(bossDriverType, _flags, null,
                    new object[] { boss, player, bossBody, body, 1f, 250, 400, 1000 }, null);
                var oldAirborne = (Action<bool>)Field(boss.Combat, "AirborneChanged").GetValue(boss.Combat);
                bossDriver.Dispose(); bossDriver.Dispose();
                UnityEngine.Object.DestroyImmediate(boss.gameObject);
                oldAirborne(true);
                Call(bossDriver, "Step", 0.016f, -10f, 10f);
                Check(true, "보스 파괴 후 이전 공중 콜백·Step이 Unity 객체에 접근하지 않음");

                int sounds = 0;
                var playerAudio = player.GetComponent<PlayerAnimationAudio>() ?? player.gameObject.AddComponent<PlayerAnimationAudio>();
                playerAudio.Connect(player, key => sounds++); Call(playerAudio, "Bind");
                var oldHealth = (Action<int>)Field(player.Actor, "HealthChanged").GetValue(player.Actor);
                playerAudio.Disconnect(); oldHealth(0);
                Check(sounds == 0, "소리 연결 해제 후 보관된 체력 콜백 차단");
                playerAudio.Connect(player, key => sounds++); Call(playerAudio, "Bind");
                oldHealth(0);
                Check(sounds == 0, "재연결 후에도 이전 활성 주기의 콜백 차단");
                playerAudio.Disconnect();

                var gameplayObject = new GameObject("AuditGameplay"); gameplayObject.SetActive(false);
                gameplayObject.transform.SetParent(root.transform, false);
                var gameplay = gameplayObject.AddComponent<GameplayPreview>();
                var progress = new GameProgressState();
                var rewards = new MonsterRewards(data.GetTable<Currency>().All, data.GetTable<MonsterReward>().All, data.GetTable<PlayerLevel>().All);
                progress.ConnectRewards(rewards);
                progress.ConnectUpgrades(new UpgradeSession(rewards.Wallet, data.GetDefinition<ShrineDefinitions>(), data.GetTable<Upgrade>().All));
                Set(gameplay, "_actor", player.Actor); Set(gameplay, "_player", player); Set(gameplay, "_progress", progress);
                Call(gameplay, "ConnectProgress");
                int notifications = 0;
                Action displayed = () => notifications++;
                player.Actor.DisplayChanged += displayed;
                var effectsChanged = (Action)Field(progress.Upgrades, "EffectsChanged").GetValue(progress.Upgrades);
                effectsChanged();
                Check(notifications == 0 && (bool)Field(gameplay, "_upgradesApplyPending").GetValue(gameplay),
                    "강화 완료 통지에서는 플레이어 명령 없이 예약만 함");
                Call(gameplay, "ApplyPendingProgress");
                Check(notifications > 0 && !(bool)Field(gameplay, "_upgradesApplyPending").GetValue(gameplay),
                    "조립자의 순서에서 강화 상태 반영");
                var progressScope = (SubscriptionScope)Field(gameplay, "_progressScope").GetValue(gameplay);
                progressScope.Dispose(); effectsChanged();
                Check(!(bool)Field(gameplay, "_upgradesApplyPending").GetValue(gameplay), "종료한 진행 연결은 다시 예약하지 않음");
                player.Actor.DisplayChanged -= displayed;

                var lifetime = new CancellationTokenSource(); var cancellation = lifetime.Token;
                Set(gameplay, "_saveLifetime", lifetime);
                var failing = new SubscriptionScope();
                failing.Add(() => throw new InvalidOperationException("LIFETIME_AUDIT_INJECTED_FAILURE"));
                Set(gameplay, "_progressScope", failing);
                int reports = 0;
                Application.LogCallback log = (message, trace, type) =>
                { if (type == LogType.Exception && message.Contains("LIFETIME_AUDIT_INJECTED_FAILURE")) reports++; };
                Application.logMessageReceived += log;
                try { Call(gameplay, "OnDisable"); }
                finally { Application.logMessageReceived -= log; }
                Check(reports == 1 && cancellation.IsCancellationRequested && Field(gameplay, "_saveLifetime").GetValue(gameplay) == null,
                    "중간 구독 해제 실패에도 저장 수명 취소·참조 제거 후 오류 보고");
                Check(Field(gameplay, "_motion").GetValue(gameplay) == null && !(bool)Field(gameplay, "_subscribed").GetValue(gameplay),
                    "종료 오류 뒤에도 조립자 수명 종료");
                UnityEngine.Object.DestroyImmediate(root); root = null;
                Debug.Log("LIFETIME_AUDIT_COMPLETE: " + _checks);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorApplication.Exit(1);
            }
        }
    }
}
