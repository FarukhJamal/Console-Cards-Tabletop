using ConsoleCards.GameTemplates;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    /// <summary>
    /// SEAT-1 (owner, 2026-10-23: "two initial ability cards are edge of the table ... have this data as scriptable so
    /// that i can adjust positioning rotation etc later"): where the pieces beside every Console go. All distances are
    /// table units (1 = a card's width, 2.5"), measured from the Console: "toward board" moves a piece along the
    /// Console's depth (0 is level with the Console's centre, less goes toward the player), "outward" moves it away
    /// from the Console along its side. The same values are used for every seat. A layout that no longer fits (pieces
    /// overlapping) is reported in the Console and the built-in places are used.
    /// </summary>
    [CreateAssetMenu(menuName = "Console Cards/Trap Floor/Seat Pieces Layout", fileName = "TrapFloorSeatPieces")]
    public sealed class TrapFloorSeatPiecesDefinition : ScriptableObject
    {
        [Header("Starting Ability cards (dealt beside the Console)")]
        [Tooltip("The side of the Console the row of starting Ability cards goes on (seen from the player).")]
        [SerializeField] private ConsoleSide startingAbilitiesSide = ConsoleSide.Left;
        [Tooltip("Along the Console's depth: 0 is level with the Console's centre, more goes toward the board, less "
            + "toward the player (the table edge).")]
        [SerializeField] private float startingAbilitiesTowardBoard = 0.9f;
        [Tooltip("Moves the whole row along the side: more goes away from the Console, less (negative) closer. 0 is "
            + "just past the pile on that side.")]
        [SerializeField] private float startingAbilitiesOutward;
        [Tooltip("The gap between two starting Ability cards in the row.")]
        [SerializeField, Min(0f)] private float startingAbilitiesGap = 0.2f;
        [Tooltip("Extra turn of each starting Ability card, in degrees, on top of the Console's own turn (a landscape "
            + "card keeps its quarter turn as well).")]
        [SerializeField] private float startingAbilitiesTurn;

        [Header("Hand zone (right of the Console)")]
        [Tooltip("Along the Console's depth: more goes toward the board, less toward the player.")]
        [SerializeField] private float handZoneTowardBoard;
        [Tooltip("Along the side: more goes away from the Console, less closer.")]
        [SerializeField] private float handZoneOutward;

        [Header("Action stack (left of the Console, where extra Abilities go)")]
        [Tooltip("Along the Console's depth: more goes toward the board, less toward the player.")]
        [SerializeField] private float actionStackTowardBoard;
        [Tooltip("Along the side: more goes away from the Console, less closer.")]
        [SerializeField] private float actionStackOutward;

        public ConsoleSide StartingAbilitiesSide => startingAbilitiesSide;
        public float StartingAbilitiesTowardBoard => startingAbilitiesTowardBoard;
        public float StartingAbilitiesOutward => startingAbilitiesOutward;
        public float StartingAbilitiesGap => startingAbilitiesGap;
        public float StartingAbilitiesTurn => startingAbilitiesTurn;
        public float HandZoneTowardBoard => handZoneTowardBoard;
        public float HandZoneOutward => handZoneOutward;
        public float ActionStackTowardBoard => actionStackTowardBoard;
        public float ActionStackOutward => actionStackOutward;
    }
}
