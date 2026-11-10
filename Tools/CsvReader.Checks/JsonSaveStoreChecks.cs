using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;
using Newtonsoft.Json.Linq;

internal static class JsonSaveStoreChecks
{
    public static async Task RunAsync(Action<bool> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "HumanGhostSaveChecks-" + Guid.NewGuid().ToString("N"));
        var store = new JsonSaveStore(directory, 1);
        SaveSnapshot Snapshot(int x) => new SaveSnapshot(8003, 8001, x, -125, 50, 2,
            new[] { 8001, 8002 }, new[] { 8001 }, new[] { 8101 }, new[] { 18001 }, new[] { 9001 },
            new[] { new MonsterSpawnId(8002, 101, 2) }, new RewardSnapshot(120,
                new System.Collections.Generic.Dictionary<int, int> { [1] = 30, [2] = 7 }),
            new UpgradeSnapshot(2, new System.Collections.Generic.Dictionary<string, int> { ["Crit"] = 1 },
                new System.Collections.Generic.Dictionary<string, int> { ["HERB_HEAL"] = 1 }), new GalimgilSnapshot(1, new[] { 101, 101, 102 }));
        try
        {
            check((await store.ReadAsync()).Count == 0 && !Directory.Exists(directory));
            string settings = Path.Combine(directory, "atomic", "settings.json");
            AtomicTextFile.Write(settings, "처음 설정");
            AtomicTextFile.Write(settings, "변경 설정");
            check(File.ReadAllText(settings, Encoding.UTF8) == "변경 설정");
            bool encodingFailed = false;
            try { AtomicTextFile.Write(settings, "\uD800"); } catch (EncoderFallbackException) { encodingFailed = true; }
            check(encodingFailed && File.ReadAllText(settings, Encoding.UTF8) == "변경 설정");
            bool replacementFailed = false;
            using (var locked = new FileStream(settings, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { AtomicTextFile.Write(settings, "실패할 설정"); } catch (IOException) { replacementFailed = true; }
            }
            check(replacementFailed && File.ReadAllText(settings, Encoding.UTF8) == "변경 설정"
                && Directory.GetFiles(Path.GetDirectoryName(settings), "*.tmp").Length == 0);

            var session = new SaveSession(SaveBuildMode.Demo, 1, store, () => 1000, Array.Empty<SaveRecord>());
            check(await session.NewGameAsync(Snapshot(100)));
            check(await session.SaveManualAsync(7, false, Snapshot(350)));
            check(await session.SaveAutomaticAsync(9002, Snapshot(351)));
            check(File.Exists(Path.Combine(directory, "slot-7.json")) && Directory.GetFiles(directory).Length == 2);
            var records = await new JsonSaveStore(directory, 1).ReadAsync();
            var restored = new SaveSession(SaveBuildMode.Demo, 1, store, () => 2000, records);
            check(restored.TryContinue(out var snapshot) && restored.CurrentSlot == 7);
            check(snapshot.PositionX == 351 && snapshot.PositionY == -125 && snapshot.Hp == 50 && snapshot.Herbs == 2);
            check(snapshot.ClearedStages.SequenceEqual(new[] { 8001, 8002 })
                && snapshot.ActiveShrines.Single() == 8001 && snapshot.OpenedGates.Single() == 8101
                && snapshot.ViewedCutscenes.Single() == 18001);
            check(!await restored.SaveAutomaticAsync(9002, Snapshot(999)));
            check(snapshot.DefeatedMonsters.Single().Equals(new MonsterSpawnId(8002, 101, 2)));
            check(snapshot.Rewards.Experience == 120 && snapshot.Rewards.Balances[1] == 30 && snapshot.Rewards.Balances[2] == 7);
            check(snapshot.Galimgil.Claimed == 1 && snapshot.Galimgil.InteractedObjects.SequenceEqual(new[] { 101, 102 }));
            check(snapshot.Upgrades.ShrineLevel == 2 && snapshot.Upgrades.ShrineSteps["Crit"] == 1
                && snapshot.Upgrades.TableSteps["HERB_HEAL"] == 1);
            check(records.Single(record => record.Slot == 7).WithExistingGates(_ => false).Snapshot.Rewards.Experience == 120);

            string path = Path.Combine(directory, "slot-7.json");
            string original = File.ReadAllText(path, Encoding.UTF8);
            var oldCurrency = JObject.Parse(original);
            var balances = (JObject)oldCurrency["Snapshot"]["Rewards"]["Balances"];
            balances.Property("1").Replace(new JProperty("6301", 30));
            balances.Property("2").Replace(new JProperty("6302", 7));
            File.WriteAllText(path, oldCurrency.ToString(), new UTF8Encoding(false));
            var migrated = (await store.ReadAsync()).Single(record => record.Slot == 7).Snapshot.Rewards;
            check(migrated.Balances[1] == 30 && migrated.Balances[2] == 7 && !migrated.Balances.ContainsKey(6301));
            check(File.ReadAllText(path, Encoding.UTF8) == oldCurrency.ToString());
            balances["1"] = 30;
            File.WriteAllText(path, oldCurrency.ToString(), new UTF8Encoding(false));
            bool conflict = false;
            try { await store.ReadAsync(); } catch (InvalidDataException) { conflict = true; }
            check(conflict && File.ReadAllText(path, Encoding.UTF8) == oldCurrency.ToString());
            File.WriteAllText(path, original, new UTF8Encoding(false));
            var legacy = JObject.Parse(original);
            ((JObject)legacy["Snapshot"]).Remove("DefeatedMonsters");
            ((JObject)legacy["Snapshot"]).Remove("Rewards");
            ((JObject)legacy["Snapshot"]).Remove("Upgrades");
            ((JObject)legacy["Snapshot"]).Remove("Galimgil");
            File.WriteAllText(path, legacy.ToString(), new UTF8Encoding(false));
            check((await store.ReadAsync()).Single(record => record.Slot == 7).Snapshot.DefeatedMonsters.Count == 0);
            check((await store.ReadAsync()).Single(record => record.Slot == 7).Snapshot.Rewards.Experience == 0);
            check((await store.ReadAsync()).Single(record => record.Slot == 7).Snapshot.Upgrades.ShrineLevel == 1);
            check((await store.ReadAsync()).Single(record => record.Slot == 7).Snapshot.Galimgil.Claimed == 0);
            check(File.ReadAllText(path, Encoding.UTF8) == legacy.ToString());
            File.WriteAllText(path, original, new UTF8Encoding(false));
            check(!File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }));
            File.WriteAllText(Path.Combine(directory, ".save-interrupted.tmp"), "중단된 임시 기록");
            check((await store.ReadAsync()).Count == 2);

