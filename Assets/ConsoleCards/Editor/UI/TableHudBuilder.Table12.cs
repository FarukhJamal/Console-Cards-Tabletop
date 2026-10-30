using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// Board 12 "Table" (owner, 2026-10-30), measured from the board (1280 x 720, x1.5), in Final UI/Notices
    /// (TablePanel12): the top bar (TABLE, the saved tables picker, UNDO, REDO); on the left the table itself, seen
    /// through a frame ("FROM ABOVE · drag the edge to resize", the size handles on its edge); on the right SHAPE (six
    /// tiles with their outline, the chosen one teal), WIDTH and DEPTH with − / +, the other sizes the shape has, the
    /// EDGE and RESET; BACK bottom left, APPLY bottom right. Made once, then kept.
    /// </summary>
    public static partial class TableHudBuilder
    {
        public const string Table12Path = Folder + "TablePanel12.prefab";

        private static void BuildTable12()
        {
            frame = frame != null ? frame : HudSprite("HudFrame", new Vector4(16f, 16f, 16f, 16f));
            hex = hex != null ? hex : HudSprite("HudHex", Vector4.zero);
            ForgetOlderBuild(Table12Path, EditorsLabel);
            Save<TablePanelView>(Table12Path, "TablePanel12", BuildTablePanel12);
            MarkBuild(Table12Path, EditorsLabel);
            AssignCatalogEntry(UiPrefabIds.TablePanel, AssetDatabase.LoadAssetAtPath<TablePanelView>(Table12Path),
                UiLayer.Modal, UiRetention.CachedSingleInstance);
        }

        private static void RestoreTable12() =>
            Restore<TablePanelView>(UiPrefabIds.TablePanel, GameplayUiBuilder.TablePanelPath, UiRetention.CachedSingleInstance);

        private static void BuildTablePanel12(RectTransform root, TablePanelView view)
        {
            // The page around the frame (the table shows only through the frame, as on the board).
            Mask12(root, "PageTop", 0, 0, 1280, 92);
            Mask12(root, "PageBottom", 0, 628, 1280, 92);
            Mask12(root, "PageLeft", 0, 92, 24, 536);
            Mask12(root, "PageRight", 784, 92, 496, 536);

            // The size handles (over the page, under the bars and the panel).
            RectTransform layer = Rect("Handles", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            TableHandleView[] handles = new TableHandleView[TablePanelModel.HandleCount];
            for (int i = 0; i < handles.Length; i++)
            {
                RectTransform dot = Rect($"Handle{i + 1}", layer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(27f, 27f));
                Image dotFill = dot.gameObject.AddComponent<Image>();
                dotFill.sprite = rounded;
                dotFill.type = Image.Type.Sliced;
                dotFill.color = Words;
                Edges(dot, Cyan);
                handles[i] = dot.gameObject.AddComponent<TableHandleView>();
                dot.gameObject.SetActive(false);
            }

            // The top bar.
            RectTransform header = EditorHeader(root, string.Empty, "TABLE", out _);
            ((RectTransform)header.Find("Title")).anchoredPosition = new Vector2(45f * P, 0f);
            RectTransform models = Rect10(header, "Saved", 560, 0, 470, 56);
            Txt(models, "Title", 0, 0, 120, 56, medium, 13, Muted, TextAnchor.MiddleRight, "Saved tables");
            Button previousModel = Btn(models, "Previous", 132, 11, 34, 34, "‹", 13);
            Text modelLabel = Txt(models, "Model", 170, 0, 150, 56, bold, 13, Words, TextAnchor.MiddleCenter, "HEX LOUNGE");
            modelLabel.resizeTextForBestFit = true;
            modelLabel.resizeTextMinSize = 12;
            modelLabel.resizeTextMaxSize = 20;
            Button nextModel = Btn(models, "Next", 324, 11, 34, 34, "›", 13);
            Button undo = Btn(header, "Undo", 1040, 11, 76, 34, "UNDO", 12);
            Button redo = Btn(header, "Redo", 1122, 11, 76, 34, "REDO", 12);

            // The table, seen through a frame.
            RectTransform view3d = Box10(root, "FromAbove", 24, 92, 760, 536, new Color(0f, 0f, 0f, 0f), Edge);
            view3d.GetComponent<Image>().raycastTarget = false;
            Txt(view3d, "Caption", 14, 12, 500, 16, bold, 11, Muted, TextAnchor.MiddleLeft, "FROM ABOVE · drag the edge to resize");

            // The settings.
            RectTransform panel = Box10(root, "Settings", 800, 92, 456, 536, EdPane, Edge);
            Swallow(panel);
            Txt(panel, "ShapeTitle", 17, 17, 300, 16, bold, 11, Muted, TextAnchor.MiddleLeft, "SHAPE");
            string[] shapes = { "CLASSIC", "RECTANGLE", "SQUARE", "ROUND", "OVAL", "HEXAGON" };
            Button[] shapeButtons = new Button[6];
            Image[] shapeFills = new Image[6];
            Image[] shapeIcons = new Image[6];
            // The shape tiles share one parent: the view shows or hides it (LAYOUT-1, Arrange table has no shapes).
            RectTransform shapeHolder = Rect10(panel, "Shapes", 0, 0, 456, 220);
            for (int i = 0; i < 6; i++)
            {
                float x = 17 + ((i % 3) * 143.5f);
                float y = 43 + ((i / 3) * 86);
                RectTransform tile = Box10(shapeHolder, "Shape" + i, x, y, 135, 78, EdRow, EdRowEdge);
                shapeFills[i] = tile.GetComponent<Image>();
                shapeButtons[i] = tile.gameObject.AddComponent<Button>();
                shapeButtons[i].targetGraphic = shapeFills[i];
                shapeButtons[i].transition = Selectable.Transition.None;
                shapeIcons[i] = ShapeIcon(tile, i);
                Txt(tile, "Label", 0, 46, 135, 22, bold, 11, Words, TextAnchor.MiddleCenter, shapes[i]);
            }


            // The sizes: two to a line.
            string[] fields = { "WIDTH", "DEPTH", "CORNERS", "POINTS", "INNER SIZE", "EDGE HEIGHT", "EDGE WIDTH" };
            float[] fieldX = { 17, 236, 17, 17, 236, 17, 236 };
            float[] fieldY = { 222, 222, 292, 292, 292, 362, 362 };
            GameObject[] fieldRows = new GameObject[7];
            Button[] minus = new Button[7];
            Button[] plus = new Button[7];
            InputField[] inputs = new InputField[7];
            for (int i = 0; i < fields.Length; i++)
            {
                RectTransform row = Rect10(panel, fields[i].Replace(" ", string.Empty) + "Row", fieldX[i], fieldY[i], 203, 62);
                Txt(row, "Name", 0, 0, 203, 16, bold, 11, Muted, TextAnchor.MiddleLeft, fields[i]);
                minus[i] = Btn(row, "Minus", 0, 24, 34, 34, "−", 14);
                inputs[i] = Field10(row, "Value", 40, 24, 123, 34, string.Empty, 15, false);
                inputs[i].textComponent.alignment = TextAnchor.MiddleCenter;
                inputs[i].textComponent.font = bold;
                inputs[i].characterLimit = 8;
                plus[i] = Btn(row, "Plus", 169, 24, 34, 34, "+", 14);
                fieldRows[i] = row.gameObject;
            }

            // The edge.
            RectTransform edgeRow = Rect10(panel, "EdgeRow", 17, 432, 422, 34);
            Txt(edgeRow, "Name", 0, 0, 80, 34, bold, 11, Muted, TextAnchor.MiddleLeft, "EDGE");
            Button previousEdge = Btn(edgeRow, "Previous", 90, 0, 34, 34, "‹", 13);
            Text edgeLabel = Txt(edgeRow, "Choice", 128, 0, 160, 34, bold, 13, Words, TextAnchor.MiddleCenter, "Orange + teal");
            edgeLabel.resizeTextForBestFit = true;
            edgeLabel.resizeTextMinSize = 12;
            edgeLabel.resizeTextMaxSize = 20;
            Button nextEdge = Btn(edgeRow, "Next", 292, 0, 34, 34, "›", 13);

            Text status = Txt(panel, "Status", 17, 476, 330, 46, medium, 11, Muted, TextAnchor.MiddleLeft,
                "Changes show on the table at once.");
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            Button reset = Btn(panel, "Reset", 357, 482, 82, 36, "RESET", 12);
            Button close = Btn(panel, "Close", 0, 0, 10, 10, "×", 10);
            close.gameObject.SetActive(false);

            // BACK | note | APPLY
            EditorFooter(root, out Button cancel, out Button done, out Text note,
                "Seats go by the shape (a hexagon has six). A game can name a table; the room can swap it before START.");
            note.raycastTarget = false;
            Text doneLabel = done.transform.Find("Label").GetComponent<Text>();
            doneLabel.text = "APPLY";
            RectTransform doneRect = (RectTransform)done.transform;
            doneRect.sizeDelta = new Vector2(101f * P, doneRect.sizeDelta.y);
            doneRect.anchoredPosition = new Vector2(1155f * P, doneRect.anchoredPosition.y);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("closeButton").objectReferenceValue = close;
            AssignArray(so.FindProperty("shapeButtons"), shapeButtons);
            so.FindProperty("modelRow").objectReferenceValue = models.gameObject;
            so.FindProperty("previousModelButton").objectReferenceValue = previousModel;
            so.FindProperty("modelLabel").objectReferenceValue = modelLabel;
            so.FindProperty("nextModelButton").objectReferenceValue = nextModel;
            AssignArray(so.FindProperty("fieldRows"), fieldRows);
            AssignArray(so.FindProperty("fieldMinusButtons"), minus);
            AssignArray(so.FindProperty("fieldPlusButtons"), plus);
            AssignArray(so.FindProperty("fieldInputs"), inputs);
            so.FindProperty("edgeRow").objectReferenceValue = edgeRow.gameObject;
            so.FindProperty("previousEdgeButton").objectReferenceValue = previousEdge;
            so.FindProperty("edgeLabel").objectReferenceValue = edgeLabel;
            so.FindProperty("nextEdgeButton").objectReferenceValue = nextEdge;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("undoButton").objectReferenceValue = undo;
            so.FindProperty("redoButton").objectReferenceValue = redo;
            so.FindProperty("resetButton").objectReferenceValue = reset;
            so.FindProperty("cancelButton").objectReferenceValue = cancel;
            so.FindProperty("doneButton").objectReferenceValue = done;
            so.FindProperty("handlesRoot").objectReferenceValue = layer;
            AssignArray(so.FindProperty("handles"), handles);
            AssignArray(so.FindProperty("shapeFills"), shapeFills);
            AssignArray(so.FindProperty("shapeIcons"), shapeIcons);
            so.FindProperty("previewViewport").rectValue = new Rect(24f / 1280f, 92f / 720f, 760f / 1280f, 536f / 720f);
            so.FindProperty("shapeOn").colorValue = EdTeal;
            so.FindProperty("shapeOff").colorValue = EdRow;
            so.FindProperty("iconOn").colorValue = Cyan;
            so.FindProperty("iconOff").colorValue = EdFaint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Mask12(RectTransform root, string name, float x, float y, float width, float height)
        {
            Image page = Rect10(root, name, x, y, width, height).gameObject.AddComponent<Image>();
            page.color = EdPage;
            page.raycastTarget = false;
        }

        // The shape's outline on its tile: a pill, a long and a square box, a disc, an oval, a hexagon.
        private static Image ShapeIcon(RectTransform tile, int shape)
        {
            float[] widths = { 40, 46, 30, 32, 42, 36 };
            float[] heights = { 26, 26, 30, 32, 26, 32 };
            float width = widths[shape];
            float height = heights[shape];
            RectTransform icon = Rect10(tile, "Icon", (135 - width) * 0.5f, 10 + ((32 - height) * 0.5f), width, height);
            Image image = icon.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = EdFaint;
            if (shape == 3 || shape == 4)
            {
                image.sprite = circle;
            }
            else if (shape == 5 && hex != null)
            {
                image.sprite = hex;
                image.preserveAspect = true;
            }
            else
            {
                image.sprite = rounded;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = shape == 0 ? 0.25f : 1f;
            }

            return image;
        }
    }
}
