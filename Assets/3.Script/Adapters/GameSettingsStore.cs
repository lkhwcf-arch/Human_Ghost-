using System;
using System.IO;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 설정 파일(O05)입니다. 세이브와 다른 파일이며 없거나 깨지면 기본값으로 시작합니다.
    public static class GameSettingsStore
    {
        [Serializable]
        private sealed class Payload { public int Master = GameSettings.MaxVolume; public int Music = 8; public int Effects = 8; public bool CameraShake = true; public int KeyboardLayout; }

        private static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

        public static GameSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new GameSettings();
                // 이전 파일에 없는 항목은 생성한 기본값을 유지합니다.
                var data = new Payload();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), data);
                var settings = new GameSettings(data.Master, data.Music, data.Effects, data.CameraShake);
                settings.SetKeyboardLayout(data.KeyboardLayout == 1 ? KeyboardLayout.Wasd : KeyboardLayout.Arrows);
                return settings;
            }
            catch (Exception error)
            {
                Debug.LogWarning("설정 파일을 읽지 못해 기본값으로 시작합니다.\n" + error.Message);
                return new GameSettings();
            }
        }

        public static void Save(GameSettings settings)
        {
            var data = new Payload { Master = settings.Master, Music = settings.Music, Effects = settings.Effects, CameraShake = settings.CameraShake, KeyboardLayout = (int)settings.KeyboardLayout };
            AtomicTextFile.Write(FilePath, JsonUtility.ToJson(data));
        }
    }
}
