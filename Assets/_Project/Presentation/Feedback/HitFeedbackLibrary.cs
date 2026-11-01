// Authority: none (asset data, cosmetic). Optional override set for the hit-feedback profiles: any entry here replaces the Core default with the same id
// (FeedbackProfiles.Defaults), so one data change retunes a weight class without code. FeedbackDirector loads Resources/HitFeedbackLibrary if one exists;
// without it the Core defaults apply. Create one with Assets > Create > Sleeping Calamity > Hit Feedback Library.
using System.Collections.Generic;
using SleepingCalamity.Core.Feedback;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [CreateAssetMenu(fileName = "HitFeedbackLibrary", menuName = "Sleeping Calamity/Hit Feedback Library")]
    public sealed class HitFeedbackLibrary : ScriptableObject
    {
        public List<FeedbackProfileData> profiles = new List<FeedbackProfileData>();

        public FeedbackProfileData Find(string id)
        {
            foreach (FeedbackProfileData p in profiles) if (p != null && p.Id == id) return p;
            return null;
        }
    }
}
