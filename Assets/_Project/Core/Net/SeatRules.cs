// Core/Net/SeatRules.cs: when a disconnect frees a player's seat in the online session (Lobby membership). Pure C#, no engine references.
//
// Authority: host only (the host owns the UGS session and is the only peer that removes members). UgsNetworkSession frees the seat of a client
// that disconnected, or a rejoin would be refused as "full". A takeover makes that wrong (audit A1, 2026-11-12): when a player's game crashes and
// they relaunch and join by code before the host's transport has timed the old connection out, ConnectionApprover.TakeOverStaleConnection kicks
// their OLD client id while their NEW one is already approved. NGO then raises ClientDisconnected for the old id, and freeing the seat by that
// client's player id would remove the player who is rejoining right now. So the seat is freed only while no OTHER connection of the same player
// is up or approved.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    // One client that holds (or is about to hold) a seat: its NGO client id and the service player id behind it.
    public readonly struct SeatClaim
    {
        public readonly ulong ClientId;
        public readonly string PlayerId;

        public SeatClaim(ulong clientId, string playerId)
        {
            ClientId = clientId;
            PlayerId = playerId;
        }
    }

    public static class SeatRules
    {
        // True when the disconnect of goneClientId should free playerId's seat. claims are every other client the host knows of (connected or
        // approved and connecting), in any order; the gone client's own claim is ignored, and claims may be null or empty.
        public static bool ShouldFreeSeat(string playerId, ulong goneClientId, IReadOnlyList<SeatClaim> claims)
        {
            if (string.IsNullOrEmpty(playerId)) return false;   // nothing to free: with no id the host could only guess at a member
            if (claims == null) return true;
            for (int i = 0; i < claims.Count; i++)
            {
                if (claims[i].ClientId == goneClientId) continue;
                if (string.Equals(claims[i].PlayerId, playerId, StringComparison.Ordinal)) return false;   // the same player is joining or still here
            }
            return true;
        }
    }
}
