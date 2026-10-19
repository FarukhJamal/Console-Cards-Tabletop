using UnityEngine;

namespace ConsoleCards.Presentation.Settings
{
    /// <summary>
    /// POL-0: the numbers that decide how the table feels, in one asset you can change while playing (Play mode
    /// picks up a change at once): Resources/FeelSettings. Without the asset the defaults below are used.
    /// Later polish stages add their numbers here.
    /// </summary>
    [CreateAssetMenu(fileName = "FeelSettings", menuName = "Console Cards/Feel/Feel Settings")]
    public sealed class FeelSettings : ScriptableObject
    {
        public const string ResourcePath = "FeelSettings";

        [Header("Carry a whole stack")]
        [Tooltip("Seconds a still press on a stack card waits before the whole stack is picked up.")]
        [SerializeField, Range(0.15f, 1.5f)] private float stackHoldSeconds = 0.45f;
        [Tooltip("How high the carried stack rides (world units).")]
        [SerializeField, Range(0f, 2f)] private float stackCarryLift = 0.35f;
        [Tooltip("How close (in card widths) the carried stack's centre must be to another stack to merge onto it.")]
        [SerializeField, Range(0.2f, 3f)] private float stackMergeReach = 0.9f;

        [Header("Table ping")]
        [SerializeField, Range(0.5f, 6f)] private float pingSeconds = 2f;
        [SerializeField, Range(0f, 1f)] private float pingVolume = 0.55f;
        [Tooltip("A middle-button press shorter than this is a ping (longer is a camera drag).")]
        [SerializeField, Range(0.1f, 0.8f)] private float pingTapSeconds = 0.3f;

        [Header("Flick")]
        [Tooltip("Pulling back this many card widths is full strength.")]
        [SerializeField, Range(0.5f, 8f)] private float flickFullPull = 3f;
        [Tooltip("Below this share of full strength the flick does nothing.")]
        [SerializeField, Range(0f, 0.5f)] private float flickDeadZone = 0.06f;
        [Tooltip("Slide speed at the weakest flick, in card widths a second.")]
        [SerializeField, Range(0.5f, 10f)] private float flickMinSpeed = 2f;
        [Tooltip("Slide speed at full strength, in card widths a second.")]
        [SerializeField, Range(2f, 40f)] private float flickMaxSpeed = 16f;

        [Header("Dice shake")]
        [SerializeField, Range(0.04f, 0.4f)] private float diceRattleInterval = 0.11f;
        [SerializeField, Range(0f, 1f)] private float diceRattleVolume = 0.4f;

        [Header("Shuffle (SMOOTH-1: a quick riffle)")]
        [Tooltip("Seconds the two halves take to slide apart.")]
        [SerializeField, Range(0.05f, 0.6f)] private float shuffleSplitSeconds = 0.16f;
        [Tooltip("Seconds each card takes to drop back onto the pile.")]
        [SerializeField, Range(0.05f, 0.6f)] private float shuffleDropSeconds = 0.14f;
        [Tooltip("Seconds between one card dropping and the next.")]
        [SerializeField, Range(0f, 0.08f)] private float shuffleStagger = 0.018f;
        [Tooltip("The drops never take longer than this in all (big decks drop faster).")]
        [SerializeField, Range(0.1f, 1.5f)] private float shuffleMaxStagger = 0.4f;
        [Tooltip("How far each half slides out, in card widths.")]
        [SerializeField, Range(0f, 1.5f)] private float shuffleSpread = 0.6f;
        [Tooltip("How high the halves lift, in card widths.")]
        [SerializeField, Range(0f, 1f)] private float shuffleLift = 0.12f;
        [Tooltip("How far the halves tip toward each other, in degrees.")]
        [SerializeField, Range(0f, 30f)] private float shuffleTilt = 9f;

        [Header("Cards moving between piles (SMOOTH-1: split, Split into, merge, draw)")]
        [Tooltip("Seconds a card takes to glide to its new pile.")]
        [SerializeField, Range(0.05f, 1f)] private float pileMoveSeconds = 0.3f;
        [Tooltip("Seconds between one card leaving and the next.")]
        [SerializeField, Range(0f, 0.1f)] private float pileMoveStagger = 0.022f;
        [Tooltip("The staggering never takes longer than this in all.")]
        [SerializeField, Range(0f, 1f)] private float pileMoveMaxStagger = 0.3f;
        [Tooltip("How high a card arcs on the way, in card widths.")]
        [SerializeField, Range(0f, 1.5f)] private float pileMoveArc = 0.3f;
        [Tooltip("Seconds a drawn card takes to fly to the hand.")]
        [SerializeField, Range(0.05f, 1f)] private float drawSeconds = 0.32f;
        [Tooltip("How high a drawn card arcs, in card widths.")]
        [SerializeField, Range(0f, 1.5f)] private float drawArc = 0.4f;

        [Header("Sound")]
        [SerializeField, Range(0f, 1f)] private float cardSoundVolume = 0.5f;

        [Header("Diagnostics")]
        [Tooltip("Writes a line to the Console (and Editor.log) when a click on the table did not pick anything up, "
            + "and why. Helps find clicks that need a second try.")]
        [SerializeField] private bool logMissedClicks = true;

        public float StackHoldSeconds => stackHoldSeconds;
        public float StackCarryLift => stackCarryLift;
        public float StackMergeReach => stackMergeReach;
        public float PingSeconds => pingSeconds;
        public float PingVolume => pingVolume;
        public float PingTapSeconds => pingTapSeconds;
        public float FlickFullPull => flickFullPull;
        public float FlickDeadZone => flickDeadZone;
        public float FlickMinSpeed => flickMinSpeed;
        public float FlickMaxSpeed => flickMaxSpeed;
        public float DiceRattleInterval => diceRattleInterval;
        public float DiceRattleVolume => diceRattleVolume;
        public float CardSoundVolume => cardSoundVolume;
        public float ShuffleSplitSeconds => shuffleSplitSeconds;
        public float ShuffleDropSeconds => shuffleDropSeconds;
        public float ShuffleStagger => shuffleStagger;
        public float ShuffleMaxStagger => shuffleMaxStagger;
        public float ShuffleSpread => shuffleSpread;
        public float ShuffleLift => shuffleLift;
        public float ShuffleTilt => shuffleTilt;
        public float PileMoveSeconds => pileMoveSeconds;
        public float PileMoveStagger => pileMoveStagger;
        public float PileMoveMaxStagger => pileMoveMaxStagger;
        public float PileMoveArc => pileMoveArc;
        public float DrawSeconds => drawSeconds;
        public float DrawArc => drawArc;
        public bool LogMissedClicks => logMissedClicks;
    }
}
