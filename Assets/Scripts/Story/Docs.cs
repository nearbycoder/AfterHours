using System.Collections.Generic;

namespace AfterHours
{
    public enum DocStyle { Note, Sticky, Printout, Email, Invoice, Card, Log, Notepad, Letter, Screen, Ledger }

    /// <summary>A readable document. Evidence documents can be kept, delivered or shredded.</summary>
    public class DocDef
    {
        public string Id;
        public DocStyle Style;
        public string Title;            // short name (inventory, menus)
        public string Header;           // context line above the paper
        public string Body;             // TMP rich text
        public string Phrase;           // clue phrase learned on reading
        public bool Evidence;           // can be taken
        public bool Key;                // counts toward the audit
        public string Owner;            // person it belongs to (for "returned" consequences)
        public string Prop = "paper_sheet";
        public string Image;            // Screen style: Resources texture path
    }

    public static class Phrases
    {
        public static readonly Dictionary<string, string> Text = new()
        {
            { "signing", "Theo is being made to sign things." },
            { "walt", "Walt didn't take the laptop." },
            { "northgate", "Northgate Supply isn't real." },
            { "logins", "Someone logs in at 1 AM as M. Cole." },
            { "wire", "Check the Friday wire to account ••7731." },
            { "folder", "The auditors should see the red folder." },
        };
    }

    public static class People
    {
        public static readonly Dictionary<string, string> Name = new()
        {
            { "dana", "Dana Whitfield" }, { "theo", "Theo Marsh" }, { "priya", "Priya Anand" },
            { "russ", "Russ Kettering" }, { "marian", "Marian Cole" }, { "auditor", "Erin Sato (Brightwater)" },
            { "walt", "Walt Bremner" },
        };

        public static readonly Dictionary<string, string> Short = new()
        {
            { "dana", "Dana" }, { "theo", "Theo" }, { "priya", "Priya" }, { "russ", "Russ" },
            { "marian", "Marian" }, { "auditor", "the auditor" }, { "walt", "Walt" },
        };
    }

    public static class Docs
    {
        public static readonly Dictionary<string, DocDef> All = new();

        static DocDef Add(DocDef d) { All[d.Id] = d; return d; }

        public static DocDef Get(string id) => All.TryGetValue(id, out var d) ? d : null;

