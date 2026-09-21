using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    [CreateAssetMenu(fileName = "GridDefinition", menuName = "Console Cards/Definitions/Grid")]
    public sealed class GridDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField, Min(1)] private int rows = 1;
        [SerializeField, Min(1)] private int columns = 1;
        [SerializeField, Min(0.001f)] private float cellWidth = 1f;
        [SerializeField, Min(0.001f)] private float cellHeight = 1f;
        [SerializeField, Min(0f)] private float horizontalSpacing;
        [SerializeField, Min(0f)] private float verticalSpacing;
        [SerializeField] private Vector2 origin;
        [SerializeField, TextArea] private string layoutMetadata;

        public int Rows => rows;
        public int Columns => columns;

        public GridDefinitionData ToData()
        {
            return new GridDefinitionData(
                stableId,
                rows,
                columns,
                cellWidth,
                cellHeight,
                horizontalSpacing,
                verticalSpacing,
                origin.x,
                origin.y,
                layoutMetadata);
        }
    }
}
