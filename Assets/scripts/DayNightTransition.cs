using UnityEngine;

public class DayNightTransition : MonoBehaviour
{
    [Header("Referencias")]
    public Light directionalLight;    // Arraste a Directional Light aqui
    public Material skyboxMaterial;   // Arraste o CustomSkyboxMat aqui

    [Header("Tempo da Transicao (em segundos)")]
    public float transitionDuration = 120f;

    [Header("Configuracoes de Noite (Inicio)")]
    public Color nightFogColor = new Color(0.04f, 0.05f, 0.08f);
    public Color nightAmbientColor = new Color(0.06f, 0.07f, 0.11f);
    public Color nightSkyTint = new Color(0.02f, 0.03f, 0.06f);
    public float nightLightIntensity = 0.15f;

    [Header("Configuracoes de Dia (Fim)")]
    public Color dayFogColor = new Color(0.65f, 0.72f, 0.78f);
    public Color dayAmbientColor = new Color(0.45f, 0.48f, 0.52f);
    public Color daySkyTint = new Color(0.45f, 0.55f, 0.68f);
    public float dayLightIntensity = 1.1f;

    [Header("Evolucao do Fog (Comeca limpo e vai fechando)")]
    public float initialFogStart = 75f;
    public float initialFogEnd = 85f;
    public float finalFogStart = 5f;
    public float finalFogEnd = 45f;

    [Header("Rotacao do Sol")]
    public Vector3 nightSunRotation = new Vector3(15f, -30f, 0f);
    public Vector3 daySunRotation = new Vector3(50f, -30f, 0f);

    private float elapsedTime = 0f;

    // 0 = noite, 1 = dia. Usado por outros sistemas (ex.: escurecer inimigos).
    public static float DayFactor { get; private set; } = 0f;

    void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        ApplyAtmosphere(0f);
    }

    void Update()
    {
        // O tempo so anda depois que o jogador clica em Jogar: o menu fica
        // sempre na mesma noite inicial.
        if (MainMenu.IsOpen) return;

        if (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / transitionDuration);
            ApplyAtmosphere(t);
        }
    }

    void ApplyAtmosphere(float t)
    {
        DayFactor = Mathf.Clamp01(t);

        Color currentFogColor = Color.Lerp(nightFogColor, dayFogColor, t);
        RenderSettings.fogColor = currentFogColor;

        RenderSettings.fogStartDistance = Mathf.Lerp(initialFogStart, finalFogStart, t);
        RenderSettings.fogEndDistance = Mathf.Lerp(initialFogEnd, finalFogEnd, t);

        RenderSettings.ambientLight = Color.Lerp(nightAmbientColor, dayAmbientColor, t);

        // Atualiza as cores do Skybox Procedural para acompanhar a hora do dia
        if (skyboxMaterial != null)
        {
            Color currentSkyTint = Color.Lerp(nightSkyTint, daySkyTint, t);
            skyboxMaterial.SetColor("_Tint", currentSkyTint);
            skyboxMaterial.SetFloat("_Exposure", Mathf.Lerp(0.4f, 1.2f, t));
        }

        if (directionalLight != null)
        {
            directionalLight.intensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, t);
            directionalLight.transform.rotation = Quaternion.Euler(Vector3.Lerp(nightSunRotation, daySunRotation, t));
        }
    }
}