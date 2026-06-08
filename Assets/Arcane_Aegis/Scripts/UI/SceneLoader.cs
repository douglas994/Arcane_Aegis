using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Loads a scene by name (wire a button OnClick or a UnityEvent → <see cref="Load"/> and type the scene name
    /// in the argument). Cross-scene state (account/char) travels in <see cref="Network.ClientSession"/>, so the
    /// per-scene connections just reconnect. Scenes must be added to Build Settings.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public void Load(string sceneName) => SceneManager.LoadScene(sceneName);
    }
}
