using System;
using System.Collections.Generic;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Audit T-3 and the owner's late-join rule (2026-11-14).
    /// <para>
    /// T-3, leave detection: Fusion's leave event sometimes never came, so a gone guest's seat waited for ever. A joined
    /// computer now says "still here" every <see cref="AliveSeconds"/>; a guest that has said it at least once and then
    /// stays silent for <see cref="AliveTimeoutSeconds"/> is treated as having lost its connection (the seat waits, as
    /// for any dropped connection). Copies that never send it are never timed out.
    /// </para>
    /// <para>
    /// Late join (owner, 03:16: "if the game has started then late joiners can only join with a request and if host
    /// allows it then join and do the syncs"; 03:20: a player coming back to their own seat is not asked): once a game is
    /// on, a newcomer's hello waits; the host is asked to let them in or refuse. Allowed: the hello is used as usual (an
    /// open seat or watching, the content the table was dealt with, then the table). Refused or unanswered for
    /// <see cref="JoinRequestSeconds"/>: told why and disconnected.
    /// </para>
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        private const byte MsgAlive = 26; // guest → host, unordered, no payload
        private const float AliveSeconds = 2f;
        private const float AliveTimeoutSeconds = 12f;
        private const float JoinRequestSeconds = 60f;

        private float nextAliveAt;
        private int admittingConnection = -1;

        private sealed class JoinRequest
        {
            public int Connection;
            public byte[] Hello;
            public string Name;
            public float Until;
        }

        private readonly List<JoinRequest> joinRequests = new List<JoinRequest>(2);

        // A joined computer: "still here", every few seconds while in a room (no allocation: the reused message stream).
        private void TickAlive(float now)
        {
            if (hosting || !welcomed || !InRoom || now < nextAliveAt) return;
            nextAliveAt = now + AliveSeconds;
            BeginMessage(MsgAlive);
            SendToHost();
        }

        // The host: every message from a member shows it is still there.
        private void NoteHeardFrom(Member member, byte type)
        {
            member.LastHeardAt = Time.unscaledTime;
            if (type == MsgAlive) member.SendsAlive = true;
        }

        // The host: a guest that used to say "still here" and went silent has lost its connection.
        private void TickSilentMembers(float now)
        {
            for (int i = members.Count - 1; i >= 0; i--)
            {
                Member member = members[i];
                if (member.IsHost || !member.Connected || member.Connection < 0 || !member.SendsAlive) continue;
                if (now - member.LastHeardAt <= AliveTimeoutSeconds) continue;
                int connection = member.Connection;
                Debug.Log($"[Online] {member.Name} has sent nothing for {AliveTimeoutSeconds:0} s: treated as a lost connection.");
                member.SendsAlive = false;
                OnConnectionLeft(connection);
                session.Disconnect(connection);
            }
        }

        // The host: a newcomer's hello while a game is on waits for the host's answer. True when it was held back.
        private bool HoldLateJoiner(int connection, byte[] hello, string name)
        {
            if (!sharedTable || privateHosting || connection == admittingConnection) return false;
            for (int i = 0; i < joinRequests.Count; i++)
            {
                if (joinRequests[i].Connection == connection) return true; // asked already
            }

            string shown = string.IsNullOrEmpty(name) ? "A player" : name;
            joinRequests.Add(new JoinRequest { Connection = connection, Hello = hello, Name = shown, Until = Time.unscaledTime + JoinRequestSeconds });
            waiting.Add(new Waiting { Connection = connection, Until = Time.unscaledTime + JoinRequestSeconds + 5f });
            BeginMessage(MsgNotice);
            writer.Write("The game has already started. The host has been asked to let you in…");
            Flush();
            session.SendTo(connection, outStream.GetBuffer(), 0, (int)outStream.Length);
            Debug.Log($"[Online] {shown} asked to join the game in progress (connection {connection}); waiting for the host.");
            AskHostAboutJoin(joinRequests[joinRequests.Count - 1]);
            return true;
        }

        private void AskHostAboutJoin(JoinRequest request)
        {
            host.ShowNotice($"{request.Name} asks to join the game in progress.");
            if (host.runtimeUi == null) return;
            List<PopupActionOption> actions = new List<PopupActionOption>(2)
            {
                new PopupActionOption($"Let {request.Name} join", true, () => AnswerJoin(request.Connection, true)),
                new PopupActionOption("Refuse", true, () => AnswerJoin(request.Connection, false)),
            };
            host.runtimeUi.ShowContextMenu(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                "JOIN REQUEST",
                $"{request.Name} wants to join this game. Allowed, they take an open seat (or watch) and get the table as it is now. "
                    + $"No answer in {JoinRequestSeconds:0} s refuses.",
                actions,
                host.ContextMenus.CloseContextMenu,
                host.ContextMenus.DismissPopupFromSecondary);
        }

        private void AnswerJoin(int connection, bool allow)
        {
            host.ContextMenus.CloseContextMenu();
            JoinRequest request = TakeJoinRequest(connection);
            if (request == null) return;
            if (!allow)
            {
                Debug.Log($"[Online] The host refused {request.Name}'s request to join.");
                Kick(connection, "The host did not let you join this game. Try again between games.");
                return;
            }

            Debug.Log($"[Online] The host let {request.Name} join the game in progress.");
            admittingConnection = connection;
            try
            {
                ReadHello(connection, request.Hello); // the usual join: seat or watching, content, then the table
            }
            finally
            {
                admittingConnection = -1;
            }
        }

        private JoinRequest TakeJoinRequest(int connection)
        {
            for (int i = 0; i < joinRequests.Count; i++)
            {
                if (joinRequests[i].Connection != connection) continue;
                JoinRequest request = joinRequests[i];
                joinRequests.RemoveAt(i);
                for (int w = waiting.Count - 1; w >= 0; w--)
                {
                    if (waiting[w].Connection == connection && !waiting[w].Kicked) waiting.RemoveAt(w);
                }

                return request;
            }

            return null;
        }

        // The host: requests left unanswered are refused; a requester that left is forgotten.
        private void TickJoinRequests(float now)
        {
            for (int i = joinRequests.Count - 1; i >= 0; i--)
            {
                JoinRequest request = joinRequests[i];
                if (now < request.Until) continue;
                TakeJoinRequest(request.Connection);
                Kick(request.Connection, "The host did not answer your request to join. Try again between games.");
                host.ShowNotice($"{request.Name}'s request to join was not answered and was refused.");
            }
        }

        private void ForgetJoinRequest(int connection)
        {
            if (TakeJoinRequest(connection) != null) Debug.Log($"[Online] A join request was dropped: connection {connection} closed.");
        }
    }
}
