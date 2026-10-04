using UnityEngine;

namespace AfterHours
{
    /// <summary>Physics layers (named by ProjectSetup in the editor) and the collision matrix.</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int Grime = 8;
        public const int Player = 9;
        public const int Prop = 10;
        public const int Viewmodel = 11;
        public const int Trigger = 12;
        public const int Glass = 13;

        public static readonly int GrimeMask = 1 << Grime;
        /// <summary>Things that block sight for interaction and cleaning rays.</summary>
        public static readonly int SolidMask = (1 << Default) | (1 << Prop) | (1 << Glass);
        public static readonly int InteractMask = (1 << Default) | (1 << Prop) | (1 << Glass) | (1 << Grime);
        public static readonly int WalkMask = (1 << Default) | (1 << Glass);

        public static void ApplyCollisionMatrix()
        {
            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(Grime, i, true);
                Physics.IgnoreLayerCollision(Viewmodel, i, true);
                Physics.IgnoreLayerCollision(Trigger, i, i != Player);
            }
            Physics.IgnoreLayerCollision(Player, Prop, true);
        }
    }
}
