using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ConsoleCards.Application.Authority
{
    /// <summary>
    /// REJOIN-1 (owner, 2026-11-05: "restore the existing seat after restarting when the session and authorization
    /// remain valid"): the secret seat key a computer uses in one room, kept on disk per profile so a restarted game
    /// presents the same key and gets its seat back. One key per room code; keys older than <see cref="MaxAge"/> are
    /// dropped (an expired credential starts as a new member). The key never goes into the roster or a snapshot: only
    /// its public id does (SEC-1). Pure C#: the caller reads and writes the text.
    /// </summary>
    public sealed class RejoinCredentialBook
    {
        public static readonly TimeSpan MaxAge = TimeSpan.FromHours(12);
        public const int MaxRooms = 16;

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<Guid> newKey;

        private struct Entry
        {
            public Guid Key;
            public DateTime SavedUtc;
        }

        public RejoinCredentialBook(Func<Guid> newKey = null)
        {
            this.newKey = newKey ?? Guid.NewGuid;
        }

        public int Count => entries.Count;

        /// <summary>Reads the saved text ("code key savedUtc" per line); bad or expired lines are skipped.</summary>
        public static RejoinCredentialBook Parse(string text, DateTime nowUtc, Func<Guid> newKey = null)
        {
            RejoinCredentialBook book = new RejoinCredentialBook(newKey);
            if (string.IsNullOrEmpty(text)) return book;
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Trim().Split(' ');
                if (parts.Length != 3 || parts[0].Length == 0) continue;
                if (!Guid.TryParse(parts[1], out Guid key) || key == Guid.Empty) continue;
                if (!DateTime.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime saved)) continue;
                saved = saved.ToUniversalTime();
                if (saved > nowUtc + TimeSpan.FromMinutes(5) || nowUtc - saved > MaxAge) continue; // expired (or from the future)
                book.entries[parts[0]] = new Entry { Key = key, SavedUtc = saved };
            }

            return book;
        }

        /// <summary>
        /// The key for a room: the saved one while it is valid (renewed), otherwise a new one. <paramref name="reused"/>
        /// says which. An empty room code gets a key that is not saved.
        /// </summary>
        public Guid KeyFor(string roomCode, DateTime nowUtc, out bool reused)
        {
            reused = false;
            if (string.IsNullOrWhiteSpace(roomCode)) return newKey();
            if (entries.TryGetValue(roomCode, out Entry entry) && nowUtc - entry.SavedUtc <= MaxAge && entry.Key != Guid.Empty)
            {
                reused = true;
                entries[roomCode] = new Entry { Key = entry.Key, SavedUtc = nowUtc };
                return entry.Key;
            }

            Guid key = newKey();
            entries[roomCode] = new Entry { Key = key, SavedUtc = nowUtc };
            Trim();
            return key;
        }

        public bool Forget(string roomCode) => !string.IsNullOrEmpty(roomCode) && entries.Remove(roomCode);

        public string Serialize()
        {
            StringBuilder text = new StringBuilder();
            foreach (KeyValuePair<string, Entry> pair in entries)
            {
                text.Append(pair.Key.Replace(' ', '_')).Append(' ')
                    .Append(pair.Value.Key.ToString("N")).Append(' ')
                    .Append(pair.Value.SavedUtc.ToString("o", CultureInfo.InvariantCulture)).Append('\n');
            }

            return text.ToString();
        }

        private void Trim()
        {
            while (entries.Count > MaxRooms)
            {
                string oldest = null;
                DateTime oldestTime = DateTime.MaxValue;
                foreach (KeyValuePair<string, Entry> pair in entries)
                {
                    if (pair.Value.SavedUtc < oldestTime)
                    {
                        oldestTime = pair.Value.SavedUtc;
                        oldest = pair.Key;
                    }
                }

                if (oldest == null) return;
                entries.Remove(oldest);
            }
        }
    }

    /// <summary>
    /// REJOIN-1: what the host does with a hello whose key matches a member. Pure, so the rules can be tested.
    /// </summary>
    public enum RejoinDecision
    {
        /// <summary>No member has this key: a new member (a watcher if the table is full and playing).</summary>
        NewMember = 0,
        /// <summary>The member's computer is gone (restart, crash, lost connection): it takes its seat back.</summary>
        Restore = 1,
        /// <summary>The same running game reconnected before its old connection timed out: swap connections.</summary>
        ReplaceSameInstance = 2,
        /// <summary>Another running copy presents a key whose seat is still connected: refused (no take-over).</summary>
        RefuseDuplicate = 3,
    }

    public static class RejoinRules
    {
        public static RejoinDecision Decide(bool memberFound, bool memberConnected, Guid memberInstance, Guid helloInstance)
        {
            if (!memberFound) return RejoinDecision.NewMember;
            if (!memberConnected) return RejoinDecision.Restore;
            // Only the very same running copy may swap its connection; anything else waits until the seat is free.
            return helloInstance != Guid.Empty && helloInstance == memberInstance
                ? RejoinDecision.ReplaceSameInstance
                : RejoinDecision.RefuseDuplicate;
        }
    }
}
