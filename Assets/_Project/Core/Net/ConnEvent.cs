namespace SleepingCalamity.Core
{
    // Inputs to ConnectionStateMachine.
    public enum ConnEvent : byte { Begin = 0, Established = 1, Fail = 2, UnexpectedStop = 3, Leave = 4, ShutdownComplete = 5 }
}
