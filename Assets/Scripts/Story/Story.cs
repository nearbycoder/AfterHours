using UnityEngine;

namespace AfterHours
{
    /// <summary>Global access to the story state plus the shared reactions to reading things.</summary>
    public static class Story
    {
        public static StoryState State = new();

        /// <summary>Learn the doc's phrase and count it as a secret if the night lists it.</summary>
        public static void OnDocRead(DocDef d)
        {
            if (d == null) return;
            Events.Raise(GameEvent.EvidenceFound, d.Id);
            NightDirector.Instance?.FindSecret(d.Id);
            if (State.Learn(d.Phrase))
            {
                Tween.Delay(0.9f, () =>
                {
                    Hud.Instance?.Toast("New lead", "\"" + Phrases.Text[d.Phrase] + "\"", Palette.Sticky, 3.2f);
                    Sfx.Play("notify", null, 0.5f);
                });
                Events.Raise(GameEvent.PhraseLearned, d.Phrase);
            }
        }
    }
}
