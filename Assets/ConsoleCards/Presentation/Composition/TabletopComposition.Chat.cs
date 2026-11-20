using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Core.Identifiers;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// CHAT-1, designer note 03 (2026-11-17: "add a text chat box for players; Chat beside Log in the Activity / Chat
    /// panel"): the players' messages at this table, oldest first, each with the sender's name in their colour. A
    /// message written here shows at once and, at an online table, goes to the others as a live message (it is never
    /// part of the table's state, saves or Undo). A message from another computer also shows as table news. The words
    /// are never logged. Built only when a message arrives.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        internal const int ChatLetters = 200;
        private const int ChatLinesKept = 60;

        private readonly List<string> chatLines = new List<string>(ChatLinesKept);
        private readonly StringBuilder chatText = new StringBuilder(4096);
        private Action<string> chatSendAction;

        // The Log view asks for this when it is made; the chat survives a new table at the same session.
        private void EnsureChatBound()
        {
            if (runtimeUi == null) return;
            if (chatSendAction == null) chatSendAction = SendChat;
            runtimeUi.BindChat(chatSendAction);
        }

        /// <summary>A message written on this computer: shown here and sent to the others at an online table.</summary>
        internal void SendChat(string text)
        {
            string clean = CleanChat(text);
            if (clean.Length == 0 || !IsInitialized) return;
            AddChat(localPlayerId, clean, false);
            if (IsOnlineTable) online.SendChat(localPlayerId, clean);
        }

        /// <summary>A message from another computer (the host has checked who sent it).</summary>
        internal void ShowRemoteChat(PlayerId player, string text)
        {
            string clean = CleanChat(text);
            if (clean.Length == 0 || !IsInitialized) return;
            AddChat(player, clean, true);
            runtimeUi?.ShowTableNews($"{FormatPlayerName(player)}: {clean}", FeedbackColour(player));
        }

        private void AddChat(PlayerId player, string text, bool fromOthers)
        {
            if (chatLines.Count >= ChatLinesKept) chatLines.RemoveAt(0);
            string colour = ColorUtility.ToHtmlStringRGB(FeedbackColour(player));
            chatLines.Add($"<color=#{colour}><b>{CleanChat(FormatPlayerName(player))}</b></color>  {text}");
            chatText.Clear();
            for (int i = 0; i < chatLines.Count; i++)
            {
                if (i > 0) chatText.Append('\n');
                chatText.Append(chatLines[i]);
            }

            runtimeUi?.ShowChat(chatText.ToString(), fromOthers);
        }

        /// <summary>
        /// One line of at most <see cref="ChatLetters"/> letters, without the marks rich text reads (so a message cannot
        /// restyle the panel) or control characters.
        /// </summary>
        internal static string CleanChat(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            StringBuilder clean = new StringBuilder(Math.Min(text.Length, ChatLetters));
            for (int i = 0; i < text.Length && clean.Length < ChatLetters; i++)
            {
                char c = text[i];
                if (c == '<' || c == '>' || char.IsControl(c)) continue;
                clean.Append(c);
            }

            return clean.ToString().Trim();
        }
    }
}
