namespace SleepingCalamity.Runtime
{
    // EventBus message; host dev panel -> WorldSaveFlow : retire this machine's world file, so the next hosted session starts a
    // new world (the current one carries on unsaved). Debug builds only.
    public readonly struct DevNewWorldRequested
    {
    }
}
