using UnityEngine.EventSystems;
using TMPro;

namespace Arcane_Aegis.UI
{
    /// <summary>Is the player typing in a text field right now? Gameplay input (movement, ability/interact/gather keys,
    /// panel hotkeys) checks this so keystrokes meant for chat/inputs don't leak into the game.</summary>
    public static class UiFocus
    {
        public static bool IsTyping
        {
            get
            {
                var es = EventSystem.current;
                var sel = es != null ? es.currentSelectedGameObject : null;
                if (sel == null) return false;
                var input = sel.GetComponent<TMP_InputField>();
                return input != null && input.isFocused;
            }
        }
    }
}
