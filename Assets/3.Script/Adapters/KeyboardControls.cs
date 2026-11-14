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
                        string mapped = null;
                        switch (action.name)
                        {
                            case "Jump": mapped = wasd ? "k" : "c"; break;
                            case "Attack": mapped = wasd ? "j" : "x"; break;
                            case "Roll": mapped = wasd ? "l" : "space"; break;
                            case "Skill": case "Skill1": mapped = wasd ? "u" : "z"; break;
                            case "Herb": case "Heal": mapped = wasd ? "i" : "q"; break;
                            case "Interact": mapped = "f"; break;
                        }
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
