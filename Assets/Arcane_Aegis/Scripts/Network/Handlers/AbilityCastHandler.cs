using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Combat;
using Arcane_Aegis.Content;
using Arcane_Aegis.Entities;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_AbilityCast: our own cast → set the authoritative cooldown; a remote cast → play its anim.</summary>
    public sealed class AbilityCastHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public AbilityCastHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_AbilityCast;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_AbilityCast();
            p.Deserialize(ref reader);

            CombatStance.Mark(p.CasterId); // casting → in combat (drives weapon sheathing)

            var local = _entities.Local;
            bool isOwn = local != null && p.CasterId == local.Id;

            // resolve the caster's view (local or remote) → drive the wind-up telegraph for both.
            EntityView casterView = isOwn ? local : (_entities.TryGetView(p.CasterId, out var v) ? v : null);
            if (p.CastMs > 0 && casterView != null && TelegraphManager.Instance != null)
                TelegraphManager.Instance.Show(casterView, p.AbilityId, p.CastMs / 1000f);

            // AoE area VFX (explosion/cone) at the moment the effect resolves (after the wind-up).
            if (casterView != null && CombatFx.Instance != null)
                CombatFx.Instance.PlayArea(casterView.transform, p.AbilityId, p.CastMs / 1000f);

            if (isOwn)
            {
                // own cast with a wind-up → show the HUD cast bar (name + icon from the skill def).
                if (p.CastMs > 0 && CastBar.Instance != null)
                {
                    var def = ContentLibrary.Active != null ? ContentLibrary.Active.GetSkill(p.AbilityId) : null;
                    CastBar.Instance.Show(def != null ? def.displayName : null, p.CastMs / 1000f, def != null ? def.icon : null);
                }
                // own cast confirmed → authoritative cooldown (we already predicted the anim/VFX locally)
                if (local.Combat != null) local.Combat.OnServerCooldown(p.AbilityId, p.CooldownMs / 1000f);
            }
            else if (casterView != null) casterView.PlayCast(p.AbilityId); // remote → skill anim + cast VFX
            else _entities.PlayAttack(p.CasterId);
        }
    }
}
