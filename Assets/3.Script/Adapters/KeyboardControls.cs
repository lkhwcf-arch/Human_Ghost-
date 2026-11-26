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

        private static string ReadKeyboardKey(string action, KeyboardLayout layout)
        {
            if (layout == KeyboardLayout.Skul)
            {
                switch (action)
                {
                    case "Jump": return "c";
                    case "Attack": return "x";
                    case "Roll": return "z";
                    case "Parry": return "s";
                    case "Skill": case "Skill1": return "a";
                    case "Herb": case "Heal": return "d";
                    case "Interact": return "f";
                }
            }
            bool wasd = layout == KeyboardLayout.Wasd;
            switch (action)
            {
                case "Jump": return wasd ? "space" : "z";
                case "Attack": return wasd ? "k" : "x";
                case "Roll": return wasd ? "leftShift" : "c";
                case "Parry": return wasd ? "j" : "leftShift";
                case "Skill": case "Skill1": return wasd ? "q" : "d";
                case "Herb": case "Heal": return wasd ? "f" : "a";
                case "Interact": return wasd ? "e" : "f";
                default: return null;
            }
        }
        internal static string KeyboardBinding(string action, KeyboardLayout layout)
        {
            string key = ReadKeyboardKey(action, layout);
            return key == null ? null : "<Keyboard>/" + key;
        }

        public static string ReadActionKey(string action)
        {
            bool wasd = Layout == KeyboardLayout.Wasd;
            switch (action)
            {
                case "Move": return wasd ? "W A S D" : "← ↑ ↓ →";
                case "CameraLook": return wasd ? "Num 8 / 2, W / S" : "↑ / ↓";
                case "DropThrough": return (wasd ? "S + " : "↓ + ") + ReadActionKey("Jump");
                case "Execute": case "Shrine": action = "Interact"; break;
            }
            string key = ReadKeyboardKey(action, Layout);
            if (key == null) throw new ArgumentException("입력 안내에 연결할 동작이 없습니다: " + action, nameof(action));
            return key == "leftShift" ? "Left Shift" : key.ToUpperInvariant();
        }

        // 이름 있는 키(Shift·Space)는 UI_Lang 문구로 바꿔 언어를 따릅니다. 글자 키·기호는 그대로 둡니다.
        public static string ReadActionKey(string action, Func<int, string> readUi)
        {
            if (readUi == null) throw new ArgumentNullException(nameof(readUi));
            if (action == "DropThrough")
                return (Layout == KeyboardLayout.Wasd ? "S + " : "↓ + ") + ReadActionKey("Jump", readUi);
            string key = ReadActionKey(action);
            switch (key)
            {
                case "Left Shift": return readUi(51318);
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
            InputActionMap player = asset.FindActionMap("Player");
            if (player == null) return;
            bool wasd = Layout == KeyboardLayout.Wasd;
            foreach (InputAction action in player.actions)
            {
                string mapped = KeyboardBinding(action.name, Layout);
                bool mappedKeyboard = false;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    string path = binding.path;
                    if (binding.isComposite || path == null || !path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (action.name == "Move")
                    {
                        bool arrows = path.EndsWith("Arrow", StringComparison.OrdinalIgnoreCase);
                        action.ApplyBindingOverride(i, (wasd ? arrows : !arrows) ? "" : path);
                    }
                    else if (mapped != null)
                    {
                        action.ApplyBindingOverride(i, mappedKeyboard ? "" : mapped);
                        mappedKeyboard = true;
                    }
                }
            }
        }
    }
}
