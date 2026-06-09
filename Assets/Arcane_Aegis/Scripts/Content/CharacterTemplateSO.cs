using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>One gender variant of a template: the 3D model + (optional) icon override for that gender.</summary>
    [Serializable]
    public class GenderModel
    {
        public GenderDefinitionSO gender;
        public GameObject model;
        public Sprite icon; // optional; falls back to gender.icon when null
    }

    /// <summary>
    /// A playable character archetype — the Atavism "Player Template". It BINDS a Race + Class (referencing the
    /// catalogs, not redefining them), the spawn, the starting progression, and a 3D model per gender. The CLIENT
    /// uses it for creation/selection (model + icons); race/class STATS still come from the catalogs synced to the
    /// server. Create via Assets ▸ Create ▸ ArcaneMMO ▸ Character Template.
    /// </summary>
    [CreateAssetMenu(fileName = "Template_", menuName = "ArcaneMMO/Character Template")]
    public class CharacterTemplateSO : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        public RaceDefinitionSO race;
        public ClassDefinitionSO characterClass;
        public string faction = "Neutral";

        [Header("Spawn")]
        public string zoneId = "veridia";
        public Vector3 spawnPosition;
        public float orientation;

        [Header("Progression")]
        public int startingLevel = 1;
        public string levelXpProfile = "default";

        [Header("Genders — model + icon per gender")]
        public List<GenderModel> genders = new();

        public GenderModel GetGender(string genderId) =>
            genders.Find(g => g != null && g.gender != null && g.gender.id == genderId);
    }
}
