using UnityEngine;

// Escurece os personagens do inimigo conforme a noite (e clareia com o dia),
// usando o progresso do DayNightTransition (0 = noite, 1 = dia).
public class EnemyNightShade : MonoBehaviour
{
    [Header("Visual Noite/Dia")]
    [Tooltip("Multiplicador de brilho no inicio da noite (0.25 = bem escuro).")]
    [Range(0f, 1f)] public float nightBrightness = 0.25f;
    public float updateInterval = 0.25f;

    private struct ShadeEntry
    {
        public Renderer renderer;
        public int materialIndex;
        public Color baseColor;
    }

    private readonly System.Collections.Generic.List<ShadeEntry> entries = new System.Collections.Generic.List<ShadeEntry>();
    private MaterialPropertyBlock propertyBlock;
    private float nextUpdate;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

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

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer renderer = renderers[r];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

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
                    baseColor = color
                });
            }
        }

        ApplyShade(DayNightTransition.DayFactor);
    }

    void ApplyShade(float dayFactor)
    {
        if (propertyBlock == null || entries.Count == 0) return;

        float brightness = Mathf.Lerp(nightBrightness, 1f, Mathf.Clamp01(dayFactor));

        for (int i = 0; i < entries.Count; i++)
        {
            ShadeEntry entry = entries[i];
            if (entry.renderer == null) continue;

            Color shaded = entry.baseColor * brightness;
            shaded.a = entry.baseColor.a;

            entry.renderer.GetPropertyBlock(propertyBlock, entry.materialIndex);
            propertyBlock.SetColor(BaseColorId, shaded);
            propertyBlock.SetColor(ColorId, shaded);
            entry.renderer.SetPropertyBlock(propertyBlock, entry.materialIndex);
        }
    }
}
