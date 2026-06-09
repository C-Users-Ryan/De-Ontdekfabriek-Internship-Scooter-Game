using System.Collections.Generic;
using UnityEngine;

namespace OvertakeGame
{
    [CreateAssetMenu(fileName = "RoadSequence", menuName = "OvertakeGame/Road Sequence")]
    public class RoadSequence : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Label shown in the editor and debug HUD (e.g. SAVANNA_OPEN, TSAVO_WILDLIFE).")]
        public string contextTag = "";

        [Header("Content")]
        [Tooltip("Ordered tile prefabs that make up this sequence. Played front-to-back.")]
        public List<GameObject> tiles = new();

        [Header("Selection")]
        [Tooltip("Relative weight in the weighted draw. 0 = excluded from random selection.")]
        public float weight = 1f;

        [Tooltip("Number of other sequences that must play before this one is eligible again.")]
        public int cooldown = 0;

        [Header("Journey Arc")]
        [Tooltip("Seconds of session time that must elapse before this sequence becomes eligible. 0 = available from session start.")]
        public float unlockAtTime = 0f;
    }
}
