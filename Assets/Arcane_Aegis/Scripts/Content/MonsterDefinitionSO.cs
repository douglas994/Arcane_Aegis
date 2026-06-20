using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Content
{
    /// <summary>A monster as a ScriptableObject: gameplay (synced to content.db) + client art. Mirrors the server's
    /// MonsterDefinition / MonsterRecord. Stats + behavior + XP + a loot table (items dropped on death).
    /// Create via Assets ▸ Create ▸ ArcaneMMO ▸ Monster Definition.</summary>
    [CreateAssetMenu(fileName = "Monster_", menuName = "ArcaneMMO/Monster Definition")]
    public class MonsterDefinitionSO : ScriptableObject
    {
        [System.Serializable]
        public struct LootEntry
        {
            [Tooltip("Item que pode dropar.")] public ItemDefinitionSO item;
            [Tooltip("Chance de dropar (0–100%). LUK do matador dá um bônus pequeno.")] public int chancePct;
            [Tooltip("Quantidade mínima/máxima (inclusivo).")] public int min, max;
        }

        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;
        public int level = 1;
        [Tooltip("Hostil = agride sozinho · Neutro = só revida se apanhar · Passivo = não luta.")]
        public CreatureDisposition disposition = CreatureDisposition.Hostile;
        [Tooltip("Família (rótulo p/ loot/resistência/UI no futuro).")]
        public CreatureKind kind = CreatureKind.Humanoid;
        [Tooltip("Vida exata. 0 = calcula pela fórmula (100 + VIT×10 + Nível×5).")]
        public int maxHp = 0;
        [Space] public int str = 4, dex = 4, intel = 1, vit = 5, spi = 3, luk = 3, armor = 10;
        public ElementType element = ElementType.None;
        [Space]
        [Tooltip("Nota inimigos neste raio (m).")] public float aggroRadius = 10f;
        [Tooltip("Desiste e volta pra casa se arrastado a este raio (m).")] public float leashRadius = 22f;
        [Tooltip("Alcance pra atacar (m).")] public float attackRange = 2.5f;
        public float moveSpeed = 3.5f;
        [Tooltip("Skill de ataque legada (id 1..255). Usada só se a lista 'Skills da IA' abaixo estiver vazia.")] public int attackAbilityId = 1;
        [Tooltip("XP concedido a quem matar.")] public int xpReward = 25;

        [Header("IA — arquétipo + skills")]
        [Tooltip("Personalidade: Melee fecha distância · Ranged/Caster atiram de longe · Support cura aliados · Passive foge ao apanhar.")]
        public AiArchetype archetype = AiArchetype.Melee;
        [Tooltip("Skills que a IA pode usar (arraste os Skill assets). Vazio = usa a 'Skill de ataque' acima. O selector escolhe a melhor no alcance/CD.")]
        public List<SkillDefinitionSO> abilities = new();
        [Tooltip("Foge abaixo deste % de vida (0 = nunca; Passive foge sempre ao apanhar).")]
        [Range(0, 100)] public int fleeHpPct = 0;
        [Tooltip("Multiplicador de stats/loot pra versão Elite (1 = normal).")]
        public float eliteScale = 1f;
        [Tooltip("Raio de perambulação ocioso em volta do spawn (m). 0 = fica parado.")]
        public float patrolRadius = 0f;
        [Tooltip("Começa DORMINDO (emboscada): só acorda se algo chega muito perto ou se apanha; volta a dormir quando se acalma.")]
        public bool startsAsleep = false;

        [Header("Tipo — revela os campos extras (e o que é enviado ao servidor)")]
        [Tooltip("Marque pra virar um BOSS (mostra fases/enrage/anúncio). Desmarcado = monstro comum, campos somem.")]
        public bool isBoss = false;
        [Tooltip("Marque pra virar um BOSS SELADO (mostra requisitos/selo). Implica Boss.")]
        public bool isSealed = false;

        [Header("Boss (aparece com 'É Boss')")]
        [Tooltip("Timer de enrage: após N s em combate o dano dobra (0 = nunca).")]
        public float enrageSeconds = 0f;
        [Tooltip("Anuncia o spawn/morte do boss pra zona inteira (banner).")]
        public bool announceGlobal = false;
        [Tooltip("Fases por limiar de vida: entra quando a vida cai ao %. O servidor ordena alto→baixo.")]
        public List<BossPhaseSO> phases = new();

        [Header("Selado (opcional — vira boss selado)")]
        [Tooltip("Nível mínimo do jogador pra quebrar o selo. 0 = não é selado.")]
        public int sealMinLevel = 0;
        [Tooltip("Segundos até o selo voltar depois que o boss morre (recorrente).")]
        public float sealCooldown = 600f;
        [Tooltip("Itens-chave consumidos pra quebrar o selo (runas/receita/artefato).")]
        public List<SealReqSO> sealReqs = new();
        [Tooltip("Modelo 3D do SELO/altar no mundo (cliente). Vazio = cápsula padrão.")]
        public GameObject sealModel3D;

        [System.Serializable]
        public struct SealReqSO
        {
            [Tooltip("Item-chave exigido (consumido).")] public ItemDefinitionSO item;
            [Min(1)] public int qty;
        }

        [System.Serializable]
        public struct BossPhaseSO
        {
            [Tooltip("Entra nesta fase quando a vida cai a este % (0–100).")] [Range(0, 100)] public int hpPct;
            [Tooltip("Multiplicador de dano nesta fase (1 = normal).")] public float damageMult;
            [Tooltip("Monstro a invocar ao entrar na fase (arraste o asset; vazio = nenhum).")] public MonsterDefinitionSO summon;
            [Tooltip("Quantos invocar.")] public int summonCount;
        }

        [Header("Loot")]
        public List<LootEntry> loot = new();

        [Header("Client — hitbox de mira/seleção (NOT synced)")]
        [Tooltip("Raio do collider de mira (m). 0 = padrão (0.4). Escala com o modelo (boss grande, bicho pequeno).")]
        public float hitboxRadius = 0.5f;
        [Tooltip("Altura do collider de mira (m). 0 = padrão (2).")]
        public float hitboxHeight = 2f;

        [Header("Client art — NOT synced")]
        public Sprite icon;
        [TextArea] public string description;
        [Tooltip("Modelo 3D (futuro: aparência replicada).")] public GameObject model3D;
    }
}
