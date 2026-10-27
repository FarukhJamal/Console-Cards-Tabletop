using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ConsoleCards.Presentation.Input
{
    /// <summary>
    /// MP-2 (found while testing online, 2026-10-27): the game keeps running when its window is not in front (online play
    /// needs that: messages keep coming), and on Windows the Input System can still see the mouse and keyboard then, so a
    /// click in another window (another copy of the game, the Editor, a browser) also clicked the game's own buttons and
    /// table. While the game's window is not in front its mouse and keyboard and its UI clicks are switched off; they
    /// come back with focus. The window's focus is read every frame (the focus message alone was missed by a full-screen
    /// window that Windows minimised).
    /// </summary>
    internal sealed class BackgroundInputGuard : MonoBehaviour
    {
        private static BackgroundInputGuard instance;
        private bool off;
        private EventSystem pausedEvents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            GameObject holder = new GameObject("Background Input Guard");
            holder.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(holder);
            instance = holder.AddComponent<BackgroundInputGuard>();
        }

        private void OnApplicationFocus(bool hasFocus) => Apply(hasFocus);

        private void Update()
        {
            bool focused = UnityEngine.Application.isFocused;
            if (focused == off) Apply(focused);
        }

        private void OnDestroy()
        {
            Apply(true);
            if (instance == this) instance = null;
        }

        private void Apply(bool on)
        {
            off = !on;
            SetEnabled(Mouse.current, on);
            SetEnabled(Keyboard.current, on);
            if (!on)
            {
                EventSystem events = EventSystem.current;
                if (events != null && events.enabled)
                {
                    events.enabled = false;
                    pausedEvents = events;
                }
            }
            else if (pausedEvents != null)
            {
                pausedEvents.enabled = true;
                pausedEvents = null;
            }
        }

        private static void SetEnabled(InputDevice device, bool on)
        {
            if (device == null || device.enabled == on) return;
            if (on) InputSystem.EnableDevice(device);
            else InputSystem.DisableDevice(device);
        }
    }
}
