using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// Feeds the global <c>_KenyaRoadScroll</c> shader value (= metres travelled) so the
    /// KenyaScooter/MurramRoad grain stays GLUED to the road. The road tiles scroll toward the fixed player,
    /// so without this the world-locked procedural pattern would slide under the tarmac ("swim"); adding the
    /// distance travelled back into Z cancels the tile motion, exactly the trick <see cref="ScrollingGround"/>
    /// uses to fake motion on the static ground plane.
    ///
    /// Self-boots after scene load like the other runtime scenery, needs zero wiring, and is harmless if no
    /// object actually uses the murram shader (it just sets a global nobody reads). In the editor the global is
    /// unset (0), so the shader previews a static pattern — which is what you want while dressing a tile.
    /// </summary>
    public sealed class MurramRoadScroll : MonoBehaviour
    {
        private static readonly int ScrollId = Shader.PropertyToID("_KenyaRoadScroll");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<MurramRoadScroll>() != null)
                return;
            var go = new GameObject("MurramRoadScroll (auto)");
            go.AddComponent<MurramRoadScroll>();
        }

        private void Update()
        {
            float dist = WorldSpeed.Instance != null ? WorldSpeed.Instance.DistanceTravelled : 0f;
            Shader.SetGlobalFloat(ScrollId, dist);
        }
    }
}
