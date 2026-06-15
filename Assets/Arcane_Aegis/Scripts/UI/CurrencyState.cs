using System;
using System.Collections.Generic;

namespace Arcane_Aegis.UI
{
    /// <summary>Client-side cache of the local player's wallet (from S2C_Currency), keyed by currency id. A HUD reads it;
    /// raises <see cref="OnChanged"/> so the HUD refreshes live.</summary>
    public static class CurrencyState
    {
        private static readonly Dictionary<string, long> _byId = new();

        public static event Action OnChanged;

        public static long Amount(string currencyId) => currencyId != null && _byId.TryGetValue(currencyId, out var a) ? a : 0;

        /// <summary>Replaces the whole wallet (full snapshot from the server).</summary>
        public static void SetAll(IEnumerable<(string id, long amount)> wallet)
        {
            _byId.Clear();
            if (wallet != null) foreach (var (id, amount) in wallet) if (!string.IsNullOrEmpty(id)) _byId[id] = amount;
            OnChanged?.Invoke();
        }
    }
}
