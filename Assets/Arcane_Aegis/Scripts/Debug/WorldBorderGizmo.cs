using UnityEngine;

namespace Arcane_Aegis.Debugging
{
    /// <summary>
    /// TEST GIZMO: draws the world grid in GLOBAL coords — each continent is a box of side <see cref="worldSize"/>,
    /// laid out side by side (<see cref="columns"/> wide). The RED vertical lines are the shared edges where the
    /// seamless border handoff fires; the GREEN lines are the outer bounds. With the client rendering in global
    /// coords (local + zone offset), the player should slide across the red lines without any jump.
    /// Put it on an empty GameObject in the World scene; set worldSize to the server's WORLD_SIZE (512 for the test).
    /// Gizmos show in the Scene view always; enable the "Gizmos" toggle in the Game view to see them while playing.
    /// </summary>
    public class WorldBorderGizmo : MonoBehaviour
    {
        [Tooltip("Must match the server's WORLD_SIZE (docker-compose). 512 for the test fleet.")]
        [SerializeField] private float worldSize = 512f;
        [Tooltip("How many continents to draw side by side (the grid row).")]
        [SerializeField] private int columns = 5;
        [Tooltip("How tall to draw the walls, just for visibility.")]
        [SerializeField] private float wallHeight = 5f;
        [SerializeField] private bool drawAlways = true;

        private void OnDrawGizmos()         { if (drawAlways) Draw(); }
        private void OnDrawGizmosSelected() { if (!drawAlways) Draw(); }

        private void Draw()
        {
            float s = worldSize, h = wallHeight;
            int cols = Mathf.Max(1, columns);

            for (int col = 0; col < cols; col++)
            {
                float x0 = col * s, x1 = x0 + s;

                // Outer bounds (north/south have no neighbor in a single row) — GREEN.
                Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.85f);
                Wall(new Vector3(x0, 0, 0), new Vector3(x1, 0, 0), h); // south (z = 0)
                Wall(new Vector3(x0, 0, s), new Vector3(x1, 0, s), h); // north (z = s)

                // Vertical edges = the crossing borders between continents — RED.
                Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
                Wall(new Vector3(x0, 0, 0), new Vector3(x0, 0, s), h); // west  edge (x = col·s)
                Wall(new Vector3(x1, 0, 0), new Vector3(x1, 0, s), h); // east  edge (x = (col+1)·s)
            }
        }

        private static void Wall(Vector3 a, Vector3 b, float h)
        {
            Vector3 up = Vector3.up * h;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(a + up, b + up);
            Gizmos.DrawLine(a, a + up);
            Gizmos.DrawLine(b, b + up);
        }
    }
}
