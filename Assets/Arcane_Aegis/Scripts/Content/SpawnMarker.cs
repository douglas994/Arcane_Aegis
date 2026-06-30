using System.Collections.Generic;
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
        [Tooltip("Monstro a spawnar (deixe vazio se for nó/vendedor).")]
        public MonsterDefinitionSO monster;
        [Tooltip("Nó de recurso a spawnar (árvore/pedra). Tem prioridade sobre o monstro.")]
        public ResourceNodeDefinitionSO node;
        [Tooltip("NPC (falante/vendedor/guarda…). Tem prioridade sobre nó e monstro. Vendedor = NpcDefinition tipo Vendor.")]
        public NpcDefinitionSO npc;

        [Header("Portal de dungeon (opcional — tem prioridade sobre tudo)")]
        [Tooltip("Marque pra este marcador ser um PORTAL de dungeon (ignora monstro/nó/npc).")]
        public bool portal;
        [Tooltip("Template da dungeon a entrar (>0, ex: 100). 0 = portal de SAÍDA (coloque DENTRO da dungeon → volta pro mundo).")]
        public int dungeonDef;

        [Header("Fogueira / estação de cozinha (opcional)")]
        [Tooltip("Marque pra este marcador ser uma FOGUEIRA — receitas de cozinha precisam de uma por perto (ignora monstro/nó/npc/portal).")]
        public bool campfire;

        [Min(1)] public int count = 1;
        [Min(0f)] public float radius = 5f;
        [Tooltip("Segundos pra reviver cada criatura. 0 = não respawna (chefe/evento).")]
        [Min(0f)] public float respawnSeconds = 15f;

        [Header("Elite raro (opcional — só monstros)")]
        [Tooltip("Chance (0..1) de cada criatura spawnar como ELITE (stats/loot escalados, prefixo 'Elite'). 0 = nunca.")]
        [Range(0f, 1f)] public float eliteChance = 0f;
        [Tooltip("Multiplicador do elite (≤1 usa o padrão 1.6).")]
        [Min(0f)] public float eliteScale = 1.6f;

        [Header("Patrulha por pontos (opcional — só monstros)")]
        [Tooltip("Pontos de patrulha (arraste Transforms da cena). O monstro anda entre eles em ordem. Vazio = sem patrulha por pontos.")]
        public List<Transform> patrolPoints = new();

        private void OnDrawGizmos()
        {
            // magenta = portal, orange = campfire, cyan = npc, green = resource node, red = monster, grey = empty.
            Color c = portal ? new Color(0.85f, 0.3f, 1f, 1f)
                    : campfire ? new Color(1f, 0.55f, 0.1f, 1f)
                    : npc != null ? new Color(0.3f, 0.8f, 1f, 1f)
                    : node != null ? new Color(0.3f, 0.85f, 0.3f, 1f)
                    : monster != null ? new Color(1f, 0.4f, 0.2f, 1f)
                    : new Color(0.6f, 0.6f, 0.6f, 1f);
            Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.3f, radius));
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position, 0.4f);

            // Patrol path: line from the spawn through each waypoint (and a marker at each).
            if (patrolPoints != null && patrolPoints.Count > 0)
            {
                Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
                Vector3 prev = transform.position;
                foreach (var pt in patrolPoints)
                {
                    if (pt == null) continue;
                    Gizmos.DrawLine(prev, pt.position);
                    Gizmos.DrawWireSphere(pt.position, 0.3f);
                    prev = pt.position;
                }
            }
#if UNITY_EDITOR
            string name = portal ? (dungeonDef > 0 ? $"Portal → dungeon {dungeonDef}" : "Portal de SAÍDA")
                        : campfire ? "Fogueira (cozinha)"
                        : npc != null ? (string.IsNullOrEmpty(npc.displayName) ? npc.name : npc.displayName)
                        : node != null ? (string.IsNullOrEmpty(node.displayName) ? node.name : node.displayName)
                        : monster != null ? (string.IsNullOrEmpty(monster.displayName) ? monster.name : monster.displayName)
                        : "(vazio)";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.6f, $"{name} ×{count}");
#endif
        }
    }
}
