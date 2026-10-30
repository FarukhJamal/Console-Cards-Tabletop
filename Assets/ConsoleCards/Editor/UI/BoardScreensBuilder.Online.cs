using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// Board 2b "Online room" (owner, 2026-10-30: "when you switch to online, it gives you a new online lobby flow"): one
    /// screen for playing online, measured from the board (1280 x 720, x1.5). On the left the lobby (MAKE A ROOM, a
    /// code and JOIN, OPEN ROOMS); on the right your room (its name, PRIVATE, the code and COPY, the seats with HOST and
    /// READY, your colour and seat, THE ROOM PLAYS and CHANGE GAME); LEAVE bottom left and START bottom right. FE_Lobby
    /// (before you are in a room) and FE_Room (in one) are both built this way; FrontEndView.OnlineBoard.cs fills them.
    /// </summary>
    public static partial class BoardScreensBuilder
    {
        internal const int OnlineRoomRows = 4;
        internal const int OnlineSeatRows = 8;
        private static readonly Color Live = Hex("2fd46b");
        private static readonly Color OrangeDeep = Hex("3a2516");
        private static readonly Color TealWords = Hex("f4fefe");
        private static readonly Color TagWell = Hex("2b3033");
        private static readonly Color ReadyInk = Hex("102021");

        private static void BuildLobbyBoard(RectTransform page) => BuildOnline(page, false);

        private static void BuildRoomBoard(RectTransform page) => BuildOnline(page, true);

        private static void BuildOnline(RectTransform page, bool inRoom)
        {
            RectTransform board = Rect(page, "Board", 0, 0, 1280, 720);
            Text kicker = TextAt(board, "Kicker", 48, 100, 400, 20, bold, 13, Muted, TextAnchor.MiddleLeft, "PLAY ONLINE");
            kicker.fontStyle = FontStyle.Bold;
            TextAt(board, "Title", 48, 116, 700, 40, bold, 30, Words, TextAnchor.MiddleLeft, "Play with friends");

            // ON THIS COMPUTER | ONLINE (ONLINE chosen, orange).
            RectTransform where = Box(board, "Where", 945, 100, 287, 44, Well, Edge, 1f);
            RectTransform local = Rect(where, "Local", 4, 4, 182, 36);
            Image localCatcher = local.gameObject.AddComponent<Image>();
            localCatcher.color = new Color(0f, 0f, 0f, 0f);
            local.gameObject.AddComponent<Button>().targetGraphic = localCatcher;
            TextAt(local, "Label", 0, 0, 182, 36, bold, 13, Muted, TextAnchor.MiddleCenter, "ON THIS COMPUTER");
            RectTransform online = Box(where, "Online", 189, 4, 94, 36, OrangeDeep, Orange, 1f);
            TextAt(online, "Label", 0, 0, 94, 36, bold, 13, Words, TextAnchor.MiddleCenter, "ONLINE");

            BuildLobbyPanel(board);
            BuildRoomPanel(board, inRoom);

            // LEAVE (BACK before a room) | status · waiting | START
            RectTransform leave = Box(board, "Leave", 48, 644, 100, 44, Well, Edge, 1f);
            leave.gameObject.AddComponent<Button>().targetGraphic = leave.GetComponent<Image>();
            Image dot = Rect(leave, "Dot", 19, 18, 8, 8).gameObject.AddComponent<Image>();
            dot.sprite = circle;
            dot.color = Faint;
            dot.raycastTarget = false;
            TextAt(leave, "Label", 34, 0, 64, 44, bold, 13, Words, TextAnchor.MiddleLeft, inRoom ? "LEAVE" : "BACK");
            Text status = TextAt(board, "Status", 170, 644, 560, 44, medium, 13, Muted, TextAnchor.MiddleLeft, "Connecting…");
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            TextAt(board, "Waiting", 740, 632, 320, 56, medium, 13, Orange, TextAnchor.MiddleRight,
                "Waiting for 1 player to be ready");
            RectTransform start = Box(board, "Start", 1072, 632, 160, 56, Well, Edge, 1f);
            Button startButton = start.gameObject.AddComponent<Button>();
            startButton.targetGraphic = start.GetComponent<Image>();
            ColorBlock colours = startButton.colors;
            colours.disabledColor = new Color(1f, 1f, 1f, 0.6f);
            startButton.colors = colours;
            Image startDot = Rect(start, "Dot", 42, 23.5f, 9, 9).gameObject.AddComponent<Image>();
            startDot.sprite = circle;
            startDot.color = Hex("4b5357");
            startDot.raycastTarget = false;
            TextAt(start, "Label", 58, 0, 100, 56, bold, 17, Faint, TextAnchor.MiddleLeft, "START");
            start.gameObject.SetActive(inRoom);
            board.Find("Waiting").gameObject.SetActive(inRoom);
        }

        // The lobby: MAKE A ROOM, a code and JOIN, the open rooms.
        private static void BuildLobbyPanel(RectTransform board)
        {
            RectTransform panel = Box(board, "Rooms", 48, 172, 400, 436, Panel, Edge, 1f);
            RectTransform make = Box(panel, "Make", 20, 20, 360, 52, TealButton, Cyan, 1f);
            make.gameObject.AddComponent<Button>().targetGraphic = make.GetComponent<Image>();
            TextAt(make, "Label", 0, 0, 360, 52, bold, 15, TealWords, TextAnchor.MiddleCenter, "MAKE A ROOM");
            TextAt(panel, "Privacy", 20, 74, 360, 18, medium, 11, Muted, TextAnchor.MiddleCenter, string.Empty);
            Button privacy = Rect(panel, "PrivacyToggle", 20, 74, 360, 18).gameObject.AddComponent<Button>();
            Image privacyCatcher = privacy.gameObject.AddComponent<Image>();
            privacyCatcher.color = new Color(0f, 0f, 0f, 0f);
            privacy.targetGraphic = privacyCatcher;

            RectTransform field = Box(panel, "Code", 20, 96, 284, 44, Well, ItemEdge, 1f);
            InputField input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = field.GetComponent<Image>();
            Text typed = TextAt(field, "Text", 12, 0, 260, 44, medium, 15, Words, TextAnchor.MiddleLeft, string.Empty);
            typed.supportRichText = false;
            Text hint = TextAt(field, "Placeholder", 12, 0, 260, 44, medium, 15, Faint, TextAnchor.MiddleLeft, "eu-");
            input.textComponent = typed;
            input.placeholder = hint;
            input.characterLimit = 16;
            input.lineType = InputField.LineType.SingleLine;
            SmallButtonB(panel, "Join", 312, 96, 68, 44, "JOIN", 13);

            Text heading = TextAt(panel, "Heading", 20, 150, 140, 18, bold, 12, Muted, TextAnchor.MiddleLeft, "OPEN ROOMS");
            heading.fontStyle = FontStyle.Bold;
            Line(panel, "Rule", 127, 159, 253, 1, Edge);
            for (int i = 0; i < OnlineRoomRows; i++)
            {
                RectTransform row = Box(panel, "Row" + i, 20, 176 + (i * 64), 360, 52, Item, ItemEdge, 1f);
                row.gameObject.AddComponent<Button>().targetGraphic = row.GetComponent<Image>();
                TextAt(row, "Title", 14, 8, 260, 20, bold, 14, Words, TextAnchor.MiddleLeft, "[ROOM NAME]");
                TextAt(row, "Detail", 14, 28, 260, 16, medium, 12, Muted, TextAnchor.MiddleLeft, "Trap Floor · 3 of 4");
                TextAt(row, "Join", 280, 0, 66, 52, bold, 13, Cyan, TextAnchor.MiddleRight, "JOIN ›");
                row.gameObject.SetActive(false);
            }

            Text empty = TextAt(panel, "Empty", 20, 176, 360, 60, medium, 13, Muted, TextAnchor.UpperLeft, "Connecting…");
            empty.horizontalOverflow = HorizontalWrapMode.Wrap;
            Text note = TextAt(panel, "Note", 20, 396, 360, 20, medium, 12, Faint, TextAnchor.MiddleLeft,
                "Private rooms do not show here; share the code instead.");
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // Your room: name, PRIVATE, the code and COPY; the seats; your colour and seat; the game.
        private static void BuildRoomPanel(RectTransform board, bool inRoom)
        {
            RectTransform panel = Box(board, "Room", 472, 172, 760, 436, Panel, inRoom ? Orange : Edge, inRoom ? 2f : 1f);
            Text placeholder = TextAt(panel, "Placeholder", 26, 20, 700, 80, medium, 15, Muted, TextAnchor.UpperLeft,
                "MAKE A ROOM and share its code, or join a friend's room with theirs.\nThe room's players, seats and game show here.");
            placeholder.horizontalOverflow = HorizontalWrapMode.Wrap;
            placeholder.gameObject.SetActive(!inRoom);
            RectTransform parts = Rect(panel, "Parts", 0, 0, 760, 436);
            parts.gameObject.SetActive(inRoom);

            TextAt(parts, "Name", 26, 20, 420, 30, bold, 20, Words, TextAnchor.MiddleLeft, "ALEX'S TABLE");
            RectTransform tag = Box(parts, "Private", 180, 24, 70, 22, Well, ItemEdge, 1f);
            TextAt(tag, "Label", 0, 0, 70, 22, bold, 11, Soft, TextAnchor.MiddleCenter, "PRIVATE");
            TextAt(parts, "CodeLabel", 440, 20, 66, 30, medium, 13, Muted, TextAnchor.MiddleRight, "Code");
            TextAt(parts, "Code", 514, 20, 150, 30, bold, 18, Cyan, TextAnchor.MiddleLeft, "eu-K7PQ2X");
            SmallButtonB(parts, "Copy", 666, 18, 68, 34, "COPY", 13);

            RectTransform seats = Rect(parts, "Seats", 26, 62, 708, 300);
            for (int i = 0; i < OnlineSeatRows; i++)
            {
                RectTransform row = Box(seats, "Seat" + i, (i % 2) * 358, (i / 2) * 60, 350, 52, Item, ItemEdge, 1f);
                Image seatDot = Rect(row, "Dot", 14, 20, 12, 12).gameObject.AddComponent<Image>();
                seatDot.sprite = circle;
                seatDot.color = Hex("2f7bff");
                seatDot.raycastTarget = false;
                TextAt(row, "Name", 38, 0, 120, 52, bold, 14, Words, TextAnchor.MiddleLeft, "ALEX");
                TextAt(row, "Detail", 120, 0, 110, 52, medium, 12, Muted, TextAnchor.MiddleLeft, "seat 1 · you");
                RectTransform host = Box(row, "Host", 210, 15, 52, 22, TagWell, null, 0f);
                TextAt(host, "Label", 0, 0, 52, 22, bold, 11, Words, TextAnchor.MiddleCenter, "HOST");
                RectTransform state = Box(row, "State", 268, 15, 70, 22, Cyan, null, 0f);
                TextAt(state, "Label", 0, 0, 70, 22, bold, 11, ReadyInk, TextAnchor.MiddleCenter, "READY");
                TextAt(row, "Open", 38, 0, 300, 52, medium, 14, Muted, TextAnchor.MiddleLeft, "Open seat · waiting for a player");
                row.gameObject.SetActive(false);
            }

            RectTransform you = Rect(parts, "You", 26, 182, 708, 32);
            TextAt(you, "ColourLabel", 0, 0, 80, 32, medium, 13, Muted, TextAnchor.MiddleLeft, "Your colour");
            for (int i = 0; i < 3; i++)
            {
                RectTransform swatch = Rect(you, "Swatch" + i, 80 + (i * 34), 3, 26, 26);
                Image fill = swatch.gameObject.AddComponent<Image>();
                fill.sprite = circle;
                fill.color = Hex(i == 0 ? "2f7bff" : i == 1 ? "ff5fa2" : "7a4bff");
                swatch.gameObject.AddComponent<Button>().targetGraphic = fill;
                if (i == 1)
                {
                    Outline ring = swatch.gameObject.AddComponent<Outline>();
                    ring.effectColor = Words;
                    ring.effectDistance = new Vector2(2f * S, -2f * S);
                    ring.useGraphicAlpha = false;
                }
            }

            TextAt(you, "SeatLabel", 560, 0, 40, 32, medium, 13, Muted, TextAnchor.MiddleRight, "Seat");
            SmallButtonB(you, "SeatPrevious", 606, 0, 32, 32, "‹", 15);
            TextAt(you, "Seat", 638, 0, 32, 32, bold, 14, Words, TextAnchor.MiddleCenter, "1");
            SmallButtonB(you, "SeatNext", 672, 0, 32, 32, "›", 15);

            RectTransform game = Box(parts, "Game", 26, 230, 708, 94, Well, Edge, 1f);
            Box(game, "Picture", 14, 15, 64, 64, Cyan, null, 0f).GetComponent<Image>().raycastTarget = false;
            RawImage picture = Rect(game, "Art", 14, 15, 64, 64).gameObject.AddComponent<RawImage>();
            picture.color = new Color(1f, 1f, 1f, 0f);
            picture.raycastTarget = false;
            Text plays = TextAt(game, "Kicker", 94, 24, 400, 18, medium, 12, Muted, TextAnchor.MiddleLeft, "THE ROOM PLAYS");
            plays.fontStyle = FontStyle.Bold;
            TextAt(game, "Name", 94, 44, 470, 26, bold, 18, Words, TextAnchor.MiddleLeft,
                "TRAP FLOOR <size=19><color=#9BA4A7>· 4 players · Hard · Regular</color></size>");
            SmallButtonB(game, "Change", 556, 24, 138, 46, "CHANGE GAME", 13);
        }

        // A dark board button (#1C1F1F, edge #3C4143) with its words in the middle.
        private static Button SmallButtonB(Transform parent, string name, float x, float y, float width, float height,
            string words, int size)
        {
            RectTransform rect = Box(parent, name, x, y, width, height, Well, Edge, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            TextAt(rect, "Label", 0, 0, width, height, bold, size, Words, TextAnchor.MiddleCenter, words);
            return button;
        }
    }
}
