using UnityEngine;
using ArcaneShared.Models;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// A living entity view (mirrors the server's Humanoid): adds the world-space health bar, driven by
    /// the snapshot's HpPercent. Players, monsters and NPCs all derive from this.
    /// </summary>
    public class HumanoidView : EntityView
    {
        [SerializeField] private EntityVitals vitals; // world-space health bar (above the head)

        protected override void Awake()
        {
            base.Awake();
            if (vitals == null) vitals = GetComponentInChildren<EntityVitals>();
        }

        public override void ApplySnapshot(in SnapshotEntry e)
        {
            base.ApplySnapshot(e);
            if (vitals != null) vitals.SetHp01(e.HpPercent / 255f);
        }

        /// <summary>The local player hides its world bar — its HP is shown on the HUD instead.</summary>
        public void ShowWorldVitals(bool show)
        {
            if (vitals != null) vitals.SetVisible(show);
        }
    }
}
