using System.Collections.Generic;
using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>
    /// A talking/town NPC: its 3D model + a small branching DIALOGUE. The server only needs the <see cref="id"/> (a
    /// spawner places an <c>EntityType.Npc</c> by id; the client resolves the model + dialogue here) — the dialogue
    /// TEXT is pure client art (not synced), so editing lines needs no re-sync. An NPC can optionally also be a vendor
    /// (set <see cref="vendorId"/>) so the dialogue offers a "Loja" option. Create via Assets ▸ Create ▸ ArcaneMMO ▸ NPC.
    /// </summary>
    [CreateAssetMenu(fileName = "Npc_", menuName = "ArcaneMMO/NPC")]
    public class NpcDefinitionSO : ScriptableObject
    {
        /// <summary>The kind of NPC — organizes them + drives behavior. Townsfolk just talk; a Vendor offers its shop
        /// (<see cref="vendorId"/>); a Guard is a friendly fighter (Faction.Guards); a QuestGiver hands out quests (later).</summary>
        public enum NpcType { Townsfolk, Vendor, Guard, QuestGiver }

        /// <summary>What an answer option does when clicked.</summary>
        public enum DialogueAction { Goto, OpenShop, End }

        [System.Serializable]
        public struct Option
        {
            [Tooltip("Texto do botão (a resposta do jogador).")]
            public string label;
            public DialogueAction action;
            [Tooltip("Só para 'Goto': id do próximo nó de diálogo.")]
            public string nextNodeId;
        }

        [System.Serializable]
        public class Node
        {
            [Tooltip("Chave do nó (ex.: 'start', 'sobre_a_cidade'). O nó 'start' (ou o primeiro) é a saudação.")]
            public string id = "start";
            [TextArea] public string text;
            public List<Option> options = new();
        }

        [Header("Identidade")]
        public string id;
        [Tooltip("Tipo do NPC: Townsfolk (só fala), Vendor (abre loja via 'Vendor id'), Guard (lutador aliado), QuestGiver (missões — futuro).")]
        public NpcType type = NpcType.Townsfolk;
        public string displayName;
        [Tooltip("Modelo 3D do NPC no mundo.")] public GameObject model3D;
        [TextArea] public string description;

        [Header("Diálogo (client art — não sincroniza)")]
        [Tooltip("Nós do diálogo. O nó com id 'start' (ou o primeiro da lista) é a saudação.")]
        public List<Node> nodes = new();

        [Header("Loja (só quando Tipo = Vendor)")]
        [Tooltip("Itens que esse NPC VENDE. Cada um é precificado pelo npcPrice/priceCurrency do próprio item. " +
                 "Sincroniza pro server (tabela de vendor, chaveada pelo id do NPC) — o server valida a compra.")]
        public List<ItemDefinitionSO> stock = new();

        /// <summary>True for a Vendor-type NPC (the dialogue offers "Loja"; the shop is keyed by this NPC's id).</summary>
        public bool Sells => type == NpcType.Vendor;

        [Header("Missões (só quando Tipo = QuestGiver)")]
        [Tooltip("Quests que esse NPC OFERECE. Sincronizam pro server (content.db); o NPC mostra as disponíveis/entregáveis.")]
        public List<QuestDefinitionSO> quests = new();

        /// <summary>True for a QuestGiver NPC (interacting opens the quest panel instead of plain dialogue).</summary>
        public bool GivesQuests => type == NpcType.QuestGiver;

        /// <summary>The greeting node ("start" by name, else the first authored node, else null).</summary>
        public Node EntryNode()
        {
            if (nodes == null || nodes.Count == 0) return null;
            for (int i = 0; i < nodes.Count; i++) if (nodes[i] != null && nodes[i].id == "start") return nodes[i];
            return nodes[0];
        }

        /// <summary>Finds a node by id (for an option's Goto), or null.</summary>
        public Node FindNode(string nodeId)
        {
            if (nodes == null || string.IsNullOrEmpty(nodeId)) return null;
            for (int i = 0; i < nodes.Count; i++) if (nodes[i] != null && nodes[i].id == nodeId) return nodes[i];
            return null;
        }
    }
}
