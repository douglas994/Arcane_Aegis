using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>
    /// A spawn point placed in the WORLD scene (WYSIWYG): you drop it on the terrain where creatures should appear.
    /// The Content Editor's "Exportar Spawners" reads every SpawnMarker in the scene → content.db; the server then
    /// keeps <see cref="count"/> of <see cref="monster"/> alive within <see cref="radius"/>, reviving after
    /// <see cref="respawnSeconds"/> (0 = one-time). The gizmo shows the area so placement is visual.
    /// </summary>
    public sealed class SpawnMarker : MonoBehaviour
    {
        [Tooltip("Monstro a spawnar (deixe vazio se for um nó de recurso).")]
        public MonsterDefinitionSO monster;
        [Tooltip("Nó de recurso a spawnar (árvore/pedra). Tem prioridade sobre o monstro.")]
        public ResourceNodeDefinitionSO node;
        [Min(1)] public int count = 1;
        [Min(0f)] public float radius = 5f;
        [Tooltip("Segundos pra reviver cada criatura. 0 = não respawna (chefe/evento).")]
        [Min(0f)] public float respawnSeconds = 15f;

        private void OnDrawGizmos()
        {
            // green = resource node, red = monster, grey = empty.
            Color c = node != null ? new Color(0.3f, 0.85f, 0.3f, 1f)
                    : monster != null ? new Color(1f, 0.4f, 0.2f, 1f)
                    : new Color(0.6f, 0.6f, 0.6f, 1f);
            Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.3f, radius));
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position, 0.4f);
#if UNITY_EDITOR
            string name = node != null ? (string.IsNullOrEmpty(node.displayName) ? node.name : node.displayName)
                        : monster != null ? (string.IsNullOrEmpty(monster.displayName) ? monster.name : monster.displayName)
                        : "(vazio)";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.6f, $"{name} ×{count}");
#endif
        }
    }
}
