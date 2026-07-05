using System;
using System.Collections.Generic;
using UnityEngine;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Editable table of every on-screen string, one row per <see cref="SwahiliUI"/> key.
    /// This is the "translate the game here" asset: fill the English / Dutch / Swahili
    /// columns in the Inspector. Leave a cell blank to fall back to English.
    ///
    /// How to use it:
    ///   1. Assets > Create > Kenya Scooter > UI Strings Config.
    ///   2. Put it in a folder named "Resources" so it loads automatically, OR assign it in
    ///      code with SwahiliUI.Load(config) from a bootstrapper.
    ///   3. Right-click the asset > "Fill with built-in defaults" to seed every current key,
    ///      then translate the columns.
    ///
    /// To add a fourth language: add a value to <see cref="SwahiliUI.Language"/>, a column
    /// field to <see cref="Entry"/> below, and a case to the switch in <see cref="TryGet"/>.
    /// Nothing else changes, because all screens read through SwahiliUI.Get(key).
    /// </summary>
    [CreateAssetMenu(fileName = "UIStringsConfig", menuName = "Kenya Scooter/UI Strings Config")]
    public sealed class UIStringsConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("The SwahiliUI key, e.g. HUD_SCORE, NEXT_PLAYER, WARN_POTHOLE.")]
            public string key;
            [TextArea] public string english;
            [TextArea] public string dutch;
            [TextArea] public string swahili;
        }

        [Tooltip("One row per on-screen string. A blank language cell falls back to English.")]
        public List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> _lookup;

        /// <summary>Returns the localised text for a key, or false to let SwahiliUI fall back.</summary>
        public bool TryGet(string key, SwahiliUI.Language language, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(key))
                return false;

            BuildLookup();
            if (!_lookup.TryGetValue(key, out Entry e))
                return false;

            string value;
            switch (language)
            {
                case SwahiliUI.Language.Swahili: value = e.swahili; break;
                case SwahiliUI.Language.Dutch:   value = e.dutch;   break;
                default:                         value = e.english; break;
            }

            if (string.IsNullOrEmpty(value))
                value = e.english; // blank cell falls back to English

            if (string.IsNullOrEmpty(value))
                return false; // nothing here, let SwahiliUI use its built-in default

            text = value;
            return true;
        }

        private void BuildLookup()
        {
            if (_lookup != null)
                return;
            _lookup = new Dictionary<string, Entry>(entries != null ? entries.Count : 0);
            if (entries == null)
                return;
            foreach (Entry e in entries)
                if (e != null && !string.IsNullOrEmpty(e.key))
                    _lookup[e.key] = e;
        }

        private void OnEnable() => _lookup = null; // rebuild after edits / domain reload

#if UNITY_EDITOR
        [ContextMenu("Fill with built-in defaults")]
        private void FillWithDefaults()
        {
            if (entries == null)
                entries = new List<Entry>();

            HashSet<string> have = new HashSet<string>();
            foreach (Entry e in entries)
                if (e != null && !string.IsNullOrEmpty(e.key))
                    have.Add(e.key);

            foreach (KeyValuePair<string, (string en, string nl, string sw)> kv in SwahiliUI.BuiltInDefaults)
            {
                if (have.Contains(kv.Key))
                    continue;
                entries.Add(new Entry { key = kv.Key, english = kv.Value.en, dutch = kv.Value.nl, swahili = kv.Value.sw });
            }

            _lookup = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
