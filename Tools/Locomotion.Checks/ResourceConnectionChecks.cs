using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HumanGhost.Data.Csv;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;
using Newtonsoft.Json.Linq;

internal static class ResourceConnectionChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var assets = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var parser = new CsvParser();
        var npcRows = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/Npc.csv"))).Rows;
        var cutsceneRows = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/CutsceneStep.csv"))).Rows;
        foreach (var step in cutsceneRows)
            if (step.Get("MediaType") == "Video" && StreamingVideoAddress.TryResolve(step.Get("ResourceAddress"), Path.Combine(root, "Assets/StreamingAssets"), out string file))
                check(File.Exists(file) && new FileInfo(file).Length > 0, "컷씬에서 참조하는 영상 파일이 없습니다: " + file);
        foreach (string meta in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.meta", SearchOption.AllDirectories))
        {
            string line = File.ReadLines(meta).Take(3).FirstOrDefault(value => value.StartsWith("guid: ", StringComparison.Ordinal));
            if (line == null) continue;
            string guid = line.Substring(6).Trim();
            if (!assets.TryGetValue(guid, out var paths)) assets.Add(guid, paths = new List<string>());
            paths.Add(meta.Substring(0, meta.Length - 5));
        }
        string Asset(string guid, string location)
        {
            check(assets.TryGetValue(guid, out var paths) && paths.Count == 1 && File.Exists(paths[0]),
                "리소스 연결 오류: " + location + "의 GUID가 없거나 중복됐습니다: " + guid);
            return assets[guid][0];
        }
        string title = File.ReadAllText(Path.Combine(root, "Assets/2.Model/Prefabs/UI/TitleScreen.prefab"));
        Match videoFile = Regex.Match(title, @"(?m)^  _videoFile: (.+)\r?$");
        check(videoFile.Success, "타이틀 영상의 파일 상대 경로가 없습니다.");
        string streamingRoot = Path.GetFullPath(Path.Combine(root, "Assets/StreamingAssets")) + Path.DirectorySeparatorChar;
        string videoPath = Path.GetFullPath(Path.Combine(streamingRoot, videoFile.Groups[1].Value.Trim()));
        check(videoPath.StartsWith(streamingRoot, StringComparison.Ordinal) && File.Exists(videoPath)
            && new FileInfo(videoPath).Length > 0, "타이틀 영상이 StreamingAssets 내부에 없거나 비어 있습니다: " + videoPath);
        var stages = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/Stage.csv"))).Rows
            .ToDictionary(row => int.Parse(row.Get("ID")));
        var monsters = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/Monster.csv"))).Rows
            .Select(row => int.Parse(row.Get("ID"))).ToHashSet();
        string config = File.ReadAllText(Path.Combine(root, "Assets/2.Model/Prefabs/Config/StageContentSet.asset"));
        foreach (Match reference in Regex.Matches(config, @"guid: ([a-f0-9]{32})")) Asset(reference.Groups[1].Value, "StageContentSet");
        string platformSection = config.Split(new[] { "  _platforms:" }, StringSplitOptions.None)[1]
            .Split(new[] { "  _monsters:" }, StringSplitOptions.None)[0];
        var platforms = Regex.Matches(platformSection, @"guid: ([a-f0-9]{32})").Cast<Match>()
            .Select(match => Path.GetFileNameWithoutExtension(Asset(match.Groups[1].Value, "플랫폼 목록"))).ToHashSet(StringComparer.Ordinal);
        var monsterPrefabs = Regex.Matches(config, @"- _code: (\d+)\s+_prefab: \{[^}]*guid: ([a-f0-9]{32})").Cast<Match>()
            .ToDictionary(match => int.Parse(match.Groups[1].Value), match => Asset(match.Groups[2].Value, "몬스터 프리팹 목록"));
        foreach (int code in monsterPrefabs.Keys) check(monsters.Contains(code), "몬스터 프리팹 코드의 CSV 참조가 없습니다: " + code);
        var npcPrefabs = Regex.Matches(config, @"- _key: ([^\r\n]+)\r?\n\s+_prefab: \{[^}]*guid: ([a-f0-9]{32})").Cast<Match>()
            .ToDictionary(match => match.Groups[1].Value.Trim(), match => Asset(match.Groups[2].Value, "NPC 프리팹 목록"));
        foreach (var row in npcRows)
            if (row.Get("ContentEnabled") == "Y")
                check(npcPrefabs.ContainsKey(row.Get("Prefab")), "NPC 테이블의 프리팹 별칭 연결이 없습니다: " + row.Get("Prefab"));
        var stageMaps = new Dictionary<int, string>();
        foreach (Match entry in Regex.Matches(config, @"(?ms)^  - _stageId: (\d+)\r?\n(.*?)(?=^  - _stageId:|^  _platforms:|\z)"))
        {
            int stageId = int.Parse(entry.Groups[1].Value);
            Match mapEntry = Regex.Match(entry.Groups[2].Value, @"_map: \{[^}]*guid: ([a-f0-9]{32})");
            if (mapEntry.Success)
            {
                string mapText = File.ReadAllText(Asset(mapEntry.Groups[1].Value, "스테이지 맵"));
                stageMaps.Add(stageId, mapText);
                foreach (Match npc in Regex.Matches(mapText, @"_npcId: (\d+)"))
                    check(npc.Groups[1].Value == "0" || npcRows.Any(row => row.Get("ID") == npc.Groups[1].Value), "고정 NPC의 테이블 참조가 없습니다.");
            }
            check(stages.ContainsKey(stageId), "StageContentSet의 Stage CSV 참조가 없습니다: " + stageId);
            Match spawn = Regex.Match(entry.Groups[2].Value, @"_spawn: \{[^}]*guid: ([a-f0-9]{32})");
            // 기존 보스 전용 맵처럼 Spawn을 연결하지 않는 실행 경로는 그대로 구분합니다.
            if (!spawn.Success) continue;
            string path = Asset(spawn.Groups[1].Value, "스테이지 " + stageId + " 스폰");
            string declared = Path.GetFullPath(Path.Combine(root, "Assets/Data/Json", stages[stageId].Get("SpawnFile")));
            check(string.Equals(Path.GetFullPath(path), declared, StringComparison.OrdinalIgnoreCase), "Stage.SpawnFile과 실제 스폰 참조가 다릅니다: " + stageId);
            string source = File.ReadAllText(path);
            var tokens = (JContainer)JToken.Parse(source);
            check(!tokens.DescendantsAndSelf().OfType<JValue>().Any(value => value.Type == JTokenType.Float), "스폰 정수 필드에 소수가 있습니다: " + path);
            StageLayout data = StageLayoutReader.Read(source);
            check(data.StageID == stageId, "스폰 JSON의 StageID가 다릅니다: " + path);
            int count = 0;
            foreach (StagePlacement placement in data.Placements)
            {
                check(placement.PlacementID > 0 && platforms.Contains(placement.Prefab), "스폰 플랫폼의 배치 ID·프리팹 연결이 잘못됐습니다: " + path);
                foreach (StageMonsterGroup group in placement.Monsters)
                {
                    check(group.MonsterCount >= 0, "몬스터 수가 음수입니다: " + path);
                    check(monsters.Contains(group.MonsterCode) && monsterPrefabs.ContainsKey(group.MonsterCode), "스폰 몬스터의 CSV·프리팹 연결이 없습니다: " + group.MonsterCode);
                    count = checked(count + group.MonsterCount);
                }
            }
            var warnings = new List<string>();
            StageLayoutRules.ResolveMonsters(data, monsters.Contains, _ => true, warnings);
            check(warnings.Count == 0, "스폰 실행 연결 오류: " + path + " / " + string.Join(" / ", warnings));
            check(data.MaxMonsterCount >= 0 && count <= data.MaxMonsterCount, "실제 스폰 수가 JSON의 최대 수를 넘습니다: " + path);
        }
        // 출구 한 행이 한 방향의 연결입니다. 역방향이나 목적지를 임의로 추론하지 않습니다.
        var exits = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/StageExit.csv"))).Rows;
        foreach (var exit in exits)
        {
            int from = int.Parse(exit.Get("StageID")), to = int.Parse(exit.Get("TargetStageID"));
            // 시연 마무리 출구는 목적지가 없습니다(0). 출발 맵 연결만 확인합니다.
            bool finish = exit.Get("ExitAction") == "FinishDemo";
            check(stages.ContainsKey(from) && (finish ? to == 0 : stages.ContainsKey(to)), "출구의 출발·목적지 Stage 참조가 없습니다: " + exit.Get("ID"));
            if (exit.Get("ContentEnabled") != "Y" || stages[from].Get("ContentEnabled") != "Y") continue;
            check(stageMaps.TryGetValue(from, out string sourceMap), "출구의 출발 맵 연결이 없습니다: " + from);
            string kind = exit.Get("TriggerType");
            check(new[] { "LeftEdge", "RightEdge", "Area", "Interact" }.Contains(kind), "출구의 실행 방식이 잘못됐습니다.");
            if (kind == "Area" || kind == "Interact")
            {
                string anchor = exit.Get("AnchorName");
                check(!string.IsNullOrWhiteSpace(anchor) && Regex.Matches(sourceMap,
                    @"(?m)^  m_Name: " + Regex.Escape(anchor) + @"\r?$").Count == 1,
                    "출구 앵커가 없거나 중복됩니다: " + from + " / " + anchor);
            }
            if (finish || stages[to].Get("ContentEnabled") != "Y") continue;
            string arrival = exit.Get("ArrivalAnchorName");
            check(stageMaps.TryGetValue(to, out string targetMap) && !string.IsNullOrWhiteSpace(arrival)
                && Regex.Matches(targetMap, @"(?m)^  m_Name: " + Regex.Escape(arrival) + @"\r?$").Count == 1,
                "출구의 목적지 앵커가 없거나 중복됩니다: " + from + " → " + to + " / " + arrival);
        }
        string audio = File.ReadAllText(Path.Combine(root, "Assets/2.Model/Prefabs/Config/GameAudio.prefab"));
        var soundRows = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/Sound.csv"))).Rows
            .ToDictionary(row => int.Parse(row.Get("ID")));
        var audioKeys = new HashSet<string>(StringComparer.Ordinal);
        var audioSoundIds = new HashSet<int>();
        var soundAddresses = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match entry in Regex.Matches(audio, @"- _key: ([^\r\n]+)\r?\n\s+_clip: \{[^}]*guid: ([a-f0-9]{32})[^}]*\}\r?\n\s+_soundId: (\d+)"))
        {
            string key = entry.Groups[1].Value.Trim().Trim('"');
            audioKeys.Add(key);
            string clip = Asset(entry.Groups[2].Value, "소리 " + key);
            int id = int.Parse(entry.Groups[3].Value);
            if (id == 0) continue; // 기존 문자열 소리 키는 Sound ID 없이도 동작합니다.
            audioSoundIds.Add(id);
            check(soundRows.ContainsKey(id), "GameAudio의 Sound CSV 참조가 없습니다: " + id);
            string address = soundRows[id].Get("Address");
            check(!string.IsNullOrWhiteSpace(address), "Sound 주소가 비어 있습니다: " + id);
            check(!soundAddresses.TryGetValue(address, out string previous) || previous == clip,
                "같은 Sound 주소에 다른 클립이 연결됐습니다: " + address);
            soundAddresses[address] = clip;
        }
        // 배경음악은 Sound ID(Category 6 BGM)로 지정하고 클립은 GameAudio 소리 목록의 같은 Sound ID 항목이 공급합니다(11/20).
        void CheckMusic(string id, string owner)
        {
            if (string.IsNullOrEmpty(id)) return;
            int soundId = int.Parse(id, CultureInfo.InvariantCulture);
            check(soundRows.TryGetValue(soundId, out var sound) && sound.Get("Category") == "6", owner + "의 배경음악이 Sound Category 6(BGM)이 아닙니다: " + id);
            check(audioSoundIds.Contains(soundId), owner + "의 배경음악 Sound " + id + " 클립이 GameAudio 소리 목록에 없습니다.");
        }
        foreach (var stage in stages.Values)
            foreach (string column in new[] { "BGM_Normal", "BGM_Battle" })
                CheckMusic(stage.Get(column), "Stage[" + stage.Get("ID") + "]." + column);
        Match titleMusic = Regex.Match(title, @"(?m)^  _musicSoundId: (\d+)\r?$");
        check(titleMusic.Success, "타이틀 배경음악 Sound ID 연결을 찾지 못했습니다.");
        if (titleMusic.Success && titleMusic.Groups[1].Value != "0") CheckMusic(titleMusic.Groups[1].Value, "타이틀");
        // 혼·엽전 재화 ID는 코드(MonsterRewards)·서낭당·갈림길 JSON에 함께 있습니다. 서로 같고 Currency에 있어야 합니다(조건 규칙 감사 3.15).
        var currencyIds = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/Currency.csv"))).Rows.Select(row => int.Parse(row.Get("ID"))).ToHashSet();
        int shrineSoul = (int)JObject.Parse(File.ReadAllText(Path.Combine(root, "Assets/Data/Json/shrine/shrine_level.json")))["soulCurrencyId"];
        int galimgilSoul = (int)JObject.Parse(File.ReadAllText(Path.Combine(root, "Assets/Data/Json/galimgil/galimgil_reward.json")))["soulCurrencyId"];
        check(currencyIds.Contains(MonsterRewards.SoulCurrencyId) && currencyIds.Contains(MonsterRewards.CoinCurrencyId)
            && shrineSoul == MonsterRewards.SoulCurrencyId && galimgilSoul == MonsterRewards.SoulCurrencyId,
            "혼·엽전 재화 ID가 MonsterRewards·서낭당·갈림길·Currency 사이에서 다릅니다.");
        // 보스 패턴(BossSkill·BossSkillHit)과 페이즈(BossState)의 교차 검사입니다(11/19 BossPatterns.asset에서 이전).
        var bossSkills = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/BossSkill.csv"))).Rows;
        var bossHits = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/BossSkillHit.csv"))).Rows;
        foreach (JToken boss in JArray.Parse(File.ReadAllText(Path.Combine(root, "Assets/Data/Json/BossState.json"))))
        {
            string bossId = (string)boss["UnitID"];
            var skills = bossSkills.Where(row => row.Get("BossID") == bossId).ToList();
            check(skills.Count > 0, "보스 " + bossId + "의 패턴 행(BossSkill)이 없습니다.");
            foreach (var skill in skills)
            {
                var hits = bossHits.Where(row => row.Get("BossSkillID") == skill.Get("ID")).OrderBy(row => int.Parse(row.Get("ID"))).ToList();
                check(hits.Count > 0, "보스 패턴 " + skill.Get("ID") + "의 타격 행(BossSkillHit)이 없습니다.");
                string sound = skill.Get("SoundKey");
                check(string.IsNullOrEmpty(sound) || audioKeys.Contains(sound), "보스 패턴 " + skill.Get("ID") + "의 SoundKey가 GameAudio에 없습니다: " + sound);
                if (skill.Get("Kind") != "DesperationLeap") continue;
                // 발악 도약 첫 타는 선딜 끝 LeapAirTime 동안 공중입니다. 선딜이 더 짧으면 공중 구간 시작이 음수가 됩니다.
                int firstWindup = int.Parse(hits[0].Get("Windup"));
                foreach (JToken state in boss["States"])
                {
                    int phase = (int)state["Phase"];
                    if (phase < int.Parse(skill.Get("MinPhase")) || phase > int.Parse(skill.Get("MaxPhase"))) continue;
                    check(firstWindup >= (int)state["LeapAirTime"], "보스 패턴 " + skill.Get("ID") + "의 첫 타 Windup(" + firstWindup
                        + ")이 BossState " + (string)state["ID"] + "의 LeapAirTime(" + (int)state["LeapAirTime"] + ")보다 짧습니다.");
                }
            }
            foreach (JToken state in boss["States"])
            {
                CheckMusic((string)state["Music"], "BossState " + (string)state["ID"] + ".Music");
            }
        }
        // 몬스터 패턴(MonsterSkill 패턴 열)과 Monster 정의의 교차 검사입니다(조건 규칙 감사 3.5·3.7).
        // 사용 중인 몬스터마다 패턴 행이 있고, 패턴 사거리는 AttackRange 이하이며, AIType 이름이 실제 패턴 구성과 같아야 합니다.
        var monsterRows = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/Monster.csv"))).Rows;
        var patternRows = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/MonsterSkill.csv"))).Rows;
        foreach (var monster in monsterRows.Where(row => row.Get("ContentEnabled") == "Y"))
        {
            string id = monster.Get("ID");
            var rows = patternRows.Where(row => row.Get("MonsterID") == id).ToList();
            check(rows.Count > 0, "몬스터 " + id + "의 패턴 행(MonsterSkill)이 없습니다.");
            int attackRange = int.Parse(monster.Get("AttackRange"));
            check(rows.All(row => int.Parse(row.Get("Range")) <= attackRange), "몬스터 " + id + "의 패턴 Range가 AttackRange보다 큽니다.");
            string behavior = rows.Any(row => row.Get("Lob") == "Y") ? "LOBBER" : rows.Any(row => row.Get("Projectile") == "Y") ? "THROWER"
                : rows.Any(row => int.Parse(row.Get("ChargeSpeed")) > 0) ? "CHARGER" : "MELEE";
            check(monster.Get("AIType") == behavior, "몬스터 " + id + "의 AIType(" + monster.Get("AIType") + ")이 패턴 구성(" + behavior + ")과 다릅니다.");
        }
        // 상태별 그림 목록(PreviewStateSprite 하위 클래스)에 같은 StateKey가 둘 이상이면 첫 항목만 쓰이고 나머지는 조용히 버려집니다(조건 규칙 감사 3.6).
        // 기본 프리팹·씬의 목록만 봅니다. 프리팹 변형의 개별 덮어쓰기(propertyPath)는 대상이 아닙니다.
        string[] spriteScripts = new[] { "PreviewBossSprite", "PreviewMonsterSprite", "PreviewPlayerSprite" }
            .Select(name => Regex.Match(File.ReadAllText(Path.Combine(root, "Assets/3.Script/Preview/" + name + ".cs.meta")), @"(?m)^guid: ([a-f0-9]{32})").Groups[1].Value)
            .ToArray();
        check(spriteScripts.All(guid => guid.Length == 32), "상태 그림 스크립트 meta의 guid를 읽지 못했습니다.");
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.prefab", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.unity", SearchOption.AllDirectories)))
        {
            string yaml = File.ReadAllText(file);
            if (!spriteScripts.Any(guid => yaml.Contains("guid: " + guid))) continue;
            foreach (Match block in Regex.Matches(yaml, @"(?ms)^--- !u!114 &\d+\r?\n.*?(?=^--- |\z)"))
            {
                if (!spriteScripts.Any(guid => block.Value.Contains("m_Script: {fileID: 11500000, guid: " + guid))) continue;
                foreach (string list in new[] { "_frames", "_animations" })
                {
                    Match section = Regex.Match(block.Value, @"(?ms)^  " + list + @":\r?\n(.*?)(?=^  [^\s-]|\z)");
                    if (!section.Success) continue;
                    string duplicate = Regex.Matches(section.Groups[1].Value, @"(?m)^  - _state: (-?\d+)").Cast<Match>()
                        .GroupBy(match => match.Groups[1].Value).Where(group => group.Count() > 1).Select(group => group.Key).FirstOrDefault();
                    check(duplicate == null, "상태 그림 목록에 같은 StateKey가 여러 번 있습니다: " + Path.GetFileName(file) + " " + list + " " + duplicate);
                }
            }
        }
    }
}
