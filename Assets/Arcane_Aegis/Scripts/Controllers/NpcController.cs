using UnityEngine;
using UnityEngine.InputSystem;
using Arcane_Aegis.Content;
using Arcane_Aegis.Entities;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Controllers
{
    /// <summary>"Press E to interact": near an NPC, a VENDOR opens its shop directly; any other NPC opens its dialogue.
    /// (Toggles the open panel closed if E is pressed again.) Drop this on a scene object once. All NPCs are
    /// <c>EntityType.Npc</c>; the NpcDefinition's type decides shop-vs-talk.</summary>
    public sealed class NpcController : MonoBehaviour
    {
        [SerializeField] private float range = 4f;
        [SerializeField] private Key interactKey = Key.E;

        private EntityManager _entities;

        private void Awake() => _entities = FindAnyObjectByType<EntityManager>();

        private void Update()
        {
            if (_entities == null) { _entities = FindAnyObjectByType<EntityManager>(); return; }
            var local = _entities.Local;
            var kb = Keyboard.current;
            if (local == null || kb == null) return;
            if (!kb[interactKey].wasPressedThisFrame || UiFocus.IsTyping) return;

            // Pressing E with a panel open just closes it.
            if (ShopPanel.Instance != null && ShopPanel.Instance.IsOpen) { ShopPanel.Instance.Close(); return; }
            if (QuestPanel.Instance != null && QuestPanel.Instance.IsOpen) { QuestPanel.Instance.Close(); return; }
            if (DialoguePanel.Instance != null && DialoguePanel.Instance.IsOpen) { DialoguePanel.Instance.Close(); return; }

            var npc = _entities.NearestNpc(local.transform.position, range);
            if (npc == null) return;

            // By type: Vendor → shop · QuestGiver → quest panel · everyone else → dialogue.
            var def = ContentLibrary.Active != null ? ContentLibrary.Active.GetNpc(npc.ContentId) : null;
            if (def != null && def.Sells)
            {
                if (ShopPanel.Instance != null) ShopPanel.Instance.Open(npc.ContentId);
                else Debug.LogWarning("[Npc] NPC é Vendor mas NÃO há ShopPanel na cena — crie/ative o painel de loja (ArcaneMMO ▸ UI ▸ Shop Panel).");
                return;
            }
            if (def != null && def.GivesQuests)
            {
                if (QuestPanel.Instance != null) QuestPanel.Instance.Open(npc.ContentId);
                else Debug.LogWarning("[Npc] NPC é QuestGiver mas NÃO há QuestPanel na cena — crie/ative o painel de quests.");
                return;
            }
            if (DialoguePanel.Instance != null) DialoguePanel.Instance.Open(npc.ContentId);
        }
    }
}
