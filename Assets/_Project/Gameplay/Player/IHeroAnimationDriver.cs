// Not networked. The animation seam: whatever draws the hero (the placeholder mannequin now, Mixamo clips through an Animator later) implements
// this on the hero's visual and is fed every frame by HeroAnimationFeeder, on the owner and on remote copies alike.
using SleepingCalamity.Core;

namespace SleepingCalamity.Gameplay
{
    public interface IHeroAnimationDriver
    {
        void Apply(in HeroAnimState state, float deltaTime);
    }
}
