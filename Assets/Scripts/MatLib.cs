using System.Collections.Generic;
using UnityEngine;

namespace RollingSteel
{
    /// Visual materials live as real assets under Assets/Resources/Mat so that
    /// the build pipeline keeps them (and the Standard shader) instead of
    /// stripping shaders nothing appears to reference. Physics materials have no
    /// shader dependency, so those are just made on the fly.
    public static class MatLib
    {
        static readonly Dictionary<string, Material> visual = new Dictionary<string, Material>();

        public static Material Get(string name)
        {
            if (visual.TryGetValue(name, out var m) && m != null) return m;

            m = Resources.Load<Material>("Mat/" + name);
            if (m == null)
            {
                Debug.LogWarning($"[MatLib] missing material Mat/{name}, falling back to magenta");
                m = new Material(Shader.Find("Standard")) { color = Color.magenta };
            }
            visual[name] = m;
            return m;
        }

        public static string VisualName(Surface s)
        {
            switch (s)
            {
                case Surface.Rough: return "Rough";
                case Surface.Ice: return "Ice";
                case Surface.Acid: return "Acid";
                case Surface.Goal: return "Goal";
                case Surface.Start: return "Start";
                case Surface.Rail: return "Rail";
                case Surface.Crumble: return "Crumble";
                default: return "Deck";
            }
        }

        // ---- physics -----------------------------------------------------

        static PhysicsMaterial Make(string name, float dyn, float stat, float bounce)
        {
            return new PhysicsMaterial(name)
            {
                dynamicFriction = dyn,
                staticFriction = stat,
                bounciness = bounce,
                // Multiply means the marble's own friction scales the deck's, so
                // ice (0.02) genuinely lets go while sand (1.1) bites.
                frictionCombine = PhysicsMaterialCombine.Multiply,
                bounceCombine = PhysicsMaterialCombine.Maximum,
            };
        }

        static PhysicsMaterial deck, rough, ice, rail, marble, bouncy;

        /// Posts and sweeper arms, so glancing off one actually deflects you.
        public static PhysicsMaterial Bouncy => bouncy ??= Make("Bouncy", 0.25f, 0.25f, 0.65f);

        public static PhysicsMaterial Marble => marble ??= Make("Marble", 0.5f, 0.5f, 0.15f);

        public static PhysicsMaterial Physics(Surface s)
        {
            switch (s)
            {
                case Surface.Rough: return rough ??= Make("Rough", 1.1f, 1.2f, 0.02f);
                case Surface.Ice: return ice ??= Make("Ice", 0.05f, 0.05f, 0.05f);
                case Surface.Rail: return rail ??= Make("Rail", 0.3f, 0.3f, 0.35f);
                case Surface.Crumble: return deck ??= Make("Deck", 0.55f, 0.6f, 0.12f);
                default: return deck ??= Make("Deck", 0.55f, 0.6f, 0.12f);
            }
        }
    }
}
