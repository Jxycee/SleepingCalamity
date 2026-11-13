// Core/World/WorldStartText.cs: the message a refused session start shows, as a UI string key. The start gate never fails silently (user
// ruling 2026-10-30): the host sees why it cannot start, whichever rule said no.
namespace SleepingCalamity.Core
{
    public static class WorldStartText
    {
        public static string ToastKey(WorldPlayerVerdict verdict)
        {
            switch (verdict)
            {
                case WorldPlayerVerdict.Ok: return null;
                case WorldPlayerVerdict.NotEnoughPlayers: return "world.need_two_players";   // a multiplayer world needs 2 or more of its players connected
                case WorldPlayerVerdict.NotRecorded: return "world.not_a_player";            // someone connected (the host too) is not a player of this world
                case WorldPlayerVerdict.WrongIdentityKind: return "world.wrong_kind";        // a local world cannot be played online, or the other way round
                case WorldPlayerVerdict.WrongPlayerCount: return "world.solo_alone";         // a solo world is played alone
                case WorldPlayerVerdict.TooManyPlayers: return "world.too_many_players";
                case WorldPlayerVerdict.WorldFull: return "world.world_full";                // the start would record someone, and the world's 4 places are taken
                default: return "world.start_refused";
            }
        }
    }
}
