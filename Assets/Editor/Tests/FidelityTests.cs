using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AfterHours.Tests
{
    /// <summary>Graphics Fidelity: the four steps, what each renders, old settings files and the slow-frames steps.</summary>
    public class FidelityTests
    {
        [Test]
        public void FourStepsFromLowToUltraWithHighTheDefault()
        {
            CollectionAssert.AreEqual(new[] { "Low", "Medium", "High", "Ultra" }, GraphicsQuality.Names);
            Assert.AreEqual(GraphicsQuality.Names.Length, GraphicsQuality.Blurbs.Length, "a line for every step");
            Assert.AreEqual(GraphicsQuality.Default, new Settings().Quality, "a new settings file starts on High");
            Assert.AreEqual("High", GraphicsQuality.Names[GraphicsQuality.Default]);
        }

        [Test]
        public void EveryStepRendersAtLeastAsMuchAsTheOneBelow()
        {
            for (int q = 1; q < GraphicsQuality.Names.Length; q++)
            {
                FidelityStep a = GraphicsQuality.StepFor(q - 1), b = GraphicsQuality.StepFor(q);
                string at = $"{GraphicsQuality.Names[q - 1]} → {GraphicsQuality.Names[q]}";
                Assert.GreaterOrEqual(b.Msaa, a.Msaa, at + ": MSAA");
                Assert.GreaterOrEqual(b.MainShadowRes, a.MainShadowRes, at + ": moon shadows");
                Assert.GreaterOrEqual(b.ShadowAtlas, a.ShadowAtlas, at + ": light shadow atlas");
                Assert.GreaterOrEqual(b.LightShadowTile, a.LightShadowTile, at + ": light shadow tiles");
                Assert.GreaterOrEqual(b.Cascades, a.Cascades, at + ": cascades");
                Assert.GreaterOrEqual(b.ShadowDistance, a.ShadowDistance, at + ": shadow distance");
                Assert.GreaterOrEqual(b.RoomShadows, a.RoomShadows, at + ": room-light shadows");
                Assert.GreaterOrEqual(b.Ssao, a.Ssao, at + ": SSAO");
                Assert.LessOrEqual(b.TextureMipLimit, a.TextureMipLimit, at + ": texture size");
                Assert.GreaterOrEqual(b.Reflections, a.Reflections, at + ": reflections");
                Assert.GreaterOrEqual(b.Particles, a.Particles, at + ": particles");
                Assert.IsTrue(!a.BloomHighQuality || b.BloomHighQuality, at + ": bloom");
                Assert.IsTrue(!a.FilmGrain || b.FilmGrain, at + ": film grain");
                Assert.IsTrue(!a.LampShadows || b.LampShadows, at + ": lamp shadows");
            }
        }

        [Test]
        public void HighIsTheAuthoredLookAndUltraGoesPastIt()
        {
            var high = GraphicsQuality.StepFor(2);
            Assert.AreEqual(4, high.Msaa);
            Assert.AreEqual(2048, high.MainShadowRes);
            Assert.AreEqual(4096, high.ShadowAtlas);
            Assert.AreEqual(1024, high.LightShadowTile);
            Assert.AreEqual(2, high.Cascades);
            Assert.AreEqual(28f, high.ShadowDistance);
            Assert.AreEqual(2, high.RoomShadows, "soft, as authored");
            Assert.AreEqual(1, high.Ssao, "as authored");
            Assert.AreEqual(-1, high.Aniso, "as the project sets it");
            Assert.AreEqual(0, high.TextureMipLimit);
            Assert.AreEqual(0, high.Reflections);
            Assert.AreEqual(1f, high.Particles);
            Assert.IsFalse(high.LampShadows);
            Assert.AreEqual(AntialiasingMode.SubpixelMorphologicalAntiAliasing, high.PostAa);
            Assert.AreEqual(AntialiasingQuality.High, high.PostAaQuality);

            var ultra = GraphicsQuality.StepFor(3);
            Assert.Greater(ultra.MainShadowRes, high.MainShadowRes);
            Assert.Greater(ultra.LightShadowTile, high.LightShadowTile);
            Assert.Greater(ultra.Cascades, high.Cascades);
            Assert.Greater(ultra.Ssao, high.Ssao);
            Assert.Greater(ultra.Reflections, 0);
            Assert.AreEqual(16, ultra.Aniso);
            Assert.IsTrue(ultra.LampShadows);
            Assert.Greater(ultra.Particles, 1f);
            // The bigger tiles come with a bigger atlas: it holds as many of them as High's holds of its own.
            Assert.GreaterOrEqual(ultra.ShadowAtlas / ultra.LightShadowTile, high.ShadowAtlas / high.LightShadowTile);
        }

        [Test]
        public void LowIsTheLightest()
        {
            var low = GraphicsQuality.StepFor(0);
            Assert.AreEqual(1, low.Msaa);
            Assert.AreEqual(0, low.RoomShadows, "the moon only");
            Assert.AreEqual(0, low.Ssao);
            Assert.AreEqual(0, low.Aniso);
            Assert.AreEqual(1, low.TextureMipLimit, "half-size textures");
            Assert.IsTrue(low.BloomQuarter);
            Assert.IsFalse(low.FilmGrain);
            Assert.AreEqual(0.5f, low.Particles);
            Assert.AreEqual(AntialiasingMode.FastApproximateAntialiasing, low.PostAa);
            Assert.AreEqual(GraphicsQuality.StepFor(0).Msaa, GraphicsQuality.StepFor(-3).Msaa, "out of range clamps");
            Assert.AreEqual(GraphicsQuality.StepFor(3).Reflections, GraphicsQuality.StepFor(9).Reflections);
        }

        [Test]
        public void OlderSettingsFilesKeepTheirStep()
        {
            for (int q = 0; q <= 2; q++)
            {
                var old = JsonUtility.FromJson<Settings>($"{{\"MouseSensitivity\":1.5,\"Quality\":{q},\"Bindings\":[]}}");
                Assert.AreEqual(q, old.Quality, $"a round 11 file on {GraphicsQuality.Names[q]} stays there");
            }
            Assert.AreEqual(GraphicsQuality.Default, JsonUtility.FromJson<Settings>("{\"MouseSensitivity\":1.5}").Quality, "a file from before the preset gets High");
            var ultra = JsonUtility.FromJson<Settings>(JsonUtility.ToJson(new Settings { Quality = 3 }));
            Assert.AreEqual(3, ultra.Quality, "Ultra is saved");
        }

        [Test]
        public void RunningSlowlyStepsDownFromUltra()
        {
            Assert.AreEqual((2, 1f), SlowFrames.NextStep(3, 1f), "Ultra → High");
            Assert.AreEqual((1, 1f), SlowFrames.NextStep(2, 1f), "High → Medium");
            Assert.AreEqual((0, 1f), SlowFrames.NextStep(1, 1f), "Medium → Low");
            Assert.AreEqual((0, 0.75f), SlowFrames.NextStep(0, 1f), "then render scale");
            Assert.IsTrue(SlowFrames.Worth(new Settings { Quality = 3 }));
        }

        [Test]
        public void ParticlesFollowTheStepButTheLeftoverBeaconsDont()
        {
            float d = Fx.Density;
            try
            {
                Fx.Density = GraphicsQuality.StepFor(0).Particles;
                Assert.AreEqual(10, Fx.Scaled(20, FxKind.Sparkle));
                Assert.AreEqual(1, Fx.Scaled(1, FxKind.Sparkle), "never below one");
                Assert.AreEqual(6, Fx.Scaled(6, FxKind.Beacon), "a hint stays a hint");
                Fx.Density = GraphicsQuality.StepFor(3).Particles;
                Assert.AreEqual(30, Fx.Scaled(20, FxKind.Confetti));
                Assert.AreEqual(6, Fx.Scaled(6, FxKind.Beacon));
                Assert.AreEqual(0, Fx.Scaled(0, FxKind.Sparkle));
            }
            finally { Fx.Density = d; }
        }
    }
}
