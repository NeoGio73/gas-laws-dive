using UnityEngine;

// The air in one container (the diver's vest, lungs or tank), worked out with the ideal gas law.
// A dive sets the depth, the temperature and how much air there is; the side panel reads State().
//   - Pressure in the sea: 1 atm at the surface, plus 1 atm for every 10.0 m of depth.
//   - A flexible container (vest, lungs) is squeezed to the pressure around it: V = nRT/P.
//   - A rigid container (tank) keeps its volume, so its pressure changes: P = nRT/V.
// Kelvin = °C + 273.15 in every calculation.
namespace DiveGame.Gas
{
    public class GasState
    {
        public string title;            // "Air in your vest"
        public float depthM;
        public float pressureAtm;
        public float volumeL;
        public float tempC;
        public float moles;
        public bool showPartials;       // Dive 4: oxygen and nitrogen partial pressures
        public float ppO2, ppN2;

        // How the particle view is scaled for this container (never shown as numbers):
        public float volumeScaleL = 8f;   // volume that fills the whole box
        public float molesRef = 0.25f;    // amount of air drawn as the standard number of dots
        public float pressureRef = 1f;    // pressure counted as "normal" for the wall flashes

        public float TempK => tempC + 273.15f;
    }

    public class GasModel
    {
        public const float R = 0.082057f;   // L·atm/(mol·K)

        public string title = "Air in your vest";
        public bool rigid;
        public float moles = 0.243f;        // 6.0 L at 1 atm and 28 °C
        public float tempC = 28f;
        public float depthM;
        public float rigidVolumeL = 11.1f;  // a standard aluminium 80 tank holds 11.1 L
        public float volumeScaleL = 8f;
        public float molesRef = 0.243f;
        public float pressureRef = 1f;
        public bool showPartials;
        public float o2Fraction = 0.21f;

        // A frozen copy, for holding an earlier state beside "Now" (SidePanel.Compare).
        public GasModel Copy() => (GasModel)MemberwiseClone();

        public static float PressureAtDepth(float depthM) => 1f + Mathf.Max(0f, depthM) / 10f;
        public static float Kelvin(float c) => c + 273.15f;

        // Moles that give this volume at this pressure and temperature (to set up a dive).
        public static float MolesFor(float volumeL, float pressureAtm, float tempC) =>
            pressureAtm * volumeL / (R * Kelvin(tempC));

        public GasState State()
        {
            float ambient = PressureAtDepth(depthM);
            float t = Kelvin(tempC);
            float p, v;
            if (rigid) { v = rigidVolumeL; p = moles * R * t / v; }
            else { p = ambient; v = moles * R * t / p; }
            return new GasState
            {
                title = title, depthM = depthM, pressureAtm = p, volumeL = v, tempC = tempC, moles = moles,
                showPartials = showPartials, ppO2 = p * o2Fraction, ppN2 = p * (1f - o2Fraction),
                volumeScaleL = volumeScaleL, molesRef = molesRef, pressureRef = pressureRef,
            };
        }
    }
}
