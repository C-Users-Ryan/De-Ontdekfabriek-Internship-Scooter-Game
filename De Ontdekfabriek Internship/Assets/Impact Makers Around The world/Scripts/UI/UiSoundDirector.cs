using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Settings;

namespace KenyaScooter.UI
{
    /// <summary>
    /// The interface's OWN sound layer — deliberately separate from AudioManager (which owns the game world:
    /// engine, ambience, world SFX). It gives every screen a voice with two cues and, crucially, keeps that
    /// voice in the UI where it belongs:
    ///
    ///  - TAP — a soft click on any button/toggle/chip press, anywhere in the UI. Rather than wiring 40-odd
    ///    onClick sites (there is no shared button factory), it listens once at the EventSystem: on a pointer
    ///    press it raycasts the UI and, if the press landed on an interactable Selectable, plays the tap. One
    ///    hook covers every current and future button.
    ///  - SCREEN — a short cue when a framing screen appears (GameEvents.MenuScreenChanged), so opening the
    ///    title / hand-off / eindstand / game-over reads as a UI beat.
    ///
    /// It plays on its own AudioSource with <c>ignoreListenerPause = true</c>, so:
    ///  - it never routes through the world mix, so game sound can never bleed into it and vice-versa, and
    ///  - the buttons still click while the facilitator menu has the whole world frozen (AudioListener.pause).
    ///
    /// Self-bootstraps after scene load like the other audio systems, needs zero scene wiring, and is fully
    /// null-safe: with no AudioConfig or no UI clips imported yet (known backlog) it simply stays silent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiSoundDirector : MonoBehaviour
    {
        private AudioSource ui;
        private readonly List<RaycastResult> hits = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<UiSoundDirector>() != null)
                return;
            var go = new GameObject("UiSoundDirector (auto)");
            DontDestroyOnLoad(go);
            go.AddComponent<UiSoundDirector>();
        }

        private void Awake()
        {
            ui = gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = false;
            ui.spatialBlend = 0f;          // 2D — the UI is not in the world
            ui.ignoreListenerPause = true; // UI keeps clicking even when the facilitator menu pauses the world
        }

        private void OnEnable() => GameEvents.MenuScreenChanged += OnMenuScreenChanged;
        private void OnDisable() => GameEvents.MenuScreenChanged -= OnMenuScreenChanged;

        // A framing screen just appeared → cue it. (Ignore the false edge: dismissing back to the HUD is silent.)
        private void OnMenuScreenChanged(bool visible)
        {
            if (!visible) return;
            AudioConfig config = ConfigLocator.Audio;
            if (config != null) Play(config.uiScreenShow, config.uiScreenShowVolume);
        }

        private void Update()
        {
            if (!PressedThisFrame(out Vector2 pos))
                return;
            if (EventSystem.current == null || PressLandedOnInteractable(pos) == false)
                return;
            AudioConfig config = ConfigLocator.Audio;
            if (config != null) Play(config.uiTap, config.uiTapVolume);
        }

        // True only if the press landed on an active, interactable Selectable (button/toggle/slider/chip). This
        // is what keeps the tap a UI sound: a press on empty background or a world object makes no click.
        private bool PressLandedOnInteractable(Vector2 screenPos)
        {
            var data = new PointerEventData(EventSystem.current) { position = screenPos };
            hits.Clear();
            EventSystem.current.RaycastAll(data, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                var selectable = hits[i].gameObject.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable())
                    return true;
            }
            return false;
        }

        // One press across mouse / touch / pen via the shared Pointer device (same input stack the UI module uses).
        private static bool PressedThisFrame(out Vector2 position)
        {
            position = default;
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
                return false;
            position = pointer.position.ReadValue();
            return true;
        }

        // perClipVolume is the sound's own slider (0–1); config.uiVolume is the shared UI master.
        private void Play(AudioClip clip, float perClipVolume)
        {
            if (clip == null) return;
            AudioConfig config = ConfigLocator.Audio;
            float master = config != null ? config.uiVolume : 1f;
            ui.PlayOneShot(clip, master * Mathf.Clamp01(perClipVolume));
        }
    }
}
