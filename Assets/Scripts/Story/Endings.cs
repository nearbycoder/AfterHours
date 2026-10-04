using System.Collections.Generic;
using System.Linq;

namespace AfterHours
{
    public class EndingDef
    {
        public string Id, Title, Headline;
        public List<string> Lines = new();
    }

    /// <summary>
    /// Pure rules that turn the story state into one of four endings plus personal epilogues.
    /// Covered by EditMode tests (every ending reachable).
    /// </summary>
    public static class Endings
    {
        public static readonly string[] KeyDocs =
        {
            "theo_note", "theo_planner", "notepad_rubbing", "northgate_invoices", "reconstructed_invoice",
            "vpn_log", "walt_letter", "payment_ledger", "red_folder",
        };

        static readonly HashSet<string> AuditPhrases = new() { "northgate", "wire", "logins", "folder" };

        /// <summary>Did Priya get anything before the audit? She forwards all of it to Brightwater.</summary>
        public static bool PriyaForwards(StoryState s) => KeyDocs.Any(d => s.IsDelivered(d, "priya"));

        /// <summary>How much reached the auditor: key documents (direct or via Priya) plus up to two pointed notes.</summary>
        public static int AuditScore(StoryState s)
        {
            bool viaPriya = PriyaForwards(s);
            int docs = KeyDocs.Count(d => s.IsDelivered(d, "auditor") || (viaPriya && s.IsDelivered(d, "priya")));
            int notes = s.Notes.Where(n => n.To == "auditor" && AuditPhrases.Contains(n.Phrase)).Select(n => n.Phrase).Distinct().Count();
            return docs + System.Math.Min(2, notes);
        }

        public static bool Meddled(StoryState s) =>
            s.Evidence.Any(e => e.Fate is Fate.Kept or Fate.Delivered or Fate.Shredded or Fate.Trashed) || s.Notes.Count > 0;

        public static string ResolveId(StoryState s)
        {
            if (!Meddled(s)) return "spotless";
            var red = s.FateOf("red_folder");
            bool redToAuditor = s.IsDelivered("red_folder", "auditor");
            int score = AuditScore(s);
            if (redToAuditor && score - 1 >= 2) return "audit";
            if (red is Fate.Shredded or Fate.Trashed || s.IsDelivered("red_folder", "marian")) return "cleanbooks";
            if (score < 2 && s.IsDelivered("theo_note", "marian")) return "cleanbooks";
            return "loose";
        }

        public static EndingDef Resolve(StoryState s)
        {
            string id = ResolveId(s);
            s.Ending = id;
            var e = new EndingDef { Id = id };
            switch (id)
            {
                case "audit":
                    e.Title = "The Audit";
                    e.Headline = "FREIGHT FIRM FINANCE CHIEF ESCORTED FROM OFFICE AS AUDITORS UNCOVER SHELL VENDOR";
                    e.Lines.Add("Monday, 9:04 AM. Erin Sato opened the tray on the conference table, read for eleven minutes and made one phone call.");
                    e.Lines.Add("Marian Cole left the building at 10:40 with two officers and her orchid.");
                    e.Lines.Add("Theo kept his job. He keeps a copy of everything now.");
                    if (PriyaForwards(s)) e.Lines.Add("Priya was named Head of Systems. She changed every password before lunch.");
                    break;
                case "cleanbooks":
                    e.Title = "Clean Books";
                    e.Headline = "HALVORSEN FREIGHT PASSES ANNUAL AUDIT; JUNIOR ACCOUNTANT DISMISSED OVER 'IRREGULAR APPROVALS'";
                    e.Lines.Add("Monday, 9:04 AM. The auditors found what Marian wanted them to find: Theo's initials on every invoice.");
                    e.Lines.Add("Theo cleared his desk by noon. Nobody said goodbye properly.");
                    e.Lines.Add("Marian left an envelope in your locker. \"Senior Night Custodian\" pin, and a card: Thank you for your discretion.");
                    break;
                case "spotless":
                    e.Title = "Spotless";
                    e.Headline = "LOCAL FREIGHT FIRM: NOTHING TO REPORT";
                    e.Lines.Add("Monday, 9:04 AM. The auditors complimented the conference room. It had never looked better.");
                    e.Lines.Add("Whatever was in the red folder went out with the morning pickup.");
                    e.Lines.Add("You never read a single page that wasn't yours. Some people would call that professional.");
                    break;
                default:
                    e.Title = "Loose Threads";
                    e.Headline = "FINANCE DIRECTOR RESIGNS 'TO PURSUE OTHER OPPORTUNITIES'";
                    e.Lines.Add("Monday, 9:04 AM. The auditors asked a lot of questions. Nobody could quite answer them.");
                    e.Lines.Add("Marian resigned on Wednesday. No charges, no explanation, a very nice farewell lunch.");
                    if (s.FateOf("red_folder") == Fate.Kept) e.Lines.Add("The red folder is still in your locker. Maybe someday someone will ask for it.");
                    else e.Lines.Add("Half the story reached the right desk. The other half went down the chute.");
                    break;
            }
            // Personal epilogues
            if (s.IsDelivered("walt_letter", "auditor") || (PriyaForwards(s) && s.IsDelivered("walt_letter", "priya")))
                e.Lines.Add("Walt got a phone call, an apology and eleven years of back pay. He sent you a postcard: \"Told you. — W.\"");
            else if (s.Knows("walt"))
                e.Lines.Add("Walt never found out who believed him.");
            if (s.Has("took_money")) e.Lines.Add("You kept the fifty dollars. You told yourself it was a tip.");
            e.Lines.Add(s.Suspicion >= 3
                ? "BrightStar Janitorial moved you to a parking garage across town. \"Client request.\""
                : "BrightStar offered you the Meridian Tower contract permanently. You said you'd think about it.");
            return e;
        }
    }
}
