using UnityEngine;

namespace AfterHours
{
    /// <summary>Title menu (placeholder until the menus milestone).</summary>
    public static class TitleScreen
    {
        public static void ShowTitle()
        {
            Story.State = new StoryState();
            GameRoot.Instance.StartNight(1);
        }
    }
}
