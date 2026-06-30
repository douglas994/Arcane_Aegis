using System.Collections.Generic;
using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>A quest as a ScriptableObject (synced to content.db — the server owns objectives/rewards). A QuestGiver
    /// NPC offers a list of these; accept → the server tracks the objectives → turn in → rewards. Create via
    /// Assets ▸ Create ▸ ArcaneMMO ▸ Quest.</summary>
    [CreateAssetMenu(fileName = "Quest_", menuName = "ArcaneMMO/Quest")]
    public class QuestDefinitionSO : ScriptableObject
    {
        /// <summary>Mirror of ArcaneShared.Enums.QuestObjectiveKind — order MUST match (byte-wired).</summary>
        public enum ObjectiveKind { KillMonster, CollectItem }

        [System.Serializable]
        public struct Objective
        {
            public ObjectiveKind kind;
            [Tooltip("Id do alvo: monstro (KillMonster) ou item (CollectItem).")] public string targetId;
            [Min(1)] public int count;
            [Tooltip("Texto mostrado no log (vazio = gerado do tipo/alvo).")] public string description;
        }

        [System.Serializable]
        public struct ItemReward { public ItemDefinitionSO item; [Min(1)] public int qty; }

        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Nível mínimo pra aceitar (0 = sem requisito).")] public int levelReq = 1;
        public List<Objective> objectives = new();

        [Header("Recompensas")]
        public int rewardXp;
        [Tooltip("Id da moeda (ex.: 'gold'). Vazio = sem moeda.")] public string rewardCurrency = "gold";
        public int rewardCurrencyAmount;
        public List<ItemReward> rewardItems = new();
        [Tooltip("Pode ser refeita depois de entregar.")] public bool repeatable;
    }
}
