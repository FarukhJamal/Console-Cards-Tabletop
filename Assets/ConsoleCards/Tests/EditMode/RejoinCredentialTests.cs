using System;
using ConsoleCards.Application.Authority;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>REJOIN-1 (2026-11-05): the saved seat key and the host's rejoin rules. Pure C#.</summary>
    public sealed class RejoinCredentialTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 5, 18, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Restart_ReusesTheSavedKey_ForTheSameRoom()
        {
            RejoinCredentialBook first = new RejoinCredentialBook();
            Guid key = first.KeyFor("eu-ABC123", Now, out bool reused);
            Assert.IsFalse(reused);
            RejoinCredentialBook afterRestart = RejoinCredentialBook.Parse(first.Serialize(), Now.AddMinutes(3));
            Assert.AreEqual(key, afterRestart.KeyFor("eu-ABC123", Now.AddMinutes(3), out reused));
            Assert.IsTrue(reused);
        }

        [Test]
        public void AnotherRoom_GetsAnotherKey()
        {
            RejoinCredentialBook book = new RejoinCredentialBook();
            Guid a = book.KeyFor("eu-AAAAAA", Now, out _);
            Guid b = book.KeyFor("eu-BBBBBB", Now, out _);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void ExpiredCredential_IsDropped_AndANewKeyIsMade()
        {
            RejoinCredentialBook book = new RejoinCredentialBook();
            Guid old = book.KeyFor("eu-ABC123", Now, out _);
            DateTime later = Now + RejoinCredentialBook.MaxAge + TimeSpan.FromMinutes(1);
            RejoinCredentialBook reloaded = RejoinCredentialBook.Parse(book.Serialize(), later);
            Assert.AreEqual(0, reloaded.Count);
            Assert.AreNotEqual(old, reloaded.KeyFor("eu-ABC123", later, out bool reused));
            Assert.IsFalse(reused);
        }

        [Test]
        public void DamagedFile_IsIgnored()
        {
            RejoinCredentialBook book = RejoinCredentialBook.Parse("garbage\nx y z\n eu-1 not-a-guid 2026-11-05\n", Now);
            Assert.AreEqual(0, book.Count);
        }

        [Test]
        public void SavedText_HoldsNoNamesOrPublicIds_OnlyTheRoomKeyAndTime()
        {
            RejoinCredentialBook book = new RejoinCredentialBook(() => new Guid("0123456789abcdef0123456789abcdef"));
            book.KeyFor("eu-ABC123", Now, out _);
            Assert.AreEqual("eu-ABC123 0123456789abcdef0123456789abcdef " + Now.ToString("o") + "\n", book.Serialize());
        }

        [Test]
        public void Rules_RestoreAGoneSeat_RefuseASecondCopy_SwapTheSameCopy()
        {
            Guid copyA = Guid.NewGuid();
            Guid copyB = Guid.NewGuid();
            Assert.AreEqual(RejoinDecision.NewMember, RejoinRules.Decide(false, false, Guid.Empty, copyA));
            Assert.AreEqual(RejoinDecision.Restore, RejoinRules.Decide(true, false, copyA, copyB), "restart: the old copy is gone");
            Assert.AreEqual(RejoinDecision.ReplaceSameInstance, RejoinRules.Decide(true, true, copyA, copyA), "same copy reconnecting");
            Assert.AreEqual(RejoinDecision.RefuseDuplicate, RejoinRules.Decide(true, true, copyA, copyB), "second copy while seated");
            Assert.AreEqual(RejoinDecision.RefuseDuplicate, RejoinRules.Decide(true, true, copyA, Guid.Empty), "no instance id: no take-over");
        }
    }
}
