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
        public static uint Token;
        public static byte ServerId;
        public static uint CharacterId; // chosen character (set on select → used to enter the world)
    }
}