        static Docs()
        {
            // ------------------------------------------------------------------ closet / Walt
            Add(new DocDef
            {
                Id = "dana_welcome", Style = DocStyle.Note, Title = "Dana's welcome note", Header = "Note · pinned in the closet",
                Body = "Welcome aboard!\n\nYour shift sheet is on the clipboard <size=80%>(Tab)</size>. Start with my desk and the bins, then the bullpen. Lights off on your way out!\n\nMarian's office is locked, don't worry about it.\n\n<align=right>— Dana ☺</align>\n<size=80%>p.s. the candy jar is for you</size>",
            });
            Add(new DocDef
            {
                Id = "walt_note_1", Style = DocStyle.Note, Title = "Walt's note", Header = "Folded note · inside W. Bremner's locker",
                Body = "If you're reading this, they replaced me.\n\nClean what they ask.\nRead what they don't.\n\nAnd watch for the <b>red folder</b>.\n\n<align=right>— W.</align>",
            });
            Add(new DocDef
            {
                Id = "walt_note_2", Style = DocStyle.Note, Title = "Walt's second note", Header = "Note · taped to a UV torch",
                Body = "The torch is my old trick. Missed spots glow, so do a few other things.\n\nI marked what I could before they walked me out. Follow the marks.\n\n<align=right>— W.</align>",
            });
            Add(new DocDef
            {
                Id = "walt_letter", Style = DocStyle.Letter, Title = "Walt's envelope", Header = "Envelope · hidden above a ceiling tile",
                Body = "To whoever finds this,\n\nThe night before they fired me I found Marian's shredder bag torn open on the floor. I taped three of the strips back together. Invoices to \"Northgate Supply\", all approved, all paid to the same account.\n\nNorthgate has no phone number and no address but a PO box. I asked Marian about it. The next morning a laptop was \"missing\" and it was on my cart.\n\nCopies are in here. I didn't take anything in eleven years and I won't start by keeping quiet.\n\n<align=right>— Walt Bremner</align>",
                Evidence = true, Key = true, Owner = "walt", Prop = "envelope", Phrase = "walt",
            });

            // ------------------------------------------------------------------ night 1
            Add(new DocDef
            {
                Id = "theo_note", Style = DocStyle.Note, Title = "Theo's crumpled note", Header = "Crumpled paper · under Theo's desk",
                Body = "M.,\n\nI can't keep signing these without POs. Four this week. If anyone asks I'm saying you told me to.\n\n<align=right>— T</align>",
                Evidence = true, Key = true, Owner = "theo", Prop = "paper_ball", Phrase = "signing",
            });
            Add(new DocDef
            {
                Id = "screen_theo_email", Style = DocStyle.Screen, Title = "Theo's open email", Header = "Theo's monitor",
                Image = "Textures/Screens/email_theo", Body = "From: Marian Cole\nTo: Theo Marsh\nSubject: Northgate — today please\n\nTheo, the three Northgate invoices need approving by EOD. Use my sign-off if Dana asks why. No need to loop anyone else in. Thanks! — M",
            });
            Add(new DocDef
            {
                Id = "screen_remote", Style = DocStyle.Screen, Title = "Remote session", Header = "The empty desk's monitor · 1:12 AM",
                Image = "Textures/Screens/remote_session", Phrase = "logins",
                Body = "REMOTE SESSION ACTIVE\nuser: mcole\nhost: HALVORSEN-FIN01\nopened: Northgate_Q3.xlsx\nconnected 01:12",
            });

            // ------------------------------------------------------------------ night 2
            Add(new DocDef
            {
                Id = "russ_slip", Style = DocStyle.Note, Title = "Russ's betting slip", Header = "Folded slip · break room bin",
                Body = "<b>BENNY</b>\n\nowe 3,200 by FRI\nNO EXCUSES\n\n<size=80%>(Lakers −4.5 · Bruins ML · Jets O 41.5)</size>",
                Evidence = true, Owner = "russ", Prop = "paper_sheet",
            });
            Add(new DocDef
            {
                Id = "walt_card", Style = DocStyle.Card, Title = "Walt's farewell card", Header = "Card · break room corkboard",
                Body = "<size=130%>Walt — 11 years!</size>\nWe'll miss you!\n\n<i>Dana ♥ · Theo · P. · Russ (who's gonna fix the coffee machine now??)</i>\n\n<color=#B03A2E>[sticky note] Do not circulate. — M.C.</color>",
            });
            Add(new DocDef
            {
                Id = "fridge_note", Style = DocStyle.Sticky, Title = "Fridge note", Header = "Note on the fridge",
                Body = "WHOEVER KEEPS TAKING MY YOGURT:\n\nI HAVE YOUR DNA NOW\n\n— R",
            });
            Add(new DocDef
            {
                Id = "window_message", Style = DocStyle.Screen, Title = "Writing in the foam", Header = "Break room window", Phrase = "walt",
                Body = "WALT DIDN'T TAKE IT",
            });
            Add(new DocDef
            {
                Id = "priya_thanks", Style = DocStyle.Sticky, Title = "Priya's sticky note", Header = "Sticky note · Priya's monitor",
                Body = "Got your note.\nKeep your eyes open.\n\n— P",
            });
            Add(new DocDef
            {
                Id = "theo_thanks", Style = DocStyle.Sticky, Title = "Theo's sticky note", Header = "Sticky note · Theo's monitor",
                Body = "thank you for giving it back.\nplease don't tell anyone.\n\n— T",
            });
            Add(new DocDef
            {
                Id = "marian_seeme", Style = DocStyle.Sticky, Title = "Note on Theo's screen", Header = "Sticky note · Theo's monitor",
                Body = "See me.\n\n— M.C.",
            });

            // ------------------------------------------------------------------ night 3
            Add(new DocDef
            {
                Id = "whiteboard_ghost", Style = DocStyle.Screen, Title = "Ghosted writing", Header = "Under the marker · conference whiteboard", Phrase = "northgate",
                Body = "NORTHGATE SUPPLY → ??? → M.C.\nwho approves these??",
            });
            Add(new DocDef
            {
                Id = "audit_agenda", Style = DocStyle.Printout, Title = "Audit prep agenda", Header = "Printout · conference table",
                Body = "<b>BRIGHTWATER & CO. — ANNUAL AUDIT</b>\nOn site: Tuesday the 15th\n\n• Lead: Marian Cole\n• AP documentation: Theo Marsh\n• System access logs: Priya Anand\n• Conference room booked all week: Dana\n\n<size=85%>Please have all vendor files (incl. NORTHGATE SUPPLY) ready for review.</size>",
            });
            Add(new DocDef
            {
                Id = "theo_planner", Style = DocStyle.Notepad, Title = "Theo's planner page", Header = "Torn planner page · under the conference table",
                Body = "<b>MON</b>  sign-off w/ M.C. — Northgate ×4\n<b>WED</b>  sign-off w/ M.C. — Northgate ×3\n<b>FRI</b>  \"just initial, don't read\" ??\n\n<size=80%>ask P. about the 1am logins??</size>",
                Evidence = true, Key = true, Owner = "theo", Prop = "paper_sheet", Phrase = "signing",
            });

            // ------------------------------------------------------------------ night 4
            Add(new DocDef
            {
                Id = "notepad_rubbing", Style = DocStyle.Notepad, Title = "Pencil rubbing", Header = "Marian's legal pad · indented writing",
                Body = "<size=125%>NORTHGATE</size>\nwire <b>48,500</b>\nacct ••7731\n<b>FRI</b> — before audit",
                Evidence = true, Key = true, Owner = "marian", Prop = "notepad", Phrase = "wire",
            });
            Add(new DocDef
            {
                Id = "northgate_invoices", Style = DocStyle.Invoice, Title = "Northgate invoices", Header = "Folder · cabinet FC-2",
                Body = "<b>NORTHGATE SUPPLY CO.</b>   PO Box 77\n\nNG-0409   Logistics consulting   $12,400   <i>Approved: T.M.</i>\nNG-0410   Route optimisation    $18,900   <i>Approved: T.M.</i>\nNG-0411   Warehouse audit        $9,750   <i>Approved: T.M.</i>\n\nRemit to account ••7731",
                Evidence = true, Key = true, Owner = "marian", Prop = "folder_manila", Phrase = "northgate",
            });
            Add(new DocDef
            {
                Id = "marian_envelope", Style = DocStyle.Note, Title = "Envelope with $50", Header = "Envelope · on Marian's desk",
                Body = "For the night cleaner —\n\nFor your discretion. Please take the shredder bag <b>straight down the chute</b>, not the recycling.\n\nThank you.\n<align=right>— M.C.</align>\n\n<size=80%>[Two twenties and a ten.]</size>",
            });
            Add(new DocDef
            {
                Id = "screen_marian_lock", Style = DocStyle.Screen, Title = "Marian's lock screen", Header = "Marian's monitor", Phrase = "logins",
                Image = "Textures/Screens/marian_lock", Body = "Locked: mcole\nLast sign-in: 01:12 (remote)",
            });
            Add(new DocDef
            {
                Id = "shred_bag", Style = DocStyle.Note, Title = "Shredder bag", Header = "Marian's shredder bin",
                Body = "A clear bag stuffed with paper strips.\nMost of it is confetti.\nSome strips still have numbers on them.",
                Evidence = true, Owner = "marian", Prop = "card_box",
            });

            // ------------------------------------------------------------------ night 5
            Add(new DocDef
            {
                Id = "reconstructed_invoice", Style = DocStyle.Invoice, Title = "Reconstructed invoice", Header = "Taped back together · closet table",
                Body = "<b>NORTHGATE SUPPLY CO.</b>\nInvoice NG-0412 — \"Strategic review\"\n<b>$48,500</b>\n\nRemit to account <b>••7731</b>\nApproved: <b><i>M. Cole</i></b>",
                Evidence = true, Key = true, Owner = "marian", Prop = "paper_sheet", Phrase = "wire",
            });
            Add(new DocDef
            {
                Id = "vpn_log", Style = DocStyle.Log, Title = "Priya's VPN log", Header = "Printout · left in the copier tray",
                Body = "VPN ACCESS — user <b>mcole</b>\n  03-02  01:12  HALVORSEN-FIN01\n  03-04  01:47  HALVORSEN-FIN01\n  03-08  01:12  HALVORSEN-FIN01\n  03-09  02:03  HALVORSEN-FIN01\n  03-11  01:12  HALVORSEN-FIN01\n\n<size=85%>(every date = a Northgate approval. — P)</size>",
                Evidence = true, Key = true, Owner = "priya", Prop = "paper_sheet", Phrase = "logins",
            });
            Add(new DocDef
            {
                Id = "theo_resignation", Style = DocStyle.Letter, Title = "Theo's draft resignation", Header = "Crumpled draft · Theo's bin",
                Body = "Dear Mr. Halvorsen,\n\nI can't be the one who signs these anymore. <s>Effective immediately</s>\n<s>I was told</s>\n\n<size=85%>(the rest is scribbled out)</size>",
                Evidence = true, Owner = "theo", Prop = "paper_ball",
            });
            Add(new DocDef
            {
                Id = "marian_warning", Style = DocStyle.Note, Title = "Marian's warning", Header = "Note · taped to the closet door",
                Body = "To the night cleaner:\n\nThings have been moved in my office. I will be speaking to your agency in the morning.\n\n<align=right>— M. Cole, Director of Finance</align>",
            });
            Add(new DocDef
            {
                Id = "priya_ally", Style = DocStyle.Printout, Title = "Copier printout", Header = "Printed by itself · 3:33 AM",
                Body = "<size=140%><b>I KNOW SOMEONE IS HELPING.</b></size>\n\nauditor arrives tuesday. conference room.\nthey'll have a tray.\n\n— P",
            });

            // ------------------------------------------------------------------ night 6
            Add(new DocDef
            {
                Id = "payment_ledger", Style = DocStyle.Ledger, Title = "Northgate payment ledger", Header = "Archive box marked DESTROY",
                Body = "<b>VENDOR LEDGER — NORTHGATE SUPPLY</b>\n\n02-14   NG-0398   $14,200   paid\n02-28   NG-0403   $22,750   paid\n03-07   NG-0409   $12,400   paid\n03-13   NG-0410   $18,900   paid\n03-21   NG-0411    $9,750   paid\n<b>Total YTD   $78,000</b>\n\n<size=85%>Signatory on file: M. Cole</size>",
                Evidence = true, Key = true, Owner = "marian", Prop = "binder", Phrase = "northgate",
            });
            Add(new DocDef
            {
                Id = "dana_doubt", Style = DocStyle.Sticky, Title = "Dana's note", Header = "Sticky note · Dana's keyboard",
                Body = "Something's off.\nI don't want to know.\n\nDo I?\n\n— D",
            });
            Add(new DocDef
            {
                Id = "voicemail", Style = DocStyle.Log, Title = "Voicemail", Header = "Reception phone · 1 new message",
                Body = "<i>[Sunday 11:48 PM — from: M. Cole, mobile]</i>\n\n\"…no, it's fine. Everything in the archive boxes goes out with the morning pickup. <b>All of it.</b> I'll handle the folder myself.\"",
            });

            // ------------------------------------------------------------------ extras
            Add(new DocDef
            {
                Id = "dana_office_key", Style = DocStyle.Note, Title = "Dana's note about the key", Header = "Note · pinned in the closet",
                Body = "Marian asked for a <b>deep clean</b> of her office before the auditors. Her door's unlocked tonight.\n\nPlease put everything back <u>exactly</u> where it was. She notices. Trust me.\n\n<align=right>— Dana</align>",
            });
            Add(new DocDef
            {
                Id = "notepad_blank", Style = DocStyle.Notepad, Title = "Marian's legal pad", Header = "Legal pad · Marian's desk",
                Body = "The top page is blank.\n\nBut if you tilt it toward the lamp you can see <i>indentations</i>, pressed through from a page that's been torn off.\n\n<size=80%>A pencil rubbing might bring them out.</size>",
            });
            Add(new DocDef
            {
                Id = "marian_memo", Style = DocStyle.Printout, Title = "All-staff memo", Header = "Printout · Dana's desk",
                Body = "<b>TO: ALL STAFF</b>\n<b>FROM: Marian Cole, Director of Finance</b>\n\nGraffiti on company property, including windows, will be treated as a disciplinary matter.\n\nWalt Bremner's departure is a closed HR issue. Please do not speculate about it.\n\nThank you.",
            });
            Add(new DocDef
            {
                Id = "priya_forward", Style = DocStyle.Sticky, Title = "Priya's note", Header = "Sticky note · the auditor's tray",
                Body = "Ms. Sato —\nEverything I had is in the blue folder.\nThe rest you'll have to find.\n\n— P. Anand",
            });
            Add(new DocDef
            {
                Id = "flight_note", Style = DocStyle.Note, Title = "Flight confirmation", Header = "Torn printout · Marian's bin",
                Body = "<b>BOOKING CONFIRMED</b>\nPassenger: COLE / MARIAN A.\nDeparting Tuesday 4:15 PM\n<b>One way.</b>",
            });
            Add(new DocDef
            {
                Id = "test_page", Style = DocStyle.Printout, Title = "Test page", Header = "The copier printed by itself",
                Body = "<b>TEST PAGE</b>\n\nPrinter: HALVORSEN-COPIER-01\nStatus: OK\nToner: 12%\n\n<size=80%>(Somebody sent this from FIN01 at 3:33 AM.)</size>",
            });
            Add(new DocDef
            {
                Id = "archive_label", Style = DocStyle.Printout, Title = "Archive box", Header = "Archive box · by the entrance",
                Body = "<size=140%><b>ARCHIVE — DESTROY</b></size>\nFor collection Monday 6:00 AM\n\n<size=85%>Do not open. — M.C.</size>",
            });

            // ------------------------------------------------------------------ night 7
            Add(new DocDef
            {
                Id = "red_folder", Style = DocStyle.Invoice, Title = "The red folder", Header = "Pulled from the jammed shredder",
                Body = "<b>NORTHGATE — ORIGINALS</b>\n\nCertificate of incorporation: Northgate Supply Co.\nDirector: <b>M. A. Cole</b>\nBank: account ending <b>7731</b>, holder <b>M. A. Cole</b>\n\nFourteen invoices. One signature underneath every approval stamp.",
                Evidence = true, Key = true, Owner = "marian", Prop = "folder_red", Phrase = "folder",
            });
        }
    }
}
