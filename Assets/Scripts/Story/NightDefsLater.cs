using UnityEngine;
using static AfterHours.GrimeStamp;

namespace AfterHours
{
    public static partial class NightDefs
    {
        static NightDef Stub(int number)
        {
            var n = new NightDef { Number = number, Day = Days[number], Title = Titles[number], Tagline = "", Rooms = new[] { "reception", "bullpen", "breakroom", "conference", "office" } };
            n.Grime("desk_reception", Spec(100 + number).Add(Dust(0.5f, 0.45f, 1f)));
            n.Task("desk_reception", "Wipe the reception desk", TaskKind.Clean, "reception", "desk_reception");
            n.Say("9:00 AM", "dana", "Morning!");
            return n;
        }

        static NightDef Night2() => Stub(2);
        static NightDef Night3() => Stub(3);
        static NightDef Night4() => Stub(4);
        static NightDef Night5() => Stub(5);
        static NightDef Night6() => Stub(6);
        static NightDef Night7() => Stub(7);
    }
}
