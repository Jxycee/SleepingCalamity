// Core/Game/UiText.cs: the words a player reads for every refusal, end reason and toast key the game can publish (PL-3 task 3.1). Pure C#, no engine
// references. Not networked: every peer turns keys into words locally. The game's key tables (WorldStartText, EndReasonText, the toasts WorldSaveFlow
// and SceneFlow publish) stay as they are; this is the one table that gives them text, so a screen never shows "world.library_full".
// Adding a toast key anywhere means adding it to PublishedToastKeys and to Toast below; UiTextTests scans the source for ToastRequested keys and fails
// on one that is missing.
using System;

namespace SleepingCalamity.Core
{
    public static class UiText
    {
        // Every toast key the game publishes today, kept beside the table so a test can check them: the keys of WorldStartText and EndReasonText,
        // plus WorldSaveFlow's "world.library_full" (arg: the WorldKind name) and "save.world_read_only" / "save.world_load_failed".
        public static readonly string[] PublishedToastKeys =
        {
            "world.library_full", "save.world_read_only", "save.world_load_failed",
            "world.need_two_players", "world.not_a_player", "world.wrong_kind", "world.solo_alone", "world.too_many_players", "world.start_refused",
            "err.host_left", "err.rejected", "err.wrong_world", "err.timeout", "err.session_full", "err.joins_closed", "err.version",
            "err.code_not_found", "err.services", "err.network", "err.generic",
        };

        // The words for a toast key (arg is the toast's optional value). Null for a key this table does not know.
        public static string Toast(string key, string arg = null)
        {
            switch (key)
            {
                case "world.library_full":
                    return "You already have " + WorldSummary.MaxPerList + " " + KindWord(arg) + " worlds. Pick one to play, or delete one to make room.";
                case "save.world_read_only":
                    return "This world was saved by a newer version of the game. You can play it, but your progress will not be saved.";
                case "save.world_load_failed":
                    return "This world's save file could not be read, so it is open read-only. Your progress will not be saved.";

                case "world.need_two_players": return "A multiplayer world needs at least 2 of its players connected to start. Wait for a friend to join.";
                case "world.not_a_player": return "Someone connected is not a player of this world. Only the players a world has recorded can play it.";
                case "world.wrong_kind": return "This world was made for a different kind of game (local or online) and cannot be played this way.";
                case "world.solo_alone": return "A solo world is played alone.";
                case "world.too_many_players": return "A world can have up to 4 players.";
                case "world.start_refused": return "The game could not start.";

                case "err.host_left": return "The host left, so the game has ended.";
                case "err.rejected": return "The host did not let you in.";
                case "err.wrong_world": return "This world was made for a different kind of game (local or online) and cannot be opened this way.";
                case "err.timeout": return "Could not reach the host in time. Check the code and your connection.";
                case "err.session_full": return "That game is full.";
                case "err.joins_closed": return "That game is not taking new players.";
                case "err.version": return "Your game version does not match the host's. Both players need the same version.";
                case "err.code_not_found": return "No game was found with that code. Check the code and try again.";
                case "err.services": return "The online service is not available right now. Try again in a moment.";
                case "err.network": return "The connection was lost.";
                case "err.generic": return "Something went wrong with the connection.";
                default: return null;
            }
        }

        // Why the host cannot start now (the start gate's verdict). Null for Ok. The toast a refused start publishes shows the same words.
        public static string StartRefusal(WorldPlayerVerdict verdict)
        {
            if (verdict == WorldPlayerVerdict.Ok) return null;
            return Toast(WorldStartText.ToastKey(verdict)) ?? Toast("world.start_refused");
        }

        // Why a session ended, for the screen that tells the player. Null when the player chose it (UserLeft): nothing to explain.
        public static string EndedBecause(EndReason reason)
        {
            string key = EndReasonText.ToastKey(reason);
            return key == null ? null : (Toast(key) ?? Toast("err.generic"));
        }

        // Why a join attempt did not work, shown on the join screen. Null for UserLeft (a cancel).
        public static string JoinRefusal(EndReason reason) => EndedBecause(reason);

        // ---- the front door's own lines ----

        public const string JoinCodeEmpty = "Type the code your host shows you.";
        public const string JoinCodeInvalid = "A code has only letters and numbers.";
        public const string WaitingForHost = "You are in. Waiting for the host to start the game.";
        public const string Connecting = "Connecting...";
        public const string DeleteAsk = "Delete this world? It is set aside and no longer listed.";
        public const string WorldDeleted = "World set aside.";
        public const string WorldNotDeleted = "That world could not be deleted.";
        public const string NeedsAWorldToHost = "Pick a multiplayer world to host.";

        // Worlds list full (WorldSummary.MaxPerList per kind): New world is off and says why.
        public static string WorldLibraryFull(WorldKind kind) => Toast("world.library_full", kind.ToString());

        private static string KindWord(string arg)
        {
            return string.Equals(arg, WorldKind.Multiplayer.ToString(), StringComparison.Ordinal) ? "multiplayer" : "solo";
        }
    }
}
