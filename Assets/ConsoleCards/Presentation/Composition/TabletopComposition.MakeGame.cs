using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>MAKE-B: the table's shape for Make a game › WHAT GETS SAVED ("Hexagon").</summary>
    public sealed partial class TabletopComposition
    {
        internal string CurrentTableShapeName => TableShapeName(tableSpec);

        // Doc 63 G1 (owner, 2026-11-21): a made game's own table is used for its run only (the profile's table.json
        // is not changed); true while it is on the table, so the next other table puts the profile's table back.
        private bool madeGameTableShown;

        /// <summary>
        /// Doc 63 G1 (owner, 2026-11-21): called while a new table is made, after the old one is shut down and before
        /// the new one is laid out. A made game that saved its table gets that table; any other table after one goes
        /// back to the profile's table. A joined computer uses the host's table (UseOnlineTableSpec), so it is left.
        /// </summary>
        internal void UseTableForNewTable(PlayerGameFile game)
        {
            if (online != null && (online.IsClientTable || online.ApplyingTable))
            {
                madeGameTableShown = false;
                return;
            }

            if (!FindClassicTable()) return;
            if (game != null && game.hasTable && game.tableSpec != null)
            {
                TableSpec spec = game.tableSpec.Copy();
                spec.Clamp();
                if (ShowTable(spec, out string error))
                {
                    tableSpec = spec;
                    madeGameTableShown = true;
                }
                else
                {
                    Debug.LogWarning($"[{TableArea}] {game.name}'s {TableShapeName(spec)} table could not be built ({error}); the table stays as it is.", this);
                }

                return;
            }

            if (!madeGameTableShown) return;
            madeGameTableShown = false;
            tableSpec = new TableSpec(); // as at start: the profile's table, else Classic
            ApplySavedTable();
        }
    }
}
