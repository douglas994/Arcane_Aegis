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

        /// <summary>The 3D model for a character's look: the CharacterTemplate matched by race+class, then that
        /// gender's model. Falls back to same-race (any class), then any template with a model. Null if none.
        /// Shared by the creation preview AND the in-world spawn so they always show the SAME model.</summary>
        public GameObject ResolveModel(string raceId, string classId, string genderId)
        {
            if (templates == null) return null;
            CharacterTemplateSO tpl = templates.Find(t => t != null
                && t.race != null && t.race.id == raceId
                && t.characterClass != null && t.characterClass.id == classId);
            if (tpl == null) tpl = templates.Find(t => t != null && t.race != null && t.race.id == raceId
                && t.genders != null && t.genders.Exists(g => g != null && g.model != null));
            if (tpl == null) tpl = templates.Find(t => t != null && t.genders != null && t.genders.Exists(g => g != null && g.model != null));
            if (tpl == null) return null;
            GenderModel gm = tpl.GetGender(genderId);
            if (gm == null || gm.model == null) gm = tpl.genders.Find(g => g != null && g.model != null);
            return gm != null ? gm.model : null;
        }
    }
}
