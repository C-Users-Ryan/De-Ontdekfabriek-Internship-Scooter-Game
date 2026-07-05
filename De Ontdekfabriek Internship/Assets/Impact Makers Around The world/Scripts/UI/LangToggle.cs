using TMPro;
using UnityEngine;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Runtime language switch (Req §12.3). The button label shows the language you
    /// would switch TO. Every text component listening to SwahiliUI.LanguageChanged
    /// refreshes itself.
    /// </summary>
    public sealed class LangToggle : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private void Awake() => UpdateLabel();

        /// <summary>Wired to the button's onClick.</summary>
        public void Toggle()
        {
            SwahiliUI.Toggle();
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (label == null)
                return;
            // Show the language the button switches TO (Dutch → Swahili → English → Dutch).
            label.text = SwahiliUI.Current == SwahiliUI.Language.Dutch   ? "SW"
                       : SwahiliUI.Current == SwahiliUI.Language.Swahili ? "EN"
                       : "NL";
        }
    }
}
