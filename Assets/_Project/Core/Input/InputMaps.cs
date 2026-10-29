// Core, no engine references. Which action maps are enabled for a context.
using System;

namespace SleepingCalamity.Core
{
    [Flags]
    public enum InputMaps : byte
    {
        None = 0,
        Player = 1,
        UI = 2
    }
}
