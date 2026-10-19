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

        private static FeelSettings current;

        /// <summary>The asset in Resources (or the defaults when there is none), for code without the composition.</summary>
        public static FeelSettings Current
        {
            get
            {
                if (current == null)
                {
                    current = Resources.Load<FeelSettings>(ResourcePath);
                    if (current == null) current = CreateInstance<FeelSettings>();
                }

                return current;
            }
        }

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

        [Header("Carry (POL-1)")]
        [Tooltip("How far a carried piece leans into the movement, in degrees per world unit a second.")]
        [SerializeField, Range(0f, 6f)] private float carryLeanPerSpeed = 1.2f;
        [Tooltip("The most a carried piece leans, in degrees (0 = no lean).")]
        [SerializeField, Range(0f, 25f)] private float carryLeanMax = 7f;
        [Tooltip("How much one mouse-wheel notch raises or lowers a carried piece (world units).")]
        [SerializeField, Range(0f, 1f)] private float carryRaiseStep = 0.12f;
        [Tooltip("The highest a carried piece can be raised with the wheel (world units).")]
        [SerializeField, Range(0f, 5f)] private float carryRaiseMax = 1.5f;
        [Tooltip("Show where a carried piece will land (an outline straight below it, on the board space it snaps to).")]
        [SerializeField] private bool showLandingGhost = true;

        [Header("Drop and settle (POL-2)")]
        [Tooltip("Seconds a piece takes to settle onto a board space or a card centre.")]
        [SerializeField, Range(0.05f, 0.8f)] private float snapGlideSeconds = 0.22f;
        [Tooltip("How far the settle overshoots before it rests (0 = none).")]
        [SerializeField, Range(0f, 3f)] private float snapOvershoot = 1.4f;
        [Tooltip("Degrees one press of Q or E turns a piece.")]
        [SerializeField, Range(1f, 90f)] private float rotateStepDegrees = 15f;
        [Tooltip("Degrees one press of Q or E turns a piece while Shift is held.")]
        [SerializeField, Range(1f, 180f)] private float rotateShiftStepDegrees = 45f;
        [Tooltip("Pawns and tokens let go on a board space or a card settle on its centre.")]
        [SerializeField] private bool pieceSnap = true;
        [Tooltip("Impacts slower than this make no sound (world units a second).")]
        [SerializeField, Range(0f, 5f)] private float impactMinSpeed = 0.5f;
        [Tooltip("Impacts this fast or faster play at full volume.")]
        [SerializeField, Range(0.5f, 20f)] private float impactFullSpeed = 6f;
        [Tooltip("Impacts this fast or faster show a small puff where they hit (0 = never).")]
        [SerializeField, Range(0f, 30f)] private float impactPuffSpeed = 5f;

        [Header("Hover and information (POL-3)")]
        [SerializeField] private bool hoverOutline = true;
        [Tooltip("Seconds the pointer rests on a piece before its tooltip shows.")]
        [SerializeField, Range(0.1f, 3f)] private float tooltipDelay = 0.6f;
        [Tooltip("Seconds Alt is held over a card before it is shown large.")]
        [SerializeField, Range(0f, 1f)] private float peekDelay = 0.12f;

        [Header("Piles (POL-4)")]
        [Tooltip("Seconds a card dropped onto a pile or into a hand takes to slide into place.")]
        [SerializeField, Range(0.05f, 0.8f)] private float dropSettleSeconds = 0.22f;
        [Tooltip("Seconds a held stack takes to lift.")]
        [SerializeField, Range(0f, 0.6f)] private float stackLiftSeconds = 0.12f;
        [Tooltip("How quickly a carried stack follows the pointer (higher = tighter).")]
        [SerializeField, Range(4f, 60f)] private float stackFollow = 20f;
        [Tooltip("How strongly a carried stack is pulled toward the stack it will merge onto (0 = not at all).")]
        [SerializeField, Range(0f, 1f)] private float stackMagnetPull = 0.35f;
        [Tooltip("Spread: space between cards in a row, in card widths.")]
        [SerializeField, Range(0.3f, 2f)] private float spreadSpacing = 1.08f;
        [Tooltip("Spread: cards in a row before the next row starts.")]
        [SerializeField, Range(2, 26)] private int spreadRowLength = 10;

        [Header("Dice and flick (POL-5)")]
        [Tooltip("Seconds a die's result floats above it after a roll.")]
        [SerializeField, Range(0f, 5f)] private float dieResultSeconds = 1.6f;
        [Tooltip("How fast (pixels a second) the mouse must swing back and forth for a carried die to shake.")]
        [SerializeField, Range(200f, 6000f)] private float mouseShakeSpeed = 1400f;
        [Tooltip("Flick strength curve: 1 = straight, above 1 = gentler short pulls, stronger long ones.")]
        [SerializeField, Range(0.5f, 3f)] private float flickCurve = 1.4f;
        [SerializeField, Range(0f, 1f)] private float flickChargeVolume = 0.3f;

        [Header("Sound")]
        [SerializeField, Range(0f, 1f)] private float cardSoundVolume = 0.5f;
        [Tooltip("Volume of every table sound (POL-8).")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.85f;
        [Tooltip("Volume of impacts (pieces hitting the table and each other).")]
        [SerializeField, Range(0f, 1f)] private float impactVolume = 0.6f;
        [Tooltip("How much each sound's pitch varies, so no two taps are alike.")]
        [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.07f;
        [Tooltip("When many sounds play at once each is this much quieter per extra sound.")]
        [SerializeField, Range(0.2f, 1f)] private float crowdDamping = 0.7f;

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
        public float CarryLeanPerSpeed => carryLeanPerSpeed;
        public float CarryLeanMax => carryLeanMax;
        public float CarryRaiseStep => carryRaiseStep;
        public float CarryRaiseMax => carryRaiseMax;
        public bool ShowLandingGhost => showLandingGhost;
        public float SnapGlideSeconds => snapGlideSeconds;
        public float SnapOvershoot => snapOvershoot;
        public float RotateStepDegrees => rotateStepDegrees;
        public float RotateShiftStepDegrees => rotateShiftStepDegrees;
        public bool PieceSnap => pieceSnap;
        public float ImpactMinSpeed => impactMinSpeed;
        public float ImpactFullSpeed => impactFullSpeed;
        public float ImpactPuffSpeed => impactPuffSpeed;
        public bool HoverOutline => hoverOutline;
        public float TooltipDelay => tooltipDelay;
        public float PeekDelay => peekDelay;
        public float DropSettleSeconds => dropSettleSeconds;
        public float StackLiftSeconds => stackLiftSeconds;
        public float StackFollow => stackFollow;
        public float StackMagnetPull => stackMagnetPull;
        public float SpreadSpacing => spreadSpacing;
        public int SpreadRowLength => spreadRowLength;
        public float DieResultSeconds => dieResultSeconds;
        public float MouseShakeSpeed => mouseShakeSpeed;
        public float FlickCurve => flickCurve;
        public float FlickChargeVolume => flickChargeVolume;
        public float MasterVolume => masterVolume;
        public float ImpactVolume => impactVolume;
        public float PitchJitter => pitchJitter;
        public float CrowdDamping => crowdDamping;
        public bool LogMissedClicks => logMissedClicks;
    }
}
