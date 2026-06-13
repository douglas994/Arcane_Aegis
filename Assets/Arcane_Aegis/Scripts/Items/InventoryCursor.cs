using System;
using UnityEngine;

namespace Arcane_Aegis.Items
{
    /// <summary>
    /// The "held" item in the click-to-pick → click-to-place model. Click an item to PICK it (it becomes the cursor's
    /// payload); click a destination slot/trash to PLACE it (the panel reads <see cref="PickedInstanceId"/> and sends
    /// the move/discard). One picked item at a time; <see cref="OnChanged"/> lets slots show a "picked" highlight.
    /// Persistent singleton (auto-created), like <see cref="InventoryStore"/>.
    /// </summary>
    public sealed class InventoryCursor : MonoBehaviour
    {
        public static InventoryCursor Instance { get; private set; }

        /// <summary>The instance id currently held, or 0 if nothing is picked.</summary>
        public uint PickedInstanceId { get; private set; }
        public bool HasPicked => PickedInstanceId != 0;

        /// <summary>Raised whenever the pick changes (panels re-render to show/clear the highlight).</summary>
        public event Action OnChanged;

        /// <summary>Picks an item (or re-picking the same one cancels — a toggle, so a double-click deselects).</summary>
        public void Pick(uint instanceId)
        {
            PickedInstanceId = (instanceId == PickedInstanceId) ? 0u : instanceId;
            OnChanged?.Invoke();
        }

        public void Clear()
        {
            if (PickedInstanceId == 0) return;
            PickedInstanceId = 0;
            OnChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("InventoryCursor");
            Instance = go.AddComponent<InventoryCursor>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
