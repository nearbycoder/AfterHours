using System;
using UnityEngine;

namespace AfterHours
{
    /// <summary>An interactable whose prompt and action are supplied by a night script.</summary>
    public class ScriptedUse : MonoBehaviour, IInteractable
    {
        public Func<string> PromptFn;
        public Action Use;

        public static ScriptedUse Attach(GameObject go, Func<string> prompt, Action use)
        {
            var s = go.GetComponent<ScriptedUse>() ?? go.AddComponent<ScriptedUse>();
            s.PromptFn = prompt;
            s.Use = use;
            if (!go.GetComponentInChildren<Collider>())
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = Vector3.one * 0.3f;
            }
            return s;
        }

        public string Prompt(Interactor who) => PromptFn?.Invoke();
        public void Interact(Interactor who) => Use?.Invoke();
    }
}
