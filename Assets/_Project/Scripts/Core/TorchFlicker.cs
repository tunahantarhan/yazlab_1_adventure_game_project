using System.Collections.Generic;
using UnityEngine;

namespace AdventureGame.Core
{
    // Point Light'lara meşale titremesi verir, her ışık kendi gürültü ofsetiyle titrer.
    // Sahnede tek bir boş objeye eklenmesi yeterlidir (affectWholeScene), istenirse yalnızca kendi altındakileri etkiler.
    public class TorchFlicker : MonoBehaviour
    {
        [Header("Hedef Işıklar")]
        [Tooltip("Açıksa sahnedeki tüm Point Light'lar, kapalıysa yalnızca bu objenin altındakiler titrer.")]
        [SerializeField] private bool affectWholeScene = true;
        [Tooltip("Titremeyecek ışıklar (örneğin şalterin ışığı).")]
        [SerializeField] private List<Light> excludedLights = new List<Light>();

        [Header("Titreme")]
        [Tooltip("Işığın başlangıç parlaklığına göre en fazla ne kadar sapacağı (0.25 = %25).")]
        [Range(0f, 1f)][SerializeField] private float intensityVariation = 0.25f;
        [Range(0f, 0.5f)][SerializeField] private float rangeVariation = 0.08f;
        [SerializeField] private float flickerSpeed = 3f;

        [Header("Hafif Konum Kayması")]
        [Tooltip("Gölgelerin hafifçe oynaması için ışığın yerinde salınma mesafesi.")]
        [SerializeField] private float positionJitter = 0.03f;

        private class FlickerLight
        {
            public Light Light;
            public float BaseIntensity;
            public float BaseRange;
            public Vector3 BaseLocalPosition;
            public float Seed;
        }

        private readonly List<FlickerLight> lights = new List<FlickerLight>();

        private void Awake()
        {
            Light[] candidates = affectWholeScene
                ? FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                : GetComponentsInChildren<Light>(true);

            foreach (Light light in candidates)
            {
                if (light.type != LightType.Point || excludedLights.Contains(light)) continue;

                lights.Add(new FlickerLight
                {
                    Light = light,
                    BaseIntensity = light.intensity,
                    BaseRange = light.range,
                    BaseLocalPosition = light.transform.localPosition,
                    Seed = Random.Range(0f, 1000f)
                });
            }
        }

        private void Update()
        {
            float time = Time.time * flickerSpeed;

            foreach (FlickerLight flicker in lights)
            {
                // İki farklı hızdaki gürültü toplanınca hem yavaş dalgalanma hem ani kıpırtı oluşur.
                float slow = Mathf.PerlinNoise(flicker.Seed, time * 0.5f);
                float fast = Mathf.PerlinNoise(flicker.Seed + 37f, time * 2.3f);
                float noise = (slow * 0.65f + fast * 0.35f) * 2f - 1f; // -1..1

                flicker.Light.intensity = flicker.BaseIntensity * (1f + noise * intensityVariation);
                flicker.Light.range = flicker.BaseRange * (1f + noise * rangeVariation);

                if (positionJitter > 0f)
                {
                    Vector3 offset = new Vector3(
                        Mathf.PerlinNoise(flicker.Seed + 11f, time) - 0.5f,
                        Mathf.PerlinNoise(flicker.Seed + 23f, time) - 0.5f,
                        Mathf.PerlinNoise(flicker.Seed + 51f, time) - 0.5f) * (positionJitter * 2f);
                    flicker.Light.transform.localPosition = flicker.BaseLocalPosition + offset;
                }
            }
        }
    }
}
