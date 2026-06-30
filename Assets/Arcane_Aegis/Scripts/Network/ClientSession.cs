namespace Arcane_Aegis.Network
{
    /// <summary>
    /// Cross-scene client session state: who we are (from the Master login) + what we picked. Static so it
    /// survives scene loads (Login → ServerSelect → Character → World). Dev: plain values; later the Gateway
    /// validates the token. (Eventually replace with a proper persistent session object if needed.)
    /// </summary>
    public static class ClientSession
    {
        public static uint AccountId;
        public static ulong Token;
        public static byte ServerId;
        public static uint CharacterId; // chosen character (set on select → used to enter the world)
        public static bool IsAdmin;     // server says this account is admin (S2C_LoginResult) → show the GM panel
        public static string ClassId = string.Empty; // local player's class (S2C_LoginResult) → drives the data-driven action bar (skills per class)
        public static string RaceId = string.Empty;  // local player's race (kept alongside class; handy for class/race-gated UI)
        public static byte CurrentDungeonDef;         // 0 = open world (World scene); >0 = inside a dungeon (Dungeon scene). Drives the scene swap on enter/exit.
    }
}
