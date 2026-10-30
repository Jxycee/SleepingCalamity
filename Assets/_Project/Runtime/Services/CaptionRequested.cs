namespace SleepingCalamity.Runtime
{
    // EventBus message. Raised by gameplay feedback when settings.captions is on; shown bottom-centre by the HUD presenter.
    // Key is a UiStrings key.
    public readonly struct CaptionRequested
    {
        public readonly string Key;
        public readonly float Seconds;

        public CaptionRequested(string key, float seconds = 2f)
        {
            Key = key;
            Seconds = seconds;
        }
    }
}
