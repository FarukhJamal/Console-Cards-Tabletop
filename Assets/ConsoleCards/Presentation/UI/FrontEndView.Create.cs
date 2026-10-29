using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// CREATE (design board 4, owner 2026-10-30): MAKE A GAME, the three editors (CARD STUDIO, DECKS, TABLE) and
    /// YOUR GAMES with PLAY, EDIT and ⋯ (Copy, Rename, Delete). The parts are found by name in FE_Create
    /// (Console Cards > UI > Build Board Screens makes it); an older FE_Create without them keeps its Menu rows.
    /// </summary>
    public sealed partial class FrontEndView
    {
        public const int MyGameRowCount = 6;
        private const string CreateGames = "Pages/Create/Games";
        private int openMenuRow = -1;
        private int renamingRow = -1;

        private void BindCreateButtons()
        {
            On("Pages/Create/MakeGame", () => actions?.MakeGame?.Invoke());
            On("Pages/Create/Tiles/CardStudio", () => actions?.OpenCardEditor?.Invoke());
            On("Pages/Create/Tiles/Decks", () => actions?.OpenDeckEditor?.Invoke());
            On("Pages/Create/Tiles/Table", () => actions?.OpenTableTools?.Invoke());
            On("Pages/Create/Footer/Back", () => Go(FrontEndPage.Home));
            for (int i = 0; i < MyGameRowCount; i++)
            {
                int row = i;
                string at = CreateGames + "/Rows/Row" + i;
                On(at + "/Play", () => WithGame(row, id => actions?.PlayMyGame?.Invoke(id)));
                On(at + "/Edit", () => WithGame(row, id => actions?.EditMyGame?.Invoke(id)));
                On(at + "/More", () => ToggleRowMenu(row));
                On(at + "/Menu/Copy", () => WithGame(row, id => actions?.CopyMyGame?.Invoke(id)));
                On(at + "/Menu/Rename", () => StartRename(row));
                On(at + "/Menu/Delete", () => WithGame(row, id => actions?.DeleteMyGame?.Invoke(id)));
                InputField field = RenameField(row);
                if (field != null)
                {
                    field.onEndEdit.RemoveAllListeners();
                    field.onEndEdit.AddListener(text => FinishRename(row, text));
                }
            }
        }

        private void RenderCreate()
        {
            if (transform.Find(CreateGames) == null) return; // an FE_Create made before board 4
            int count = model.MyGames.Count;
            for (int i = 0; i < MyGameRowCount; i++)
            {
                string at = CreateGames + "/Rows/Row" + i;
                bool shown = i < count;
                SetActive(at, shown);
                if (!shown) continue;
                FrontEndMyGame game = model.MyGames[i];
                SetText(at + "/Name", game.Name);
                SetText(at + "/Detail", game.Detail);
                bool menu = openMenuRow == i || game.DeleteArmed;
                SetActive(at + "/Menu", menu);
                SetText(at + "/Menu/Delete/Label", game.DeleteArmed ? "DELETE?" : "DELETE");
                bool renaming = renamingRow == i;
                SetActive(at + "/RenameField", renaming);
                SetActive(at + "/Name", !renaming);
            }

            SetActive(CreateGames + "/Empty", count == 0);
            SetActive(CreateGames + "/More", count > MyGameRowCount);
            SetText(CreateGames + "/More", count > MyGameRowCount
                ? $"+ {count - MyGameRowCount} more in PLAY › Game library"
                : string.Empty);
        }

        private void WithGame(int row, System.Action<string> act)
        {
            if (model == null || row < 0 || row >= model.MyGames.Count) return;
            openMenuRow = -1;
            renamingRow = -1;
            act(model.MyGames[row].Id);
        }

        private void ToggleRowMenu(int row)
        {
            openMenuRow = openMenuRow == row ? -1 : row;
            renamingRow = -1;
            RenderCreate();
        }

        private void StartRename(int row)
        {
            if (model == null || row >= model.MyGames.Count) return;
            openMenuRow = -1;
            renamingRow = row;
            RenderCreate();
            InputField field = RenameField(row);
            if (field == null) return;
            field.SetTextWithoutNotify(model.MyGames[row].Name);
            field.Select();
            field.ActivateInputField();
        }

        private void FinishRename(int row, string text)
        {
            if (renamingRow != row) return;
            renamingRow = -1;
            if (model == null || row >= model.MyGames.Count) return;
            if (string.IsNullOrWhiteSpace(text) || text.Trim() == model.MyGames[row].Name)
            {
                RenderCreate();
                return;
            }

            actions?.RenameMyGame?.Invoke(model.MyGames[row].Id, text);
        }

        private InputField RenameField(int row)
        {
            Transform field = transform.Find(CreateGames + "/Rows/Row" + row + "/RenameField");
            return field != null ? field.GetComponent<InputField>() : null;
        }
    }
}
