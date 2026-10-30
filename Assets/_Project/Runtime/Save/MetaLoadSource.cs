namespace SleepingCalamity.Runtime
{
    // Where a JsonSaveFile value came from on the last Load: nothing readable on disk, the main file, or the .bak.
    public enum MetaLoadSource : byte { Defaults = 0, Main = 1, Backup = 2 }
}
