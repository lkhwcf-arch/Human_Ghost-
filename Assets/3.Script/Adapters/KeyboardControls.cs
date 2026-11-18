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
        public static string ReadActionKey(string action)
        {
            bool wasd = Layout == KeyboardLayout.Wasd;
            switch (action)
            {
                case "Move": return wasd ? "W A S D" : "← ↑ ↓ →";
                case "DropThrough": return (wasd ? "S + " : "↓ + ") + ReadActionKey("Jump");
                case "Parry": return wasd ? "Right Shift" : "Left Shift";
                case "Execute": case "Shrine": action = "Interact"; break;
            }
            string key = ReadKeyboardKey(action);
            if (key == null) throw new ArgumentException("입력 안내에 연결할 동작이 없습니다: " + action, nameof(action));
            return key.ToUpperInvariant();
        }

        // 이름 있는 키(Shift·Space)는 UI_Lang 문구로 바꿔 언어를 따릅니다. 글자 키·기호는 그대로 둡니다.
        public static string ReadActionKey(string action, Func<int, string> readUi)
        {
            if (readUi == null) throw new ArgumentNullException(nameof(readUi));
            string key = ReadActionKey(action);
            switch (key)
            {
                case "Left Shift": return readUi(51318);
                case "Right Shift": return readUi(51319);
                case "SPACE": return readUi(51320);
                default: return key;
            }
        }

        public static string[] ReadActionKeys()
        {
            return new[] { ReadActionKey("Move"), ReadActionKey("Jump"), ReadActionKey("DropThrough"),
                ReadActionKey("Attack"), ReadActionKey("Roll"), ReadActionKey("Parry"), ReadActionKey("Skill"), ReadActionKey("Herb"), ReadActionKey("Interact") };
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
