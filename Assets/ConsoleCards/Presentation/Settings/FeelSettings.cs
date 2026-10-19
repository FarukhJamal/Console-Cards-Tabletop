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
        public bool LogMissedClicks => logMissedClicks;
    }
}
