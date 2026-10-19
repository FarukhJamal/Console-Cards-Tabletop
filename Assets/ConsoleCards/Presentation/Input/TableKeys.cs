using UnityEngine;
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
        private InputAction selectModifier;
        private InputAction groupSelection;
        private InputAction cancel;
        private InputAction rotateLeft;
        private InputAction rotateRight;
        private InputAction roll;
        private InputAction deleteSelection;
        private InputAction flip;
        private InputAction deal;

        /// <summary>Finds and enables the actions in the map (the map the object input adapter reads from).</summary>
        public void Bind(InputActionMap map)
        {
            ping = Find(map, "Ping");
            pingModifier = Find(map, "PingModifier");
            actionModifier = Find(map, "ActionModifier");
            handTray = Find(map, "HandTray");
            undo = Find(map, "Undo");
            redo = Find(map, "Redo");
            selectModifier = Find(map, "SelectModifier");
            groupSelection = Find(map, "GroupSelection");
            cancel = Find(map, "Cancel");
            rotateLeft = Find(map, "RotateLeft");
            rotateRight = Find(map, "RotateRight");
            roll = Find(map, "Roll");
            deleteSelection = Find(map, "DeleteSelection");
            flip = Find(map, "Flip");
            deal = Find(map, "Deal");
        }

        /// <summary>POL-2: Q turns the piece under the pointer, the selection or the carried piece left.</summary>
        public bool RotateLeftPressedThisFrame => rotateLeft != null
            ? rotateLeft.WasPressedThisFrame() && !ControlHeld
            : Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame && !ControlHeld;

        /// <summary>POL-2: E turns it right.</summary>
        public bool RotateRightPressedThisFrame => rotateRight != null
            ? rotateRight.WasPressedThisFrame() && !ControlHeld
            : Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && !ControlHeld;

        /// <summary>POL-5: R rolls the die under the pointer (or the selected dice), or shuffles the pile under it.</summary>
        public bool RollPressedThisFrame => roll != null
            ? roll.WasPressedThisFrame() && !ControlHeld
            : Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && !ControlHeld;

        /// <summary>POL-7: Delete removes the selected pieces the Toolbox made.</summary>
        public bool DeletePressedThisFrame => deleteSelection != null
            ? deleteSelection.WasPressedThisFrame()
            : Keyboard.current != null && Keyboard.current.deleteKey.wasPressedThisFrame;

        /// <summary>POL-7: F (the map's Flip), read here to flip every selected card.</summary>
        public bool FlipPressedThisFrame => flip != null
            ? flip.WasPressedThisFrame()
            : Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;

        /// <summary>
        /// POL-4: a number key pressed this frame (1–9 on the top row or the number pad), or 0. Over a deck or stack it
        /// draws that many cards; elsewhere 1–4 keep changing the camera view.
        /// </summary>
        public int NumberPressedThisFrame
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard == null || ControlHeld) return 0;
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return 1;
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return 2;
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return 3;
                if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) return 4;
                if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) return 5;
                if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame) return 6;
                if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame) return 7;
                if (keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame) return 8;
                if (keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame) return 9;
                return 0;
            }
        }

        /// <summary>POL-1: the mouse wheel this frame (notches, up is positive); used to raise a carried piece.</summary>
        public float WheelNotches
        {
            get
            {
                Mouse mouse = Mouse.current;
                if (mouse == null) return 0f;
                float y = mouse.scroll.ReadValue().y;
                return y == 0f ? 0f : Mathf.Sign(y);
            }
        }

        public string RotateLeftName => NameOf(rotateLeft, "Q");
        public string RotateRightName => NameOf(rotateRight, "E");
        public string RollName => NameOf(roll, "R");

        /// <summary>DEAL-1: T over a deck or stack deals it (by the game's rule, or asks).</summary>
        public bool DealPressedThisFrame => deal != null
            ? deal.WasPressedThisFrame() && !ControlHeld
            : Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame && !ControlHeld;

        public string DealName => NameOf(deal, "T");
        public string DeleteName => NameOf(deleteSelection, "Del");

        /// <summary>TOOLS-1: Esc (the map's Cancel): with nothing in progress it clears the selection, then picks Grab.</summary>
        public bool CancelPressedThisFrame => cancel != null
            ? cancel.WasPressedThisFrame()
            : Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        /// <summary>TOOLS-1: Ctrl held: a click adds a card to the selection, a drag draws a selection box.</summary>
        public bool SelectModifierHeld => selectModifier != null
            ? selectModifier.IsPressed()
            : Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;

        /// <summary>TOOLS-1: G groups the selected cards into a deck.</summary>
        public bool GroupSelectionPressedThisFrame => groupSelection != null
            ? groupSelection.WasPressedThisFrame() && !ControlHeld
            : Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame && !ControlHeld;

        /// <summary>The key names as bound now (for the keys strip); read once, not per frame.</summary>
        public string PingModifierName => NameOf(pingModifier, "Alt");
        public string ActionModifierName => NameOf(actionModifier, "Shift");
        public string SelectModifierName => NameOf(selectModifier, "Ctrl");
        public string GroupSelectionName => NameOf(groupSelection, "G");
        public string HandTrayName => NameOf(handTray, "H");

        private static string NameOf(InputAction action, string fallback)
        {
            if (action == null) return fallback;
            string name = action.GetBindingDisplayString();
            return string.IsNullOrEmpty(name) ? fallback : name;
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
