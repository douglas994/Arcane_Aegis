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
        /// <summary>The loaded library, so runtime UI (skill bar, tooltips) can resolve content without a per-component
        /// serialized reference. Set when the SO is loaded (it's referenced by EntityManager/CombatFx, so it loads).</summary>
        public static ContentLibrary Active { get; private set; }
        private void OnEnable() { if (Active == null) Active = this; }


        public List<ClassDefinitionSO> classes = new();
        public List<RaceDefinitionSO> races = new();
        public List<GenderDefinitionSO> genders = new();
        public List<CharacterTemplateSO> templates = new();
        public List<ItemDefinitionSO> items = new();
        public List<SkillDefinitionSO> skills = new();
        public List<StatusDefinitionSO> statuses = new();
        public List<MonsterDefinitionSO> monsters = new();
        public List<ResourceNodeDefinitionSO> resourceNodes = new();
        public List<RecipeDefinitionSO> recipes = new();
        public List<CurrencyDefinitionSO> currencies = new();
        public List<PetDefinitionSO> pets = new();
        public List<MountDefinitionSO> mounts = new();
        public List<NpcDefinitionSO> npcs = new();
        public List<QuestDefinitionSO> quests = new();
        public List<DungeonDefinitionSO> dungeons = new();
        public List<BuildingDefinitionSO> buildingDefs = new();

        /// <summary>One generic 3D prefab/model for ALL dungeon portals (entrance + exit render the same for now). Assign
        /// by hand. The server replicates portals as <c>EntityType.Portal</c>; the client renders this prefab for them.</summary>
        public GameObject portalPrefab;

        /// <summary>One generic 3D prefab for ALL campfires (cooking stations). The server replicates them as
        /// <c>EntityType.Campfire</c>; the client renders this prefab. Stand near one to craft "cooking" recipes.</summary>
        public GameObject campfirePrefab;

        public ClassDefinitionSO GetClass(string id) => classes.Find(c => c != null && c.id == id);
        public RaceDefinitionSO GetRace(string id) => races.Find(r => r != null && r.id == id);
        public GenderDefinitionSO GetGender(string id) => genders.Find(g => g != null && g.id == id);
        public CharacterTemplateSO GetTemplate(string id) => templates.Find(t => t != null && t.id == id);
        public ItemDefinitionSO GetItem(string id) => items.Find(i => i != null && i.id == id);
        public SkillDefinitionSO GetSkill(int id) => skills.Find(s => s != null && s.id == id);   // for the skill bar (icon/cooldown)
        public StatusDefinitionSO GetStatus(string id) => statuses.Find(s => s != null && s.id == id);
        public MonsterDefinitionSO GetMonster(string id) => monsters.Find(m => m != null && m.id == id);
        public ResourceNodeDefinitionSO GetResourceNode(string id) => resourceNodes.Find(n => n != null && n.id == id);
        public RecipeDefinitionSO GetRecipe(string id) => recipes.Find(r => r != null && r.id == id);
        public CurrencyDefinitionSO GetCurrency(string id) => currencies.Find(c => c != null && c.id == id);
        public PetDefinitionSO GetPet(string id) => pets.Find(p => p != null && p.id == id);       // pet model/icon by id
        public MountDefinitionSO GetMount(string id) => mounts.Find(m => m != null && m.id == id);  // mount model/icon by id
        public NpcDefinitionSO GetNpc(string id) => npcs.Find(n => n != null && n.id == id);          // npc model + dialogue by id
        public QuestDefinitionSO GetQuest(string id) => quests.Find(q => q != null && q.id == id);     // quest def by id
        public DungeonDefinitionSO GetDungeon(byte id) => dungeons.Find(d => d != null && d.id == id);   // dungeon def (scene) by template id
        public BuildingDefinitionSO GetBuildingDef(string id) => buildingDefs.Find(b => b != null && b.id == id); // building-piece def (prefab/cost) by id

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
