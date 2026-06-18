using System;
using ArcaneShared.Models;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>Client-side cache of the local player's party (from S2C_PartyState). Empty roster = not in a party. Members
    /// are keyed by the STABLE characterId (same id as <see cref="ClientSession.CharacterId"/>), so "me"/leader checks are
    /// trivial. Raises <see cref="OnChanged"/> so the party panel refreshes live.</summary>
    public static class PartyState
    {
        private static PartyMember[] _members = Array.Empty<PartyMember>();

        public static event Action OnChanged;

        public static uint LeaderCharacterId { get; private set; }
        public static PartyMember[] Members => _members;
        public static bool InParty => _members.Length > 0;
        public static uint MyCharacterId => ClientSession.CharacterId;
        public static bool AmLeader => InParty && LeaderCharacterId == ClientSession.CharacterId;

        public static void Set(uint leaderCharacterId, PartyMember[] members)
        {
            LeaderCharacterId = leaderCharacterId;
            _members = members ?? Array.Empty<PartyMember>();
            OnChanged?.Invoke();
        }

        /// <summary>Live HP% update for one member (from S2C_PartyVitals) — patches just their bar, no roster rebuild.</summary>
        public static void SetVitals(uint characterId, byte hpPercent)
        {
            for (int i = 0; i < _members.Length; i++)
                if (_members[i].CharacterId == characterId)
                {
                    _members[i].HpPercent = hpPercent;
                    OnChanged?.Invoke();
                    return;
                }
        }
    }
}
