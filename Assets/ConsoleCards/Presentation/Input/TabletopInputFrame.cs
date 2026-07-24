using System;
using UnityEngine;

namespace ConsoleCards.Presentation.Input
{
    public readonly struct TabletopInputFrame
    {
        public TabletopInputFrame(
            Vector2 keyboardPan,
            bool dragHeld,
            Vector2 pointerDelta,
            float scrollDelta,
            Vector2 screenPosition,
            bool selectPressedThisFrame,
            bool selectHeld,
            bool selectReleasedThisFrame,
            bool cancelPressedThisFrame)
        {
            ValidateFinite(keyboardPan, nameof(keyboardPan));
            ValidateFinite(pointerDelta, nameof(pointerDelta));
            ValidateFinite(scrollDelta, nameof(scrollDelta));
            ValidateFinite(screenPosition, nameof(screenPosition));

            KeyboardPan = keyboardPan;
            DragHeld = dragHeld;
            PointerDelta = pointerDelta;
            ScrollDelta = scrollDelta;
            ScreenPosition = screenPosition;
            SelectPressedThisFrame = selectPressedThisFrame;
            SelectHeld = selectHeld;
            SelectReleasedThisFrame = selectReleasedThisFrame;
            CancelPressedThisFrame = cancelPressedThisFrame;
        }

        public Vector2 KeyboardPan { get; }

        public bool DragHeld { get; }

        public Vector2 PointerDelta { get; }

        public float ScrollDelta { get; }

        public Vector2 ScreenPosition { get; }

        public bool SelectPressedThisFrame { get; }

        public bool SelectHeld { get; }

        public bool SelectReleasedThisFrame { get; }

        public bool CancelPressedThisFrame { get; }

        public bool HasPointerTransition =>
            SelectPressedThisFrame ||
            SelectReleasedThisFrame ||
            CancelPressedThisFrame;

        private static void ValidateFinite(Vector2 value, string parameterName)
        {
            ValidateFinite(value.x, parameterName);
            ValidateFinite(value.y, parameterName);
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
