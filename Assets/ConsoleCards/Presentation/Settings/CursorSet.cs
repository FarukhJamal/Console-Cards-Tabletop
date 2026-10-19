using UnityEngine;

namespace ConsoleCards.Presentation.Settings
{
    /// <summary>
    /// POL-0: the table's cursor pictures and their hot spots (the pixel that clicks), in one asset you can edit:
    /// Resources/CursorSet. A picture left empty shows the normal arrow. Pictures are 32 × 32, imported as Cursor.
    /// </summary>
    [CreateAssetMenu(fileName = "CursorSet", menuName = "Console Cards/Look/Cursor Set")]
    public sealed class CursorSet : ScriptableObject
    {
        public const string ResourcePath = "CursorSet";

        [Tooltip("Over a piece you can pick up.")]
        [SerializeField] private Texture2D hand;
        [SerializeField] private Vector2 handHotspot = new Vector2(12f, 3f);
        [Tooltip("While carrying.")]
        [SerializeField] private Texture2D grab;
        [SerializeField] private Vector2 grabHotspot = new Vector2(12f, 8f);
        [Tooltip("While a press on a stack waits to pick the whole stack up.")]
        [SerializeField] private Texture2D stack;
        [SerializeField] private Vector2 stackHotspot = new Vector2(12f, 8f);
        [Tooltip("Over a pile that will not take the card.")]
        [SerializeField] private Texture2D no;
        [SerializeField] private Vector2 noHotspot = new Vector2(16f, 16f);
        [Tooltip("Pinging (the ping key held, or the Ping tool).")]
        [SerializeField] private Texture2D ping;
        [SerializeField] private Vector2 pingHotspot = new Vector2(16f, 27f); // TOOLS-1: the tip of the map pin
        [Tooltip("Aiming a flick.")]
        [SerializeField] private Texture2D aim;
        [SerializeField] private Vector2 aimHotspot = new Vector2(16f, 16f);

        public Texture2D Hand => hand;
        public Vector2 HandHotspot => handHotspot;
        public Texture2D Grab => grab;
        public Vector2 GrabHotspot => grabHotspot;
        public Texture2D Stack => stack;
        public Vector2 StackHotspot => stackHotspot;
        public Texture2D No => no;
        public Vector2 NoHotspot => noHotspot;
        public Texture2D Ping => ping;
        public Vector2 PingHotspot => pingHotspot;
        public Texture2D Aim => aim;
        public Vector2 AimHotspot => aimHotspot;
    }
}
