using TMPro;
using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Floating score labels ("+150 INGEHAALD!", "-50 KUUKUA!", Req §7.5) — a pooled
    /// set of TMP labels under the overlay canvas, anchored to the world position of
    /// the event (the overtaken matatu, the pothole) and rising/fading out over a
    /// second. Pool slots are reused oldest-first; nothing is instantiated during play.
    /// </summary>
    public sealed class ScorePopupUI : MonoBehaviour
    {
        [Tooltip("Disabled TMP child used as the blueprint for the pool.")]
        [SerializeField] private TMP_Text template;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private int poolSize = 8;
        [SerializeField] private float lifetime = 1.1f;
        [SerializeField] private float risePixels = 90f;
        [SerializeField] private Color positiveColour = new Color(0.5f, 1f, 0.55f);
        [SerializeField] private Color negativeColour = new Color(1f, 0.45f, 0.4f);
        [SerializeField] private Color neutralColour = Color.white;

        private TMP_Text[] pool;
        private float[] age;
        private Vector2[] basePosition;
        private int nextSlot;

        private void Awake()
        {
            template.gameObject.SetActive(false);
            pool = new TMP_Text[poolSize];
            age = new float[poolSize];
            basePosition = new Vector2[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                pool[i] = Instantiate(template, template.transform.parent);
                age[i] = float.MaxValue;
            }
        }

        private void OnEnable()
        {
            GameEvents.PopupRequested += HandlePopupRequested;
            GameEvents.SessionReset += HideAll;
        }

        private void OnDisable()
        {
            GameEvents.PopupRequested -= HandlePopupRequested;
            GameEvents.SessionReset -= HideAll;
        }

        private void Update()
        {
            for (int i = 0; i < poolSize; i++)
            {
                if (age[i] >= lifetime)
                    continue;

                age[i] += Time.deltaTime;
                float t = Mathf.Clamp01(age[i] / lifetime);
                TMP_Text popup = pool[i];
                popup.rectTransform.position = basePosition[i] + Vector2.up * (risePixels * t);
                popup.alpha = 1f - t * t;

                if (t >= 1f)
                    popup.gameObject.SetActive(false);
            }
        }

        private void HandlePopupRequested(int delta, string labelKey, Vector3 position, bool worldSpace)
        {
            Vector2 screenPosition;
            if (worldSpace)
            {
                if (worldCamera == null)
                    return;
                Vector3 projected = worldCamera.WorldToScreenPoint(position);
                if (projected.z <= 0f)
                    return; // behind the camera
                screenPosition = projected;
            }
            else
            {
                screenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
            }

            int slot = TakeSlot();
            TMP_Text popup = pool[slot];
            popup.text = delta == 0
                ? SwahiliUI.Get(labelKey)
                : $"{(delta > 0 ? "+" : "-")}{Mathf.Abs(delta)} {SwahiliUI.Get(labelKey)}";
            popup.color = delta > 0 ? positiveColour : delta < 0 ? negativeColour : neutralColour;
            popup.rectTransform.position = screenPosition;
            popup.alpha = 1f;
            popup.gameObject.SetActive(true);

            age[slot] = 0f;
            basePosition[slot] = screenPosition;
        }

        private int TakeSlot()
        {
            // Prefer a free slot, otherwise steal the oldest.
            int oldest = 0;
            float oldestAge = -1f;
            for (int i = 0; i < poolSize; i++)
            {
                int candidate = (nextSlot + i) % poolSize;
                if (age[candidate] >= lifetime)
                {
                    nextSlot = (candidate + 1) % poolSize;
                    return candidate;
                }
                if (age[candidate] > oldestAge)
                {
                    oldestAge = age[candidate];
                    oldest = candidate;
                }
            }
            return oldest;
        }

        private void HideAll()
        {
            for (int i = 0; i < poolSize; i++)
            {
                age[i] = float.MaxValue;
                pool[i].gameObject.SetActive(false);
            }
        }
    }
}
