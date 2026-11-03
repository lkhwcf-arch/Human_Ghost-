using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            new[] { 8001, 8002 }, new[] { 8001 }, new[] { 8101 }, new[] { 18001 }, new[] { 9001 });
        try
        {
            check((await store.ReadAsync()).Count == 0 && !Directory.Exists(directory));
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

            string path = Path.Combine(directory, "slot-7.json");
            string original = File.ReadAllText(path, Encoding.UTF8);
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
        }
        finally
        {
            // 검사에서 직접 만든 임시 폴더만 정리합니다.
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
