using UnityEngine;

namespace Arcane_Aegis.Controllers
{
    /// <summary>A connection point on a building piece (Rust/Valheim-style snapping). Drop empty child GameObjects with
    /// this on a piece's prefab where it should connect to others — e.g. the 4 edges of a floor, the two ends + base of a
    /// wall. The <see cref="BuildModeController"/> snaps a ghost so one of ITS sockets meets a nearby placed piece's
    /// socket (position + facing). The blue arrow (Z/forward) is the connection direction: two sockets connect when their
    /// arrows face each other. <see cref="kind"/> filters what connects (Any connects to everything).</summary>
    public sealed class BuildSnapPoint : MonoBehaviour
    {
        public enum Kind { Any, Floor, Wall, Edge }

        [Tooltip("O que pode encaixar aqui. Any encaixa com qualquer um. Use Floor/Wall pra separar chão de parede.")]
        public Kind kind = Kind.Any;

        /// <summary>True if two sockets may connect (same kind, or either is Any).</summary>
        public static bool Compatible(Kind a, Kind b) => a == b || a == Kind.Any || b == Kind.Any;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color c = kind switch
            {
                Kind.Floor => new Color(0.3f, 0.85f, 0.3f, 1f),
                Kind.Wall => new Color(1f, 0.6f, 0.2f, 1f),
                Kind.Edge => new Color(0.9f, 0.8f, 0.2f, 1f),
                _ => new Color(0.3f, 0.7f, 1f, 1f),
            };
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position, 0.12f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f); // connection direction
        }
#endif
    }
}
