using System.Collections.Generic;
using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>
    /// Runtime lookup of content SOs by id — the client uses this to fetch a class/race/gender's ART (model, icon,
    /// description) at runtime (creation/selection). Assign the SOs here (or use the Content Editor's "Collect").
    /// Create via Assets ▸ Create ▸ ArcaneMMO ▸ Content Library and reference it from the CharacterLobby.
    /// </summary>
    [CreateAssetMenu(fileName = "ContentLibrary", menuName = "ArcaneMMO/Content Library")]
    public class ContentLibrary : ScriptableObject
    {
        public List<ClassDefinitionSO> classes = new();
        public List<RaceDefinitionSO> races = new();
        public List<GenderDefinitionSO> genders = new();
        public List<CharacterTemplateSO> templates = new();

        public ClassDefinitionSO GetClass(string id) => classes.Find(c => c != null && c.id == id);
        public RaceDefinitionSO GetRace(string id) => races.Find(r => r != null && r.id == id);
        public GenderDefinitionSO GetGender(string id) => genders.Find(g => g != null && g.id == id);
        public CharacterTemplateSO GetTemplate(string id) => templates.Find(t => t != null && t.id == id);
    }
}
