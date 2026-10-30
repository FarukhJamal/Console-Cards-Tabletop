using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// PROFILE-2 (owner, 2026-10-31): board 19 "Player profile v2" as FE_Profile, and board 19b's switch-player list
    /// under the top bar's chip (FE_TopBar). FrontEndView.Profile.cs fills and wires them.
    /// </summary>
    public static partial class BoardScreensBuilder
    {
        internal const int ProfileBoardRows = 5;
        internal const int ProfileBoardAvatars = 8;
        internal const int ProfileBoardColours = 4;
        internal const int SwitchRows = 5;
        private static readonly Color Danger = UiKit.Hex("ff7a6b");
        private static readonly Color Saved = UiKit.Hex("2fd46b");

        private static void BuildProfileBoard(RectTransform page)
        {
            Text kicker = TextAt(page, "Kicker", 72, 100, 300, 18, bold, 12, Muted, TextAnchor.MiddleLeft, "PROFILE");
            kicker.fontStyle = FontStyle.Bold;
            TextAt(page, "Title", 72, 118, 700, 42, bold, 34, Words, TextAnchor.MiddleLeft, "Who is playing?");

            // Left: the players on this computer.
            RectTransform players = Rect(page, "Players", 72, 176, 360, 440);
            TextAt(players, "Label", 0, 0, 360, 16, bold, 12, Muted, TextAnchor.MiddleLeft, "PLAYERS ON THIS COMPUTER");
            for (int i = 0; i < ProfileBoardRows; i++)
            {
                RectTransform row = Box(players, "Row" + i, 0, 26 + (i * 82), 360, 72, Panel, Edge, 1f);
                Button button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = row.GetComponent<Image>();
                FramedPicture(row, "Avatar", 12, 12, 48, 2f);
                TextAt(row, "Name", 74, 14, 200, 22, bold, 16, Words, TextAnchor.MiddleLeft, "PLAYER " + (i + 1));
                TextAt(row, "Line", 74, 38, 210, 18, medium, 13, Muted, TextAnchor.MiddleLeft, "Not played yet");
                RectTransform tag = Box(row, "Tag", 268, 25, 78, 22, Cyan, null, 0f);
                tag.GetComponent<Image>().raycastTarget = false;
                TextAt(tag, "Label", 0, 0, 78, 22, bold, 11, Ink, TextAnchor.MiddleCenter, "PLAYING");
                TextAt(row, "Chevron", 324, 22, 24, 28, bold, 20, Muted, TextAnchor.MiddleCenter, "›");
            }

            RectTransform add = Box(players, "New", 0, 26 + (ProfileBoardRows * 82), 360, 72, new Color(0f, 0f, 0f, 0f), UiKit.Hex("5a6266"), 1f);
            Button addButton = add.gameObject.AddComponent<Button>();
            addButton.targetGraphic = add.GetComponent<Image>();
            RectTransform plus = Box(add, "Plus", 12, 12, 48, 48, Well, UiKit.Hex("5a6266"), 1f);
            plus.GetComponent<Image>().raycastTarget = false;
            TextAt(plus, "Label", 0, 0, 48, 48, bold, 24, Cyan, TextAnchor.MiddleCenter, "+");
            TextAt(add, "Label", 74, 0, 200, 72, bold, 14, Cyan, TextAnchor.MiddleLeft, "NEW PLAYER");

            // Right: the player playing now.
            RectTransform card = Box(page, "Card", 464, 176, 744, 420, Panel, Edge, 1f);
            card.GetComponent<Image>().raycastTarget = false;
            FramedPicture(card, "Big", 26, 24, 168, 4f);
            TextAt(card, "NameLabel", 220, 24, 300, 16, bold, 12, Muted, TextAnchor.MiddleLeft, "NAME");
            NameField(card, "Name", 220, 44, 498, 44);
            TextAt(card, "ColourLabel", 220, 104, 498, 16, bold, 12, Muted, TextAnchor.MiddleLeft,
                "COLOUR  <color=#6B7477>your seat, your pieces, your drawings</color>");
            for (int i = 0; i < ProfileBoardColours; i++)
            {
                RectTransform ring = Box(card, "ColourRing" + i, 218 + (i * 46), 124, 42, 42, Orange, null, 0f);
                ring.GetComponent<Image>().raycastTarget = false;
                RectTransform swatch = Box(card, "Colour" + i, 222 + (i * 46), 128, 34, 34, Words, null, 0f);
                Button button = swatch.gameObject.AddComponent<Button>();
                button.targetGraphic = swatch.GetComponent<Image>();
            }

            TextAt(card, "AvatarLabel", 26, 214, 300, 16, bold, 12, Muted, TextAnchor.MiddleLeft, "AVATAR");
            for (int i = 0; i < ProfileBoardAvatars; i++)
            {
                RectTransform ring = Box(card, "AvatarRing" + i, 24 + (i * 74), 232, 68, 68, Cyan, null, 0f);
                ring.GetComponent<Image>().raycastTarget = false;
                RectTransform slot = Box(card, "Avatar" + i, 26 + (i * 74), 234, 64, 64, UiKit.Hex("2b3033"), Edge, 1f);
                Button button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slot.GetComponent<Image>();
                RawImage picture = Rect(slot, "Picture", 2, 2, 60, 60).gameObject.AddComponent<RawImage>();
                picture.raycastTarget = false;
            }

            string[] stats = { "GAMES", "WINS", "PLAYED LAST", "YOUR GAMES" };
            for (int i = 0; i < stats.Length; i++)
            {
                RectTransform stat = Box(card, "Stat" + i, 26 + (i * 176), 322, 166, 72, Well, Edge, 1f);
                stat.GetComponent<Image>().raycastTarget = false;
                TextAt(stat, "Label", 14, 12, 150, 14, bold, 11, Muted, TextAnchor.MiddleLeft, stats[i]);
                TextAt(stat, "Value", 14, 32, 150, 28, bold, i == 2 ? 15 : 22, Words, TextAnchor.MiddleLeft, "0");
            }

            // Bottom: BACK, DELETE PLAYER (asks again), SAVED, and PLAY AS on the right.
            BackB(page, 72, 640, 120, 44);
            RectTransform delete = Rect(page, "Delete", 204, 640, 220, 44);
            Image catcher = delete.gameObject.AddComponent<Image>();
            catcher.sprite = rounded;
            catcher.type = Image.Type.Sliced;
            catcher.color = new Color(0f, 0f, 0f, 0f);
            Button deleteButton = delete.gameObject.AddComponent<Button>();
            deleteButton.targetGraphic = catcher;
            TextAt(delete, "Label", 0, 0, 220, 44, bold, 13, Danger, TextAnchor.MiddleCenter, "DELETE PLAYER");
            RectTransform saved = Rect(page, "Saved", 436, 640, 120, 44);
            if (check != null)
            {
                Image tick = Rect(saved, "Tick", 0, 15, 14, 14).gameObject.AddComponent<Image>();
                tick.sprite = check;
                tick.color = Saved;
                tick.raycastTarget = false;
            }

            TextAt(saved, "Label", 20, 0, 100, 44, bold, 12, Saved, TextAnchor.MiddleLeft, "SAVED");
            RectTransform play = Box(page, "PlayAs", 908, 636, 300, 48, TealButton, Cyan, 1f);
            Button playButton = play.gameObject.AddComponent<Button>();
            playButton.targetGraphic = play.GetComponent<Image>();
            Image dot = Rect(play, "Dot", 18, 20, 8, 8).gameObject.AddComponent<Image>();
            dot.sprite = circle;
            dot.color = Words;
            dot.raycastTarget = false;
            TextAt(play, "Label", 34, 0, 260, 48, bold, 14, UiKit.Hex("f4fefe"), TextAnchor.MiddleLeft, "PLAY AS PLAYER 1");
        }

        // A picture in a coloured frame: <name> (the frame, coloured by the view) with <name>/Picture inside.
        private static void FramedPicture(RectTransform parent, string name, float x, float y, float size, float edge)
        {
            RectTransform frame = Box(parent, name, x, y, size, size, Cyan, null, 0f);
            frame.GetComponent<Image>().raycastTarget = false;
            RawImage picture = Rect(frame, "Picture", edge, edge, size - (2f * edge), size - (2f * edge)).gameObject.AddComponent<RawImage>();
            picture.color = new Color(1f, 1f, 1f, 0.06f);
            picture.raycastTarget = false;
        }

        private static void NameField(RectTransform parent, string name, float x, float y, float width, float height)
        {
            RectTransform rect = Box(parent, name, x, y, width, height, Well, UiKit.Hex("424547"), 1f);
            InputField input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = rect.GetComponent<Image>();
            Text typed = TextAt(rect, "Text", 14, 0, width - 28, height, bold, 18, Words, TextAnchor.MiddleLeft, string.Empty);
            typed.supportRichText = false;
            Text placeholder = TextAt(rect, "Placeholder", 14, 0, width - 28, height, medium, 16, Faint, TextAnchor.MiddleLeft, "Your name");
            input.textComponent = typed;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 32;
        }

        // Board 19b: the switch-player list under the chip. It draws over the screens (its own sorting) and a click
        // outside it closes it.
        private static void SwitchList(RectTransform top)
        {
            RectTransform holder = Rect(top, "Switch", 0, 0, 1280, 720);
            UiKit.Stretch(holder); // over the whole screen, whatever its shape
            Canvas canvas = holder.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30;
            holder.gameObject.AddComponent<GraphicRaycaster>();
            RectTransform outside = Rect(holder, "Outside", 0, 0, 1280, 720);
            UiKit.Stretch(outside);
            Image catcher = outside.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            Button close = outside.gameObject.AddComponent<Button>();
            close.targetGraphic = catcher;
            close.transition = Selectable.Transition.None;

            RectTransform list = Box(holder, "List", 836, 84, 300, 64 + (SwitchRows * 58) + 96, Well, Edge, 1f);
            TextAt(list, "Label", 18, 10, 260, 20, bold, 11, Muted, TextAnchor.MiddleLeft, "SWITCH PLAYER");
            for (int i = 0; i < SwitchRows; i++)
            {
                RectTransform row = Rect(list, "Row" + i, 8, 36 + (i * 58), 284, 56);
                Image fill = row.gameObject.AddComponent<Image>();
                fill.sprite = rounded;
                fill.type = Image.Type.Sliced;
                fill.color = new Color(0f, 0f, 0f, 0f);
                Button button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;
                ColorBlock colours = button.colors;
                colours.normalColor = new Color(1f, 1f, 1f, 0f);
                colours.highlightedColor = TealFill;
                colours.pressedColor = TealFill;
                colours.selectedColor = new Color(1f, 1f, 1f, 0f);
                button.colors = colours;
                FramedPicture(row, "Avatar", 8, 8, 40, 2f);
                TextAt(row, "Name", 60, 8, 180, 20, bold, 14, Words, TextAnchor.MiddleLeft, "PLAYER");
                TextAt(row, "Line", 60, 28, 200, 18, medium, 12, Muted, TextAnchor.MiddleLeft, string.Empty);
                if (check != null)
                {
                    Image tick = Rect(row, "Tick", 256, 20, 16, 16).gameObject.AddComponent<Image>();
                    tick.sprite = check;
                    tick.color = Cyan;
                    tick.raycastTarget = false;
                }
            }

            float y = 36 + (SwitchRows * 58);
            Line(list, "Rule", 12, y + 4, 276, 1, Edge);
            SwitchAction(list, "New", y + 12, "+  NEW PLAYER", Cyan);
            SwitchAction(list, "Edit", y + 54, "EDIT PROFILE  ›", Words);
            holder.gameObject.SetActive(false);
        }

        private static void SwitchAction(RectTransform list, string name, float y, string words, Color colour)
        {
            RectTransform row = Rect(list, name, 8, y, 284, 40);
            Image fill = row.gameObject.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.color = new Color(0f, 0f, 0f, 0f);
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = TealFill;
            button.colors = colours;
            TextAt(row, "Label", 14, 0, 260, 40, bold, 13, colour, TextAnchor.MiddleLeft, words);
        }
    }
}
