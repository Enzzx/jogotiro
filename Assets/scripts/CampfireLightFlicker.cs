using UnityEngine;

// Faz a luz da fogueira subir e descer de leve, como uma chama tremulando.
[RequireComponent(typeof(Light))]
public class CampfireLightFlicker : MonoBehaviour
{
    [Tooltip("Intensidade media da luz.")]
    public float baseIntensity = 1.6f;

    [Tooltip("Quanto a intensidade sobe e desce em relacao a media (0.25 = +-25%).")]
    [Range(0f, 1f)] public float intensityVariation = 0.22f;

    [Tooltip("Quanto maior, mais rapida e nervosa a tremulacao.")]
    public float flickerSpeed = 2.2f;

    [Tooltip("Variacao suave extra no alcance da luz.")]
    [Range(0f, 1f)] public float rangeVariation = 0.12f;

    private Light fireLight;
    private float baseRange;
    private float seed;

    void Awake()
    {
        fireLight = GetComponent<Light>();
        baseRange = fireLight.range;
        seed = Random.Range(0f, 128f);

        if (baseIntensity <= 0f)
            baseIntensity = fireLight.intensity;
    }

    void Update()
    {
        if (fireLight == null) return;

        float t = Time.time * flickerSpeed;

        // Duas frequencias somadas dao um tremular menos repetitivo.
        float noise = Mathf.PerlinNoise(seed, t) * 2f - 1f;
        float fastNoise = (Mathf.PerlinNoise(seed + 37f, t * 2.7f) * 2f - 1f) * 0.35f;

        float offset = Mathf.Clamp(noise + fastNoise, -1f, 1f);

        fireLight.intensity = Mathf.Max(0f, baseIntensity * (1f + offset * intensityVariation));
        fireLight.range = baseRange * (1f + offset * rangeVariation);
    }
}
