using System.Collections.Generic;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Toolbox (board 8, owner 2026-10-30): "Colour of new pieces". Pawns, tokens and dice placed from the Toolbox
    /// take the chosen colour: your seat colour, white (as made), blue, red, green or yellow. A piece keeps its colour
    /// while the table is up, through Undo and Redo (the views are coloured again when they are made).
    /// </summary>
    public sealed partial class TabletopComposition
    {
        internal const int PieceColourCount = 6;
        internal const int PieceColourWhite = 1;

        private static readonly Color[] PieceColours =
        {
            Color.white, // the seat colour stands in at 0
            Color.white,
            new Color(0.184f, 0.482f, 1f, 1f),
            new Color(1f, 0.231f, 0.231f, 1f),
            new Color(0.184f, 0.831f, 0.420f, 1f),
            new Color(1f, 0.788f, 0.231f, 1f),
        };

        private readonly Dictionary<TabletopObjectId, Color> pieceColours = new Dictionary<TabletopObjectId, Color>();
        private readonly List<Renderer> pieceColourRenderers = new List<Renderer>(16);
        private MaterialPropertyBlock pieceColourBlock;
        private int newPieceColour = PieceColourWhite;

        internal int NewPieceColourIndex => newPieceColour;

        /// <summary>The swatch shown for each choice (0 is the local seat's colour).</summary>
        internal Color PieceColourSwatch(int index) =>
            index == 0 ? FeedbackColour(localPlayerId) : PieceColours[Mathf.Clamp(index, 0, PieceColourCount - 1)];

        internal void ChoosePieceColour(int index)
        {
            newPieceColour = Mathf.Clamp(index, 0, PieceColourCount - 1);
            RefreshPieceColourChoice();
        }

        internal void RefreshPieceColourChoice()
        {
            runtimeUi?.ShowPieceColours(PieceColourSwatch(0), newPieceColour, ChoosePieceColour);
        }

        // A piece the Toolbox just placed takes the chosen colour (white: as made, nothing kept).
        private void RememberPieceColour(CreateTabletopComponentResult result)
        {
            if (!result.Succeeded || result.ObjectId.IsEmpty) return;
            if (result.ComponentKind != TabletopComponentKind.Pawn && result.ComponentKind != TabletopComponentKind.Token
                && result.ComponentKind != TabletopComponentKind.Die)
            {
                return;
            }

            if (newPieceColour == PieceColourWhite) return;
            pieceColours[result.ObjectId] = PieceColourSwatch(newPieceColour);
            if (TryFindPieceView(result.ObjectId, out TabletopObjectView view)) ApplyPieceColour(view);
        }

        private bool TryFindPieceView(TabletopObjectId id, out TabletopObjectView view)
        {
            for (int i = 0; i < Registry.pawnViews.Count; i++)
            {
                if (Registry.pawnViews[i] != null && Registry.pawnViews[i].ObjectId == id) { view = Registry.pawnViews[i]; return true; }
            }

            for (int i = 0; i < Registry.tokenViews.Count; i++)
            {
                if (Registry.tokenViews[i] != null && Registry.tokenViews[i].ObjectId == id) { view = Registry.tokenViews[i]; return true; }
            }

            for (int i = 0; i < Registry.dieViews.Count; i++)
            {
                if (Registry.dieViews[i] != null && Registry.dieViews[i].ObjectId == id) { view = Registry.dieViews[i]; return true; }
            }

            view = null;
            return false;
        }

        /// <summary>Colours a piece's view when it has a Toolbox colour (called when its view is made).</summary>
        internal void ApplyPieceColour(TabletopObjectView view)
        {
            if (view == null || !pieceColours.TryGetValue(view.ObjectId, out Color colour)) return;
            pieceColourBlock ??= new MaterialPropertyBlock();
            pieceColourRenderers.Clear();
            view.GetComponentsInChildren(true, pieceColourRenderers);
            for (int i = 0; i < pieceColourRenderers.Count; i++)
            {
                Renderer renderer = pieceColourRenderers[i];
                if (renderer == null || renderer is LineRenderer || renderer.GetComponent<TextMesh>() != null) continue;
                renderer.GetPropertyBlock(pieceColourBlock);
                pieceColourBlock.SetColor("_BaseColor", colour);
                pieceColourBlock.SetColor("_Color", colour);
                renderer.SetPropertyBlock(pieceColourBlock);
            }
        }
    }
}
