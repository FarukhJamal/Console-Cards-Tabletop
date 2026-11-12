using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Board 2b "Online room" (owner, 2026-10-30): playing online is one screen. The lobby on the left (MAKE A ROOM, a
    /// code and JOIN, OPEN ROOMS) and your room on the right (seats, HOST, READY, your colour and seat, THE ROOM PLAYS,
    /// CHANGE GAME); LEAVE bottom left, START (the host: pick the game and start it) or READY (a guest) bottom right.
    /// FE_Lobby and FE_Room built by Console Cards > UI > Build Board Screens have a "Board"; older ones work as before.
    /// </summary>
    public sealed partial class FrontEndView
    {
        private const int OnlineBoardRooms = 4;
        private const int OnlineBoardSeats = 8;
        private static readonly Color ReadyFill = new Color(0.024f, 0.937f, 0.941f, 1f);
        private static readonly Color ReadyInk = new Color(0.063f, 0.125f, 0.129f, 1f);
        private static readonly Color WaitFill = new Color(0.949f, 0.463f, 0.180f, 1f);
        private static readonly Color WaitInk = new Color(0.102f, 0.055f, 0.024f, 1f);
        private static readonly Color IdleFill = new Color(0.169f, 0.188f, 0.200f, 1f);
        private static readonly Color IdleInk = new Color(0.718f, 0.753f, 0.765f, 1f);
        private static readonly Color StartOn = new Color(0.075f, 0.541f, 0.549f, 1f);
        private static readonly Color StartOff = new Color(0.110f, 0.122f, 0.122f, 1f);
        private static readonly Color StartOnWords = new Color(0.957f, 0.996f, 0.996f, 1f);
        private static readonly Color StartOffWords = new Color(0.420f, 0.455f, 0.467f, 1f);

        private bool HasBoardOnline => transform.Find("Pages/Lobby/Board") != null;

        private void BindOnlineBoard()
        {
            if (!HasBoardOnline) return;
            BindOnlineBoard("Pages/Lobby/Board/");
            BindOnlineBoard("Pages/Room/Board/");
        }

        private void BindOnlineBoard(string at)
        {
            BuildServerPicker(at); // P8A
            On(at + "Where/Local", LeaveOnlineForLibrary);
            On(at + "Rooms/Make", () => actions?.CreateRoom?.Invoke(DefaultRoomName(), roomPrivate));
            On(at + "Rooms/PrivacyToggle", () =>
            {
                roomPrivate = !roomPrivate;
                Render();
            });
            On(at + "Rooms/Join", () => actions?.JoinRoom?.Invoke(FieldText(at + "Rooms/Code")));
            for (int i = 0; i < OnlineBoardRooms; i++)
            {
                int row = i;
                On(at + "Rooms/Row" + i, () => JoinListed(row));
            }

            On(at + "Room/Parts/Copy", CopyRoomCodeBoard);
            On(at + "Room/Parts/You/Swatch0", () => actions?.CycleOnlineColour?.Invoke(-1));
            On(at + "Room/Parts/You/Swatch2", () => actions?.CycleOnlineColour?.Invoke(1));
            On(at + "Room/Parts/You/SeatPrevious", () => actions?.CycleOnlineSeat?.Invoke(-1));
            On(at + "Room/Parts/You/SeatNext", () => actions?.CycleOnlineSeat?.Invoke(1));
            On(at + "Room/Parts/Game/Change", () => Go(FrontEndPage.Library));
            On(at + "Leave", () =>
            {
                if (InOnlineRoom) actions?.LeaveRoom?.Invoke();
                else Go(FrontEndPage.Library);
            });
            On(at + "Start", PressOnlineStart);
        }

        private string DefaultRoomName() =>
            model?.Online != null && model.Online.DefaultRoomTitle.Length > 0 ? model.Online.DefaultRoomTitle : "My table";

        // ON THIS COMPUTER: back to the Library. In a room (owner, 2026-11-03: "you can't move back to ON THIS
        // COMPUTER"), the first press asks and the second leaves the room and goes there.
        private bool leaveRoomForLibraryArmed;

        private void LeaveOnlineForLibrary()
        {
            if (InOnlineRoom && !leaveRoomForLibraryArmed)
            {
                leaveRoomForLibraryArmed = true;
                SetText(OnlinePath() + "Status", "Press ON THIS COMPUTER again to leave the room and play on this computer.");
                return;
            }

            bool leave = InOnlineRoom;
            leaveRoomForLibraryArmed = false;
            libraryAfterLeavingRoom = leave; // the room's "you left" screen comes back on the Lobby otherwise
            if (leave) actions?.LeaveRoom?.Invoke();
            Go(FrontEndPage.Library);
        }

        // The host: pick the game (its sheet has START). A guest: READY.
        private void PressOnlineStart()
        {
            OnlineScreenModel online = model?.Online;
            if (online == null || !online.InRoom) return;
            if (online.IsHost) Go(FrontEndPage.Library);
            else actions?.ToggleReady?.Invoke();
        }

        private void CopyRoomCodeBoard()
        {
            if (model?.Online == null || string.IsNullOrEmpty(model.Online.RoomCode)) return;
            GUIUtility.systemCopyBuffer = model.Online.RoomCode;
            SetText(OnlinePath() + "Status", $"Copied {model.Online.RoomCode}: friends type it beside JOIN.");
        }

        private string OnlinePath() => page == FrontEndPage.Room ? "Pages/Room/Board/" : "Pages/Lobby/Board/";

        private void RenderOnlineBoard()
        {
            OnlineScreenModel online = model.Online;
            if (online == null) return;
            string at = OnlinePath();
            SetText(at + "Status", online.Status);
            FitLine(at + "Status");
            RenderServerPicker(at, online);

            // The lobby.
            SetInteractable(at + "Rooms/Make", online.Connected);
            SetInteractable(at + "Rooms/Join", online.Connected);
            SetText(at + "Rooms/Privacy", roomPrivate ? "PRIVATE · only with the code (click to change)" : "PUBLIC · in the room list (click to change)");
            SetActive(at + "Rooms/Privacy", !online.InRoom);
            SetActive(at + "Rooms/PrivacyToggle", !online.InRoom);
            IReadOnlyList<OnlineRoomRow> rooms = online.Rooms;
            for (int i = 0; i < OnlineBoardRooms; i++)
            {
                string row = at + "Rooms/Row" + i;
                bool shown = i < rooms.Count;
                SetActive(row, shown);
                if (!shown) continue;
                SetText(row + "/Title", Upper(rooms[i].Title));
                SetText(row + "/Detail", rooms[i].Detail);
                SetInteractable(row, rooms[i].CanJoin && online.Connected && !online.InRoom);
            }

            SetActive(at + "Rooms/Empty", rooms.Count == 0);
            SetText(at + "Rooms/Empty", online.InRoom
                ? "You are in a room. LEAVE it to see the open rooms."
                : online.Connected
                ? "No open rooms yet: make one, or join a friend's with its code."
                : online.Busy ? "Connecting…" : "Not connected.");

            // Your room.
            bool inRoom = online.InRoom;
            SetActive(at + "Room/Placeholder", !inRoom);
            SetActive(at + "Room/Parts", inRoom);
            SetActive(at + "Start", inRoom);
            SetActive(at + "Waiting", inRoom);
            SetText(at + "Leave/Label", inRoom ? "LEAVE" : "BACK");
            if (!inRoom) return;

            string parts = at + "Room/Parts/";
            SetText(parts + "Name", Upper(online.RoomTitle.Length > 0 ? online.RoomTitle : "ROOM"));
            PlaceAfterName(parts);
            SetText(parts + "Private/Label", online.RoomPrivate ? "PRIVATE" : "PUBLIC");
            SetText(parts + "Code", online.RoomCode);
            IReadOnlyList<OnlineMemberRow> members = online.Members;
            // The seats taken, and one open seat to show friends can still join (board 2b).
            int open = online.Started ? 0 : 1;
            int slots = Mathf.Clamp(Mathf.Max(online.Seats, members.Count + open), 0, OnlineBoardSeats);
            for (int i = 0; i < OnlineBoardSeats; i++)
            {
                string row = parts + "Seats/Seat" + i;
                bool shown = i < slots;
                SetActive(row, shown);
                if (!shown) continue;
                bool taken = i < members.Count;
                SetActive(row + "/Name", taken);
                SetActive(row + "/Detail", taken);
                SetActive(row + "/Open", !taken);
                SetActive(row + "/Host", taken && members[i].IsHost);
                SetActive(row + "/State", taken);
                if (!taken)
                {
                    TintImage(row + "/Dot", IdleInk);
                    continue;
                }

                OnlineMemberRow member = members[i];
                TintImage(row + "/Dot", member.Colour);
                SetText(row + "/Name", Upper(member.Name));
                SetText(row + "/Detail", member.SeatLabel);
                PlaceDetailAfterName(row); // QA Q3: a long name pushed "seat 2 · you" over itself
                SetText(row + "/State/Label", member.State);
                bool ready = member.State == "READY";
                bool waiting = member.State == "RECONNECTING" || member.State == "AWAY";
                TintImage(row + "/State", ready ? ReadyFill : waiting ? WaitFill : IdleFill);
                Tint(row + "/State/Label", ready ? ReadyInk : waiting ? WaitInk : IdleInk);
            }

            // Your colour, your seat and the game sit under the seats.
            int seatRows = (slots + 1) / 2;
            Move(parts + "You", 26f, 62f + (seatRows * 60f) + 2f);
            Move(parts + "Game", 26f, 62f + (seatRows * 60f) + 50f);
            TintImage(parts + "You/Swatch0", online.ColourBefore);
            TintImage(parts + "You/Swatch1", online.Colour);
            TintImage(parts + "You/Swatch2", online.ColourAfter);
            SetText(parts + "You/Seat", online.SeatNumber);
            SetInteractable(parts + "You/SeatPrevious", online.CanChangeSeat);
            SetInteractable(parts + "You/SeatNext", online.CanChangeSeat);
            SetText(parts + "Game/Name",
                $"{online.GameName} <size=19><color=#9BA4A7>· {online.GameDetail}</color></size>");
            SetActive(parts + "Game/Change", online.IsHost);

            // START (the host) or READY (a guest).
            bool canStart = online.IsHost && online.AllReady;
            bool lit = online.IsHost ? canStart : !online.Ready && online.CanReady;
            SetText(at + "Start/Label", online.IsHost ? (online.Started ? "NEW GAME" : "START") : online.Ready ? "NOT READY" : "READY");
            SetInteractable(at + "Start", online.IsHost ? canStart : online.CanReady);
            TintImage(at + "Start", lit ? StartOn : StartOff);
            Tint(at + "Start/Label", lit ? StartOnWords : StartOffWords);
            SetText(at + "Waiting", !online.IsHost && !string.IsNullOrEmpty(online.Waiting) ? online.Waiting
                : online.IsHost
                ? online.Started && online.NotReady > 0 ? "A game is on: NEW GAME once everyone is READY again"
                : online.NotReady > 0 ? $"Waiting for {online.NotReady} player{(online.NotReady == 1 ? string.Empty : "s")} to be ready" : string.Empty
                : online.Ready ? "Waiting for the host to start" : "Press READY when you are");
        }

        // ---------- P8A: the server region (owner, 2026-11-10: "if your server is not available, you can join Asia's one") ----------
        // A SERVER button beside ON THIS COMPUTER | ONLINE opens a list of regions. Made here from the board's own look
        // (the board itself is built by the Editor's board builder). In a room it only shows the room's region.

        private sealed class ServerPicker
        {
            public Button Button;
            public Text Label;
            public GameObject List;
        }

        private readonly Dictionary<string, ServerPicker> serverPickers = new Dictionary<string, ServerPicker>();

        private void BuildServerPicker(string at)
        {
            if (serverPickers.ContainsKey(at)) return;
            RectTransform board = transform.Find(at.TrimEnd('/')) as RectTransform;
            RectTransform where = board != null ? board.Find("Where") as RectTransform : null;
            Text sample = where != null ? where.Find("Local/Label")?.GetComponent<Text>() : null;
            Image look = where != null ? where.GetComponent<Image>() : null;
            if (where == null || sample == null || look == null) return;

            float scale = where.sizeDelta.x / 287f; // the board's units (the builder's 1280 x 720 board, scaled)
            Vector2 size = new Vector2(200f * scale, where.sizeDelta.y);
            ServerPicker picker = new ServerPicker();
            RectTransform button = NewRect("Server", board, where);
            button.sizeDelta = size;
            button.anchoredPosition = where.anchoredPosition + new Vector2(-(200f + 10f) * scale, 0f);
            Image face = button.gameObject.AddComponent<Image>();
            CopyLook(face, look);
            picker.Button = button.gameObject.AddComponent<Button>();
            picker.Button.targetGraphic = face;
            picker.Label = NewLabel(button, sample, "SERVER · EUROPE");

            // The list: one row a region, under the button, over the room panel while open.
            float rowHeight = 30f * scale;
            RectTransform list = NewRect("ServerList", board, where);
            list.sizeDelta = new Vector2(size.x, (rowHeight * OnlineRegions.Codes.Length) + (8f * scale));
            list.anchoredPosition = button.anchoredPosition + new Vector2(0f, -size.y - (4f * scale));
            Image back = list.gameObject.AddComponent<Image>();
            CopyLook(back, look);
            back.color = new Color(0.07f, 0.08f, 0.09f, 0.98f);
            for (int i = 0; i < OnlineRegions.Codes.Length; i++)
            {
                string code = OnlineRegions.Codes[i];
                RectTransform row = NewRect("Region" + i, list, null);
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(-8f * scale, rowHeight);
                row.anchoredPosition = new Vector2(0f, -(4f * scale) - (i * rowHeight));
                Image catcher = row.gameObject.AddComponent<Image>();
                catcher.color = new Color(1f, 1f, 1f, 0f);
                Button pick = row.gameObject.AddComponent<Button>();
                pick.targetGraphic = catcher;
                ColorBlock colours = pick.colors;
                colours.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
                pick.colors = colours;
                NewLabel(row, sample, OnlineRegions.Names[i]);
                pick.onClick.AddListener(() =>
                {
                    CloseServerLists();
                    actions?.SetOnlineRegion?.Invoke(code);
                });
            }

            list.gameObject.SetActive(false);
            picker.List = list.gameObject;
            picker.Button.onClick.AddListener(() =>
            {
                bool open = !picker.List.activeSelf;
                CloseServerLists();
                if (open)
                {
                    picker.List.transform.SetAsLastSibling();
                    picker.List.SetActive(true);
                }
            });
            serverPickers[at] = picker;
        }

        private void RenderServerPicker(string at, OnlineScreenModel online)
        {
            if (!serverPickers.TryGetValue(at, out ServerPicker picker) || picker.Label == null) return;
            string region = online.InRoom && !string.IsNullOrEmpty(online.RoomCode) && online.RoomCode.IndexOf('-') > 0
                ? online.RoomCode.Substring(0, online.RoomCode.IndexOf('-'))
                : online.ServerRegion;
            picker.Label.text = $"SERVER · {OnlineRegions.NameOf(region)}";
            picker.Button.interactable = !online.InRoom;
            if (online.InRoom && picker.List != null && picker.List.activeSelf) picker.List.SetActive(false);
        }

        private void CloseServerLists()
        {
            foreach (ServerPicker picker in serverPickers.Values)
                if (picker.List != null && picker.List.activeSelf) picker.List.SetActive(false);
        }

        private static RectTransform NewRect(string name, Transform parent, RectTransform likeAnchors)
        {
            GameObject made = new GameObject(name, typeof(RectTransform));
            made.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)made.transform;
            rect.SetParent(parent, false);
            if (likeAnchors != null)
            {
                rect.anchorMin = likeAnchors.anchorMin;
                rect.anchorMax = likeAnchors.anchorMax;
                rect.pivot = likeAnchors.pivot;
            }

            return rect;
        }

        private static void CopyLook(Image image, Image look)
        {
            image.sprite = look.sprite;
            image.type = look.type;
            image.pixelsPerUnitMultiplier = look.pixelsPerUnitMultiplier;
            image.color = look.color;
        }

        private static Text NewLabel(RectTransform parent, Text sample, string words)
        {
            RectTransform rect = NewRect("Label", parent, null);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = sample.font;
            text.fontSize = sample.fontSize;
            text.fontStyle = sample.fontStyle;
            text.color = sample.color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = words;
            return text;
        }

        // PRIVATE sits just after the room's name.
        private void PlaceAfterName(string parts)
        {
            if (!texts.TryGetValue(parts + "Name", out Text name) || name == null) return;
            Transform tag = transform.Find(parts + "Private");
            if (tag == null) return;
            RectTransform rect = (RectTransform)tag;
            RectTransform nameRect = name.rectTransform;
            rect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x + Mathf.Min(name.preferredWidth, nameRect.rect.width) + 18f,
                rect.anchoredPosition.y);
        }

        private void PlaceDetailAfterName(string row)
        {
            if (!texts.TryGetValue(row + "/Name", out Text name) || name == null) return;
            if (!texts.TryGetValue(row + "/Detail", out Text detail) || detail == null) return;
            RectTransform nameRect = name.rectTransform;
            RectTransform detailRect = detail.rectTransform;
            float x = nameRect.anchoredPosition.x + Mathf.Min(name.preferredWidth, nameRect.rect.width) + 12f;
            detailRect.anchoredPosition = new Vector2(x, detailRect.anchoredPosition.y);
        }

        private void Move(string path, float x, float y)
        {
            Transform found = transform.Find(path);
            if (found != null) ((RectTransform)found).anchoredPosition = new Vector2(x * 1.5f, -y * 1.5f);
        }
    }
}
