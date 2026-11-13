// Tests/EditMode/SeatRulesTests.cs: the Lobby-seat rule (Core/Net/SeatRules; audit A1, 2026-11-12). Pure Core.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class SeatRulesTests
    {
        static List<SeatClaim> Claims(params SeatClaim[] claims) => new List<SeatClaim>(claims);

        // Catches: a disconnect that frees nobody's seat (a player who really left would be stuck as a member of a full Lobby).
        [Test]
        public void TheOnlyConnectionOfAPlayer_FreesTheSeat()
        {
            Assert.That(SeatRules.ShouldFreeSeat("P", 1, Claims()), Is.True);
            Assert.That(SeatRules.ShouldFreeSeat("P", 1, Claims(new SeatClaim(2, "Q"))), Is.True, "another player's connection is not P's");
        }

        // Catches (audit A1): the stale-connection takeover freeing the Lobby seat of the player who is rejoining right now. The approver kicks
        // key P's old client 1 while P's new client 2 is approved; the kick's disconnect event must not remove P from the session.
        [Test]
        public void ANewerConnectionOfTheSamePlayer_KeepsTheSeat()
        {
            Assert.That(SeatRules.ShouldFreeSeat("P", 1, Claims(new SeatClaim(2, "P"))), Is.False, "P is joining right now");
            Assert.That(SeatRules.ShouldFreeSeat("P", 1, Claims(new SeatClaim(2, "Q"), new SeatClaim(3, "P"))), Is.False);
        }

        // Catches: the gone client's own claim (a cache entry not cleared yet) reading as a newer connection, so no seat is ever freed.
        [Test]
        public void TheGoneClientsOwnClaim_IsIgnored()
        {
            Assert.That(SeatRules.ShouldFreeSeat("P", 1, Claims(new SeatClaim(1, "P"))), Is.True);
        }

        // Catches: a seat removal asked for with no player id (every member of the Lobby shares "", so the wrong one could go).
        [Test]
        public void WithoutAPlayerId_NothingIsFreed()
        {
            Assert.That(SeatRules.ShouldFreeSeat(null, 1, Claims()), Is.False);
            Assert.That(SeatRules.ShouldFreeSeat("", 1, Claims()), Is.False);
            Assert.That(SeatRules.ShouldFreeSeat("P", 1, null), Is.True, "no claims at all is still a real disconnect");
        }
    }
}
