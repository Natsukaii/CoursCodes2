using UnityEngine;

namespace Arena.Combat
{
    /// <summary>
    /// Display info of the 3 ammo types (one per DamageType): name and neon colour.
    /// </summary>
    public static class AmmoTypes
    {
        public static readonly DamageType[] All = { DamageType.Physical, DamageType.Energy, DamageType.Digital };

        public static string Label(DamageType type)
        {
            switch (type)
            {
                case DamageType.Energy: return "ENERGIE";
                case DamageType.Digital: return "DIGITAL";
                default: return "CINETIQUE";
            }
        }

        public static string Short(DamageType type)
        {
            switch (type)
            {
                case DamageType.Energy: return "NRJ";
                case DamageType.Digital: return "DIG";
                default: return "CIN";
            }
        }

        /// <summary>
        /// LDR colour, for the UI. Multiply it for an HDR glow.
        /// </summary>
        public static Color Color(DamageType type)
        {
            switch (type)
            {
                case DamageType.Energy: return new Color(0f, 0.85f, 1f);
                case DamageType.Digital: return new Color(1f, 0.1f, 0.75f);
                default: return new Color(1f, 0.6f, 0.05f);
            }
        }
    }
}
