using UnityEngine.InputSystem;

namespace ConsoleCards.Presentation.Input
{
    /// <summary>
    /// POL-0: the table's shortcut keys, read from the TabletopObject action map (TabletopObjectControls.inputactions)
    /// so they can be changed in Unity's Input Actions editor (and later in the game's settings): Ping (middle tap),
    /// Ping modifier (Alt + click), Action modifier (Shift: flick, dice shake), Hand tray (H), Undo (Ctrl+Z) and Redo
    /// (Ctrl+Y, Ctrl+Shift+Z). When an action is missing from the asset, the old key is used, so nothing breaks.
    /// Nothing is allocated per frame.
    /// </summary>
    public sealed class TableKeys
    {
        private InputAction ping;
        private InputAction pingModifier;
        private InputAction actionModifier;
        private InputAction handTray;
        private InputAction undo;
        private InputAction redo;

        /// <summary>Finds and enables the actions in the map (the map the object input adapter reads from).</summary>
        public void Bind(InputActionMap map)
        {
            ping = Find(map, "Ping");
            pingModifier = Find(map, "PingModifier");
            actionModifier = Find(map, "ActionModifier");
            handTray = Find(map, "HandTray");
            undo = Find(map, "Undo");
            redo = Find(map, "Redo");
        }

        public bool PingTapPressedThisFrame => ping != null
            ? ping.WasPressedThisFrame()
            : Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;

        public bool PingTapReleasedThisFrame => ping != null
            ? ping.WasReleasedThisFrame()
            : Mouse.current != null && Mouse.current.middleButton.wasReleasedThisFrame;

        public bool PingModifierHeld => pingModifier != null
            ? pingModifier.IsPressed()
            : Keyboard.current != null && Keyboard.current.altKey.isPressed;

        public bool ActionModifierHeld => actionModifier != null
            ? actionModifier.IsPressed()
            : Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

        public bool HandTrayPressedThisFrame => handTray != null
            ? handTray.WasPressedThisFrame() && !ControlHeld
            : Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame && !ControlHeld;

        // Ctrl+Shift+Z is Redo, so Undo ignores its key while Shift is held.
        public bool UndoPressedThisFrame => undo != null
            ? undo.WasPressedThisFrame() && !ActionModifierHeld
            : Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame && ControlHeld && !ActionModifierHeld;

        public bool RedoPressedThisFrame => redo != null
            ? redo.WasPressedThisFrame()
            : Keyboard.current != null
                && ControlHeld
                && (Keyboard.current.yKey.wasPressedThisFrame
                    || (ActionModifierHeld && Keyboard.current.zKey.wasPressedThisFrame));

        private static bool ControlHeld => Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;

        private static InputAction Find(InputActionMap map, string name)
        {
            InputAction action = map?.FindAction(name);
            if (action != null && !action.enabled) action.Enable();
            return action;
        }
    }
}
