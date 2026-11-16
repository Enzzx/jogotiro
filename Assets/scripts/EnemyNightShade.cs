using UnityEngine;

// Escurece os personagens do inimigo conforme a noite (e clareia com o dia),
// usando o progresso do DayNightTransition (0 = noite, 1 = dia).
public class EnemyNightShade : MonoBehaviour
{
    [Header("Visual Noite/Dia")]
    [Tooltip("Multiplicador de brilho no inicio da noite (0.25 = bem escuro).")]
    [Range(0f, 1f)] public float nightBrightness = 0.25f;
    public float updateInterval = 0.25f;

    [Header("Silhueta do modelo noturno (Ch30)")]
    [Tooltip("Brilho do modelo Ch30. Perto de 0 ele fica preto, virando apenas uma silhueta.")]
    [Range(0f, 0.2f)] public float silhouetteBrightness = 0.02f;
    [Tooltip("Remove o brilho especular do modelo noturno para a silhueta ficar uniforme.")]
    public bool silhouetteMatte = true;

    private struct ShadeEntry
    {
        public Renderer renderer;
        public int materialIndex;
        public Color baseColor;
        public bool silhouette;
    }

    private readonly System.Collections.Generic.List<ShadeEntry> entries = new System.Collections.Generic.List<ShadeEntry>();
    private MaterialPropertyBlock propertyBlock;
    private float nextUpdate;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        propertyBlock = new MaterialPropertyBlock();
        RefreshRenderers();
    }

    void Update()
    {
        if (Time.time < nextUpdate) return;
        nextUpdate = Time.time + updateInterval;
        ApplyShade(DayNightTransition.DayFactor);
    }

    // Recoleta os renderers visiveis (chamar quando o modelo do inimigo troca).
    public void RefreshRenderers()
    {
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        entries.Clear();

        // O modelo noturno (Ch30) vira silhueta: albedo quase preto e sem brilho.
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        Transform nightRoot = enemyAI != null && enemyAI.nightModel != null
            ? enemyAI.nightModel.transform
            : null;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer renderer = renderers[r];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

            bool isNightModel = nightRoot != null && renderer.transform.IsChildOf(nightRoot);

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) continue;

                Color color;
                if (material.HasProperty(BaseColorId)) color = material.GetColor(BaseColorId);
                else if (material.HasProperty(ColorId)) color = material.GetColor(ColorId);
                else continue;

                entries.Add(new ShadeEntry
                {
                    renderer = renderer,
                    materialIndex = i,
                    baseColor = color,
                    silhouette = isNightModel
                });
            }
        }

        ApplyShade(DayNightTransition.DayFactor);
    }

    void ApplyShade(float dayFactor)
    {
        if (propertyBlock == null || entries.Count == 0) return;

        for (int i = 0; i < entries.Count; i++)
        {
            ShadeEntry entry = entries[i];
            if (entry.renderer == null) continue;

            float brightness = entry.silhouette
                ? silhouetteBrightness
                : Mathf.Lerp(nightBrightness, 1f, Mathf.Clamp01(dayFactor));

            Color shaded = entry.baseColor * brightness;
            shaded.a = entry.baseColor.a;

            entry.renderer.GetPropertyBlock(propertyBlock, entry.materialIndex);
            propertyBlock.SetColor(BaseColorId, shaded);
            propertyBlock.SetColor(ColorId, shaded);

            if (entry.silhouette && silhouetteMatte)
            {
                propertyBlock.SetFloat(SmoothnessId, 0f);
                propertyBlock.SetFloat(GlossinessId, 0f);
                propertyBlock.SetFloat(MetallicId, 0f);
                propertyBlock.SetColor(EmissionColorId, Color.black);
            }

            entry.renderer.SetPropertyBlock(propertyBlock, entry.materialIndex);
        }
    }
}
