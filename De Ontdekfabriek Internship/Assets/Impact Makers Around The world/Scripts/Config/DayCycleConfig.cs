using System;
using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// Day phase definitions (M25). Defaults follow the MDA's four-phase arc
    /// (ASUBUHI → MCHANA → ALASIRI → JIONI); the Requirements' three-phase variant is
    /// an asset edit, not a code change (decision logged in D17). Per-phase URP Volumes
    /// are scene references on DayCycleManager, matched to these entries by index.
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Day Cycle Config", fileName = "DayCycleConfig")]
    public sealed class DayCycleConfig : ScriptableObject
    {
        [Serializable]
        public struct Phase
        {
            [Tooltip("Swahili label shown on the HUD.")]
            public string label;
            [Tooltip("Session time (seconds) at which this phase begins.")]
            public float startTime;
            public Color labelColour;
            [Tooltip("Directional light rotation for this phase.")]
            public Vector3 sunEuler;
            public Color sunColour;
            public float sunIntensity;
            public Color fogColour;
        }

        public Phase[] phases =
        {
            new Phase { label = "ASUBUHI", startTime = 0f,   labelColour = new Color(0.75f, 0.88f, 1f),    sunEuler = new Vector3(18f, -35f, 0f), sunColour = new Color(1f, 0.96f, 0.88f), sunIntensity = 0.95f, fogColour = new Color(0.78f, 0.86f, 0.92f) },
            new Phase { label = "MCHANA",  startTime = 30f,  labelColour = new Color(1f, 0.98f, 0.8f),     sunEuler = new Vector3(62f, -20f, 0f), sunColour = Color.white,                  sunIntensity = 1.15f, fogColour = new Color(0.85f, 0.9f, 0.95f)  },
            new Phase { label = "ALASIRI", startTime = 75f,  labelColour = new Color(1f, 0.85f, 0.6f),     sunEuler = new Vector3(34f, 25f, 0f),  sunColour = new Color(1f, 0.88f, 0.7f),  sunIntensity = 1.0f,  fogColour = new Color(0.9f, 0.82f, 0.7f)   },
            new Phase { label = "JIONI",   startTime = 105f, labelColour = new Color(1f, 0.62f, 0.38f),    sunEuler = new Vector3(9f, 48f, 0f),   sunColour = new Color(1f, 0.6f, 0.35f),  sunIntensity = 0.8f,  fogColour = new Color(0.85f, 0.6f, 0.45f)  }
        };

        [Tooltip("Seconds a phase transition takes (volume weights, sun and fog lerp).")]
        public float transitionSeconds = 4f;
    }
}
