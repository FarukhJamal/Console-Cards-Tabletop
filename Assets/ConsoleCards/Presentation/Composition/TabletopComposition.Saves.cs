using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ConsoleCards.GameTemplates;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Doc 58 step 4 (owner, 2026-11-04): <b>Save progress</b> and <b>Load progress</b> (the table menu: right-click
    /// the table, or Esc › SAVE · LOAD · TABLE), plus an <b>auto-save</b> every five minutes. A save is the whole table
    /// at that moment (the match, the game's own state, the drawings and toys, and the game it is: a built-in game or
    /// one you made), written to your profile's "saves" folder. Loading deals that game again and puts the saved moment
    /// on it; your table before loading is kept as "Auto-save - before loading". Nothing here runs per frame except a
    /// time check.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const float AutoSaveSeconds = 300f;
        private const string SaveExtension = ".ccsave";
        private const int LoadMenuSize = 10;

        private string SavesFolder =>
            profiles != null && profiles.HasActive
                ? Path.Combine(profiles.ActiveFolder, "saves")
                : Path.Combine(UnityEngine.Application.persistentDataPath, "saves");

        private string CurrentGameName()
        {
            if (PlayerGamesPanel.activePlayerGame != null && !string.IsNullOrWhiteSpace(PlayerGamesPanel.activePlayerGame.name))
            {
                return PlayerGamesPanel.activePlayerGame.name.Trim();
            }

            if (activeSession == null || activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom) return "Empty Table";
            return SessionService.ReplacementDisplayName(activeSession.Selection);
        }

        private void SaveProgress()
        {
            ContextMenus.CloseContextMenu();
            if (WriteSave(null, out string result))
            {
                runtimeUi?.ShowTableNews($"Saved: {result}", FeedbackColour(localPlayerId));
                RecordHandoverLine($"Saved the table as \"{result}\".");
            }
            else
            {
                ShowMessage($"The table could not be saved: {result}");
            }
        }

        // name null: a new save named after the game and the time; otherwise that file (an auto-save) is replaced.
        private bool WriteSave(string fixedName, out string result)
        {
            if (activeSession == null || matchState == null)
            {
                result = "there is no table yet";
                return false;
            }

            byte[] data = Online.PackTableForSave();
            if (data == null)
            {
                result = "the table could not be packed (see the log)";
                return false;
            }

            string game = FileWords(CurrentGameName());
            string name = FileWords(fixedName ?? $"{game} {DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss", CultureInfo.InvariantCulture)}");
            string path = Path.Combine(SavesFolder, name + SaveExtension);
            string temporary = path + ".saving";
            try
            {
                Directory.CreateDirectory(SavesFolder);
                File.WriteAllBytes(temporary, data);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
            }
            catch (Exception exception)
            {
                result = exception.Message;
                return false;
            }

            result = name;
            return true;
        }

        // A name a file can carry as it reads ("Trap Floor · Easy 2026-11-04 02.47.20"): only the characters Windows
        // does not allow are replaced.
        private static string FileWords(string words)
        {
            if (string.IsNullOrWhiteSpace(words)) return "Table";
            char[] letters = words.Trim().ToCharArray();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < letters.Length; i++)
            {
                if (Array.IndexOf(invalid, letters[i]) >= 0) letters[i] = '-';
            }

            string name = new string(letters);
            return name.Length > 80 ? name.Substring(0, 80) : name;
        }

        private void ShowLoadMenu()
        {
            Vector2 anchor = toyMenuScreen;
            ContextMenus.CloseContextMenu();
            List<PopupActionOption> actions = new List<PopupActionOption>(LoadMenuSize);
            FileInfo[] files = Array.Empty<FileInfo>();
            try
            {
                if (Directory.Exists(SavesFolder)) files = new DirectoryInfo(SavesFolder).GetFiles("*" + SaveExtension);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Saves] {SavesFolder} could not be read: {exception.Message}");
            }

            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (int i = 0; i < files.Length && actions.Count < LoadMenuSize; i++)
            {
                string path = files[i].FullName;
                string label = Path.GetFileNameWithoutExtension(path);
                actions.Add(new PopupActionOption(label, !IsOnlineTable, () => LoadProgress(path, label)));
            }

            if (files.Length > 0) actions.Add(new PopupActionOption("Remove a save…", true, () => ShowDeleteSaveMenu(anchor)));

            runtimeUi?.ShowContextMenu(
                anchor,
                "LOAD PROGRESS",
                files.Length == 0
                    ? "No saves yet. Right-click the table › Save progress (the table also saves itself every five minutes)."
                    : IsOnlineTable ? "Leave the online room to load a save." : "Your newest saves. The table you have now is kept as an auto-save first.",
                actions,
                ContextMenus.CloseContextMenu,
                ContextMenus.DismissPopupFromSecondary);
        }

        // The saves again; a click deletes that one (the menu says so).
        private void ShowDeleteSaveMenu(Vector2 anchor)
        {
            ContextMenus.CloseContextMenu();
            List<PopupActionOption> actions = new List<PopupActionOption>(LoadMenuSize);
            FileInfo[] files = Array.Empty<FileInfo>();
            try
            {
                if (Directory.Exists(SavesFolder)) files = new DirectoryInfo(SavesFolder).GetFiles("*" + SaveExtension);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Saves] {SavesFolder} could not be read: {exception.Message}");
            }

            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (int i = 0; i < files.Length && actions.Count < LoadMenuSize * 2; i++)
            {
                string path = files[i].FullName;
                string label = Path.GetFileNameWithoutExtension(path);
                actions.Add(new PopupActionOption($"Delete: {label}", true, () =>
                {
                    try
                    {
                        File.Delete(path);
                        runtimeUi?.ShowTableNews($"Deleted the save {label}", FeedbackColour(localPlayerId));
                    }
                    catch (Exception exception)
                    {
                        ShowMessage($"The save could not be deleted: {exception.Message}");
                    }

                    ShowDeleteSaveMenu(anchor);
                }));
            }

            runtimeUi?.ShowContextMenu(anchor, "DELETE A SAVE", files.Length == 0 ? "No saves left." : "A click deletes that save for good.",
                actions, ContextMenus.CloseContextMenu, ContextMenus.DismissPopupFromSecondary);
        }

        private void LoadProgress(string path, string label)
        {
            ContextMenus.CloseContextMenu();
            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                ShowMessage($"The save could not be read: {exception.Message}");
                return;
            }

            if (activeSession != null && matchState != null) WriteSave("Auto-save - before loading", out _);
            if (!Online.LoadSavedTable(data, out string error))
            {
                ShowMessage($"The save could not be loaded: {error}");
                return;
            }

            runtimeUi?.ShowTableNews($"Loaded: {label}", FeedbackColour(localPlayerId));
            RecordHandoverLine($"Loaded the save \"{label}\".");
            toyCheckPending = true;
        }

        // Every five minutes of play the table is saved over "Auto-save - <game>" (one computer: offline or the host).
        private void TickAutoSave()
        {
            float now = Time.unscaledTime;
            if (nextAutoSaveAt < 0f)
            {
                nextAutoSaveAt = now + AutoSaveSeconds;
                return;
            }

            if (now < nextAutoSaveAt) return;
            nextAutoSaveAt = now + AutoSaveSeconds;
            if (rebuildingFromUndo || activeSession == null || (IsOnlineTable && !Online.IsHosting)) return;
            if (interactionRouter != null && interactionRouter.HasActiveInteraction)
            {
                nextAutoSaveAt = now + 20f; // not mid-move: soon after
                return;
            }

            if (!WriteSave("Auto-save", out string result)) // one auto-save, replaced each time
            {
                Debug.LogWarning($"[Saves] The auto-save failed: {result}");
            }
        }

        // Esc › SAVE · LOAD · TABLE opens the table menu in the middle of the screen.
        private void OpenTableMenuFromPause()
        {
            OpenTableMenu(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }
    }
}
