using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>MP-2: what the Lobby and Room screens show (built by the online presenter for each refresh).</summary>
    public sealed class OnlineScreenModel
    {
        public bool Available;
        public string Status = string.Empty;
        public bool Connected;
        public bool Busy;
        public bool InRoom;
        public bool IsHost;
        public string RoomTitle = string.Empty;
        public string RoomCode = string.Empty;
        public bool RoomPrivate;
        public string DefaultRoomTitle = string.Empty;
        public IReadOnlyList<OnlineRoomRow> Rooms = Array.Empty<OnlineRoomRow>();
        public IReadOnlyList<OnlineMemberRow> Members = Array.Empty<OnlineMemberRow>();
        public string GameText = string.Empty;
        public bool Started;
        public bool Ready;
        public bool CanReady;
        public string ColourName = string.Empty;
        public Color Colour = Color.grey;
        public string SeatText = string.Empty;
        public bool CanChangeSeat;
        public bool AllReady = true;
        public int Seats;
    }

    /// <summary>One room in the Lobby's list.</summary>
    public sealed class OnlineRoomRow
    {
        public string Code = string.Empty;
        public string Title = string.Empty;
        public string Detail = string.Empty;
        public bool CanJoin;
    }

    /// <summary>One player in the Room.</summary>
    public sealed class OnlineMemberRow
    {
        public string Badge = string.Empty;
        public string Name = string.Empty;
        public string Tags = string.Empty;
        public Color Colour = Color.grey;
        public bool IsYou;
    }

    /// <summary>
    /// MP-2 (owner, 2026-10-26: "two options… join a server or create your own… privacy to your friends or to the
    /// world"): the Lobby (make a room, public or private; join one from the list or by its code) and the Room (the
    /// players with their seat, colour and READY; the room's code to share; the host's CHOOSE GAME, which goes through
    /// the usual Library, Details and setup steps to START). Both are their own prefabs (FE_Lobby, FE_Room); a front end
    /// made before them works as before and says how to add them.
    /// </summary>
    public sealed partial class FrontEndView
    {
        public const int RoomRowCount = 6;
        public const int MemberRowCount = 8;

        private bool roomPrivate;

        private bool InOnlineRoom => model != null && model.Online != null && model.Online.InRoom;

        private FrontEndPage PlayPage => InOnlineRoom ? FrontEndPage.Room : FrontEndPage.Profile;

        private static bool IsOnlinePage(FrontEndPage target) => target == FrontEndPage.Lobby || target == FrontEndPage.Room;

        private void BindOnlineButtons()
        {
            On("Pages/Profile/Online", OpenOnline);
            On("Pages/Lobby/Back", () => Go(FrontEndPage.Profile));
            On("Pages/Lobby/Create/Privacy", () =>
            {
                roomPrivate = !roomPrivate;
                Render();
            });
            On("Pages/Lobby/Create/Make", () => actions?.CreateRoom?.Invoke(FieldText("Pages/Lobby/Create/Name"), roomPrivate));
            On("Pages/Lobby/Join/Go", () => actions?.JoinRoom?.Invoke(FieldText("Pages/Lobby/Join/Code")));
            for (int i = 0; i < RoomRowCount; i++)
            {
                int row = i;
                On("Pages/Lobby/Rooms/Row" + i, () => JoinListed(row));
            }

            On("Pages/Room/Leave", () => actions?.LeaveRoom?.Invoke());
            On("Pages/Room/Code/Copy", CopyRoomCode);
            On("Pages/Room/Controls/Ready", () => actions?.ToggleReady?.Invoke());
            On("Pages/Room/Controls/Choose", () => Go(FrontEndPage.Library));
            On("Pages/Room/Controls/Colour/Previous", () => actions?.CycleOnlineColour?.Invoke(-1));
            On("Pages/Room/Controls/Colour/Next", () => actions?.CycleOnlineColour?.Invoke(1));
            On("Pages/Room/Controls/Seat/Previous", () => actions?.CycleOnlineSeat?.Invoke(-1));
            On("Pages/Room/Controls/Seat/Next", () => actions?.CycleOnlineSeat?.Invoke(1));
        }

        // PLAY ONLINE on the Player profile screen.
        private void OpenOnline()
        {
            if (pages[(int)FrontEndPage.Lobby] == null || pages[(int)FrontEndPage.Room] == null)
            {
                SetText("Status", "The online screens are not in the front end yet: run Console Cards > UI > Apply Kit Look (UI-1).");
                return;
            }

            actions?.OpenOnline?.Invoke();
        }

        private void JoinListed(int row)
        {
            if (model?.Online == null || row >= model.Online.Rooms.Count) return;
            actions?.JoinRoom?.Invoke(model.Online.Rooms[row].Code);
        }

        private void CopyRoomCode()
        {
            if (model?.Online == null || string.IsNullOrEmpty(model.Online.RoomCode)) return;
            GUIUtility.systemCopyBuffer = model.Online.RoomCode;
            SetText("Pages/Room/Status", $"Copied {model.Online.RoomCode}: friends type it in JOIN BY CODE.");
        }

        // MP-P (owner, 2026-10-27): the Players step's line said "local players at this table" in an online room too.
        private string offlinePlayersIntro;

        private void RenderPlayersIntro()
        {
            const string path = "Pages/Players/Text";
            if (!texts.TryGetValue(path, out Text text) || text == null) return;
            if (offlinePlayersIntro == null) offlinePlayersIntro = text.text;
            text.text = model.Online != null && model.Online.InRoom
                ? "Each seat plays in its colour. The room's players sit in their seat order;\nthe host plays any seat left open."
                : offlinePlayersIntro;
        }

        // MP-P (owner, 2026-10-27): a long status or room name shrinks to stay inside its place.
        private void FitLine(string path)
        {
            if (texts.TryGetValue(path, out Text text)) FitText.Shrink(text);
        }

        private string FieldText(string path)
        {
            InputField field = Field(path);
            return field != null ? field.text ?? string.Empty : string.Empty;
        }

        private InputField Field(string path)
        {
            Transform found = transform.Find(path);
            return found != null ? found.GetComponent<InputField>() : null;
        }

        private void TintImage(string path, Color colour)
        {
            Transform found = transform.Find(path);
            Image image = found != null ? found.GetComponent<Image>() : null;
            if (image != null) image.color = colour;
        }

        private void RenderLobby()
        {
            OnlineScreenModel online = model.Online;
            if (online == null) return;
            FitLine("Pages/Lobby/Status");
            SetText("Pages/Lobby/Status", online.Status);
            InputField name = Field("Pages/Lobby/Create/Name");
            if (name != null && string.IsNullOrEmpty(name.text)) name.text = online.DefaultRoomTitle;
            SetText("Pages/Lobby/Create/Privacy/Label", roomPrivate ? "PRIVATE  ·  only with the code" : "PUBLIC  ·  in the room list");
            SetInteractable("Pages/Lobby/Create/Make", online.Connected);
            SetInteractable("Pages/Lobby/Join/Go", online.Connected);
            IReadOnlyList<OnlineRoomRow> rooms = online.Rooms;
            for (int i = 0; i < RoomRowCount; i++)
            {
                string row = "Pages/Lobby/Rooms/Row" + i;
                bool shown = i < rooms.Count;
                SetActive(row, shown);
                if (!shown) continue;
                SetText(row + "/Title", Upper(rooms[i].Title));
                SetText(row + "/Detail", rooms[i].Detail);
                SetInteractable(row, rooms[i].CanJoin && online.Connected);
            }

            SetActive("Pages/Lobby/Rooms/Empty", rooms.Count == 0);
            SetText("Pages/Lobby/Rooms/Empty", online.Connected
                ? "No open rooms here yet: make one, or join a friend's with its code."
                : online.Busy ? "Connecting…" : "Not connected.");
        }

        private void RenderRoom()
        {
            OnlineScreenModel online = model.Online;
            if (online == null) return;
            SetText("Pages/Room/Title", Upper(online.RoomTitle.Length > 0 ? online.RoomTitle : "ROOM"));
            SetText("Pages/Room/Subtitle", online.IsHost ? "Y O U   H O S T   T H I S   R O O M" : "O N L I N E   R O O M");
            SetText("Pages/Room/Code/Label", "CODE  " + online.RoomCode + (online.RoomPrivate ? "  ·  PRIVATE" : "  ·  PUBLIC"));
            FitLine("Pages/Room/Status");
            FitLine("Pages/Room/Title");
            SetText("Pages/Room/Status", online.Status);
            IReadOnlyList<OnlineMemberRow> members = online.Members;
            for (int i = 0; i < MemberRowCount; i++)
            {
                string row = "Pages/Room/Players/Row" + i;
                bool shown = i < members.Count;
                SetActive(row, shown);
                if (!shown) continue;
                OnlineMemberRow member = members[i];
                SetText(row + "/Badge", member.Badge);
                SetText(row + "/Name", Upper(member.Name));
                SetText(row + "/Tags", member.Tags);
                TintImage(row + "/Dot", member.Colour);
                Tint(row + "/Name", member.IsYou ? activeTab : idleTab);
            }

            SetText("Pages/Room/Controls/Game", online.GameText);
            SetText("Pages/Room/Controls/Colour/Value", online.ColourName);
            TintImage("Pages/Room/Controls/Colour/Dot", online.Colour);
            SetText("Pages/Room/Controls/Seat/Value", online.SeatText);
            SetInteractable("Pages/Room/Controls/Seat/Previous", online.CanChangeSeat);
            SetInteractable("Pages/Room/Controls/Seat/Next", online.CanChangeSeat);
            SetActive("Pages/Room/Controls/Ready", !online.IsHost);
            SetText("Pages/Room/Controls/Ready/Label", online.Ready ? "READY  ·  YES" : "PRESS WHEN READY");
            SetInteractable("Pages/Room/Controls/Ready", online.CanReady);
            SetActive("Pages/Room/Controls/Choose", online.IsHost);
            SetText("Pages/Room/Controls/Choose/Label", online.Started ? "NEW GAME" : "CHOOSE GAME");
        }
    }
}
