using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 한 사용자의 키보드 설정을 공유하며 실제 액션은 각 소비자가 복제하여 소유합니다.
    public static class KeyboardControls
    {
        public static KeyboardLayout Layout { get; private set; }
        public static event Action Changed;
        public static bool LastDeviceWasGamepad { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            Changed = null;
            Layout = GameSettingsStore.Load().KeyboardLayout;
            LastDeviceWasGamepad = false;
        }

        public static void NoteDevice(InputDevice device)
        {
            if (device is Keyboard || device is Mouse) LastDeviceWasGamepad = false;
            else if (device is Gamepad) LastDeviceWasGamepad = true;
        }

        public static void SetLayout(KeyboardLayout layout)
        {
            if (Layout == layout) return;
            Layout = layout;
            Changed?.Invoke();
        }

        private static string ReadKeyboardKey(string action)
        {
            bool wasd = Layout == KeyboardLayout.Wasd;
            switch (action)
            {
                case "Jump": return wasd ? "k" : "c";
                case "Attack": return wasd ? "j" : "x";
                case "Roll": return wasd ? "l" : "space";
                case "Skill": case "Skill1": return wasd ? "u" : "z";
                case "Herb": case "Heal": return wasd ? "i" : "q";
                case "Interact": return "f";
                default: return null;
            }
        }
        public static string[] ReadActionKeys()
        {
            bool wasd = Layout == KeyboardLayout.Wasd;
            string Key(string action) => ReadKeyboardKey(action).ToUpperInvariant();
            return new[] { wasd ? "W A S D" : "← ↑ ↓ →", Key("Jump"), (wasd ? "S + " : "↓ + ") + Key("Jump"),
                Key("Attack"), Key("Roll"), wasd ? "Right Shift" : "Left Shift", Key("Skill"), Key("Herb"), Key("Interact") };
        }

        public static void Apply(InputActionAsset asset)
        {
            bool wasd = Layout == KeyboardLayout.Wasd;
            foreach (InputActionMap map in asset.actionMaps)
            foreach (InputAction action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (binding.isComposite || binding.path == null || !binding.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string path = binding.path;
                    string key = path.Substring("<Keyboard>/".Length).ToLowerInvariant();
                    if (map.name == "Player")
                    {
                        if (action.name == "Move")
                        {
                            bool arrows = key == "uparrow" || key == "downarrow" || key == "leftarrow" || key == "rightarrow";
                            action.ApplyBindingOverride(i, (wasd ? arrows : !arrows) ? "" : path);
                            continue;
                        }
                        string mapped = ReadKeyboardKey(action.name);
                        if (mapped != null)
                        {
                            bool first = true;
                            for (int previous = 0; previous < i; previous++)
                                if (action.bindings[previous].path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase)) first = false;
                            action.ApplyBindingOverride(i, first ? "<Keyboard>/" + mapped : "");
                        }
                    }
                    else if (map.name == "UI" && action.name == "Submit" && (key == "c" || key == "k"))
                        action.ApplyBindingOverride(i, "<Keyboard>/" + (wasd ? "k" : "c"));
                }
            }
        }
    }
}
