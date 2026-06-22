using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Models;
using Arcane_Aegis.Content;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>The "stable": the player's owned pets + mounts (from <see cref="CompanionState"/>). Each row shows the
    /// icon/name + which is active, with a "Set active" button (→ C2S_SetActiveCompanion). Visibility is handled by a
    /// UIPanelToggle on the window; this only fills the list. Build by hand and wire container + row template.</summary>
    public sealed class CollectionPanel : MonoBehaviour
    {
        [SerializeField] private Transform container;     // a vertical layout that holds the rows
        [SerializeField] private CollectionRow rowTemplate; // one row to clone (kept inactive)

        private readonly List<CollectionRow> _pool = new();

        private void Awake() { if (rowTemplate != null) rowTemplate.gameObject.SetActive(false); }
        private void OnEnable() { CompanionState.OnChanged += Rebuild; Rebuild(); }
        private void OnDisable() { CompanionState.OnChanged -= Rebuild; }

        private void Rebuild()
        {
            if (container == null || rowTemplate == null) return;
            var comps = CompanionState.Companions;
            while (_pool.Count < comps.Length) _pool.Add(Instantiate(rowTemplate, container));
            var lib = ContentLibrary.Active;

            for (int i = 0; i < _pool.Count; i++)
            {
                if (i >= comps.Length) { _pool[i].gameObject.SetActive(false); continue; }
                var c = comps[i];
                string name = c.DefId;
                Sprite icon = null;
                if (lib != null)
                {
                    if (c.Kind == OwnedCompanion.KindMount)
                    { var m = lib.GetMount(c.DefId); if (m != null) { name = string.IsNullOrEmpty(m.displayName) ? m.id : m.displayName; icon = m.icon; } }
                    else
                    { var p = lib.GetPet(c.DefId); if (p != null) { name = string.IsNullOrEmpty(p.displayName) ? p.id : p.displayName; icon = p.icon; } }
                }
                string prefix = c.Kind == OwnedCompanion.KindMount ? "[Montaria] " : "[Pet] ";
                _pool[i].gameObject.SetActive(true);
                _pool[i].Bind(c.Kind, c.DefId, prefix + name, icon, c.Active, OnSetActive);
            }
        }

        private static void OnSetActive(byte kind, string defId) => NetClient.Instance?.SendSetActiveCompanion(kind, defId);
    }
}
