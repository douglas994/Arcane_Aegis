using UnityEngine;
using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Combat;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_CombatEvent: a damage/heal result → spawn a floating number over the target (presentation only).</summary>
    public sealed class CombatEventHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public CombatEventHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_CombatEvent;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_CombatEvent();
            p.Deserialize(ref reader);

            CombatStance.Mark(p.SourceId); // attacker + victim → in combat (weapon sheathing)
            CombatStance.Mark(p.TargetId);

            var local = _entities.Local;
            Vector3 pos;
            bool found = false;
            if (local != null && p.TargetId == local.Id) { pos = local.transform.position; found = true; }
            else if (_entities.TryGetView(p.TargetId, out var v)) { pos = v.transform.position; found = true; }
            else pos = default;

            if (!found) return;

            bool heal = (p.Flags & S2C_CombatEvent.FlagHeal) != 0;
            bool crit = (p.Flags & S2C_CombatEvent.FlagCrit) != 0;
            DamagePopup.Spawn(pos + Vector3.up * 2f, p.Amount, crit, heal);

            // per-skill impact VFX on the target (damage hits; projectile skills are handled by ProjectileManager).
            if (!heal && CombatFx.Instance != null) CombatFx.Instance.SpawnImpact(p.AbilityId, pos + Vector3.up);
        }
    }
}
