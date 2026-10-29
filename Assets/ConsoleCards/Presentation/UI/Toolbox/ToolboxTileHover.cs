using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ConsoleCards.Presentation.UI.Toolbox
{
    /// <summary>
    /// TOOL-B: tells the Toolbox which tile the pointer is over, so its detail line can say what the piece is. Put on
    /// a tile by the colour-B Toolbox builder; a tile without it works as before.
    /// </summary>
    [RequireComponent(typeof(ToolboxEntryTile))]
    public sealed class ToolboxTileHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("Board 8: the tile looks chosen while the pointer is over it.")]
        [SerializeField] private bool highlight;

        private ToolboxEntryTile tile;
        private Action<ToolboxEntryTile> entered;

        public void Bind(Action<ToolboxEntryTile> onEntered)
        {
            entered = onEntered;
            if (tile == null) tile = GetComponent<ToolboxEntryTile>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tile == null) tile = GetComponent<ToolboxEntryTile>();
            if (highlight && tile != null) tile.SetHovered(true);
            if (entered != null && tile != null) entered(tile);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (highlight && tile != null) tile.SetHovered(false);
        }

        private void OnDisable()
        {
            if (highlight && tile != null) tile.SetHovered(false);
        }
    }
}