            // 같은 읽기 경계에서 형식 오류들을 확인하고, 실패한 파일이 수정되지 않았는지 검사합니다.
            async Task RejectAsync(string damaged)
            {
                File.WriteAllText(path, damaged, new UTF8Encoding(false));
                bool rejected = false;
                try { await store.ReadAsync(); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected && File.ReadAllText(path, Encoding.UTF8) == damaged);
                File.WriteAllText(path, original, new UTF8Encoding(false));
            }
            await RejectAsync("{");
            foreach (Action<JObject> damage in new Action<JObject>[] {
                document => document.Remove("SaveOrder"),
                document => document["Version"] = 2,
                document => document["Slot"] = 8,
                document => document["Snapshot"]["Hp"] = -1,
                document => document["Snapshot"]["PositionX"] = 1.5,
                document => document["Snapshot"]["Herbs"] = "2",
                document => document["Snapshot"]["UsedSavePoints"] = null,
                document => document["Snapshot"]["DefeatedMonsters"] = null,
                document => document["Snapshot"]["DefeatedMonsters"][0]["Sequence"] = 0,
                document => document["Snapshot"]["StageId"] = long.MaxValue,
                document => document["Unexpected"] = 1
            })
            {
                var document = JObject.Parse(original);
                damage(document);
                await RejectAsync(document.ToString());
            }
            await RejectAsync(original + "{}");
            await RejectAsync(original.Replace("\"Slot\": 7", "\"Slot\": 7, \"Slot\": 7"));
            File.WriteAllBytes(path, new byte[] { 0xff });
            bool invalidEncoding = false;
            try { await store.ReadAsync(); }
            catch (InvalidDataException) { invalidEncoding = true; }
            check(invalidEncoding && File.ReadAllBytes(path).Single() == 0xff);
            File.WriteAllText(path, original, new UTF8Encoding(true));
            check((await store.ReadAsync()).Count == 2);

            bool incompatible = false;
            try { await store.WriteAsync(new SaveRecord(2, 7, 2000, Snapshot(999))); }
            catch (InvalidDataException) { incompatible = true; }
            check(incompatible && File.ReadAllText(path, Encoding.UTF8) == original);

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                bool failed = false;
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    try { await restored.SaveAutomaticAsync(9003, Snapshot(999)); }
                    catch (IOException) { failed = true; }
                }
                check(failed && File.ReadAllText(path, Encoding.UTF8) == original && !restored.IsSaving);
                check(await restored.SaveAutomaticAsync(9003, Snapshot(999)));
                check(Directory.GetFiles(directory, ".save-*.tmp").Select(Path.GetFileName)
                    .SequenceEqual(new[] { ".save-interrupted.tmp" }));
            }
            var resetDirectory = Path.Combine(directory, "reset");
            var resetStore = new JsonSaveStore(resetDirectory, 1);
            check(await resetStore.DeleteLatestAsync() == null && !Directory.Exists(resetDirectory));
            await resetStore.WriteAsync(new SaveRecord(1, 3, 1000, Snapshot(10), 99));
            await resetStore.WriteAsync(new SaveRecord(1, 8, 2000, Snapshot(20), 1));
            await resetStore.WriteAsync(new SaveRecord(1, 2, 2000, Snapshot(30), 2));
            string preserved = File.ReadAllText(Path.Combine(resetDirectory, "slot-3.json"));
            File.WriteAllText(Path.Combine(resetDirectory, "settings.json"), "설정 유지");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                bool stopped = false;
                try { await resetStore.DeleteLatestAsync(cancelled.Token); }
                catch (OperationCanceledException) { stopped = true; }
                check(stopped && (await resetStore.ReadAsync()).Count == 3);
            }
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                bool denied = false;
                using (var locked = new FileStream(Path.Combine(resetDirectory, "slot-2.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    try { await resetStore.DeleteLatestAsync(); }
                    catch (IOException) { denied = true; }
                }
                check(denied && (await resetStore.ReadAsync()).Count == 3);
            }
            SaveRecord removed = await resetStore.DeleteLatestAsync();
            check(removed.Slot == 2 && !File.Exists(Path.Combine(resetDirectory, "slot-2.json")));
            check(File.ReadAllText(Path.Combine(resetDirectory, "slot-3.json")) == preserved
                && File.ReadAllText(Path.Combine(resetDirectory, "settings.json")) == "설정 유지");
            check((await resetStore.DeleteLatestAsync()).Slot == 8);
            check((await resetStore.DeleteLatestAsync()).Slot == 3);
            check(await resetStore.DeleteLatestAsync() == null && File.Exists(Path.Combine(resetDirectory, "settings.json")));
        }
        finally
        {
            // 검사에서 직접 만든 임시 폴더만 정리합니다.
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
