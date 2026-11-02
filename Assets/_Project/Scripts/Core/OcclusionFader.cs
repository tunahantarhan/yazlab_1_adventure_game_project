using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using AdventureGame.AI;
using AdventureGame.Player;

namespace AdventureGame.Core
{
    // Kamera ile takip edilen karakterler arasına giren duvar/taşları yarı saydam yapar.
    // Main Camera'ya eklenir. URP Lit dışındaki shader'larda obje yalnızca gölge bırakacak şekilde gizlenir.
    public class OcclusionFader : MonoBehaviour
    {
        [Header("Takip Edilenler")]
        [Tooltip("Boş bırakılırsa Player ve Companion otomatik bulunur.")]
        [SerializeField] private List<Transform> targets = new List<Transform>();
        [SerializeField] private float targetHeightOffset = 1f;

        [Header("Algılama")]
        [SerializeField] private LayerMask occluderLayers = ~0;
        [SerializeField] private float castRadius = 0.4f;

        [Header("Saydamlık")]
        [Range(0f, 1f)][SerializeField] private float fadedAlpha = 0.25f;
        [SerializeField] private float fadeSpeed = 4f;

        [Tooltip("Build'de URP'nin transparan shader varyantını silmemesi için referans tutulur (Mat_FadeVariantKeeper).")]
        [SerializeField] private Material transparentVariantKeeper;

        private const string SurfaceProperty = "_Surface";
        private const string BaseColorProperty = "_BaseColor";

        private class FadeState
        {
            public Material[] OriginalMaterials;
            public Material[] FadeMaterials; // null ise shader desteklemiyor, gölge moduna düşülür
            public ShadowCastingMode OriginalShadowMode;
            public float Alpha = 1f;
        }

        private readonly Dictionary<Renderer, FadeState> fadeStates = new Dictionary<Renderer, FadeState>();
        private readonly HashSet<Renderer> currentOccluders = new HashSet<Renderer>();
        private readonly List<Renderer> finishedRenderers = new List<Renderer>();
        private readonly RaycastHit[] hitBuffer = new RaycastHit[32];

        private void Start()
        {
            if (targets.Count == 0)
            {
                var player = FindFirstObjectByType<PlayerController>();
                var companion = FindFirstObjectByType<CompanionController>();
                if (player != null) targets.Add(player.transform);
                if (companion != null) targets.Add(companion.transform);
            }
        }

        private void LateUpdate()
        {
            CollectOccluders();

            foreach (var occluder in currentOccluders)
            {
                if (!fadeStates.ContainsKey(occluder))
                {
                    fadeStates[occluder] = CreateFadeState(occluder);
                }
            }

            finishedRenderers.Clear();
            foreach (var pair in fadeStates)
            {
                var renderer = pair.Key;
                var state = pair.Value;

                if (renderer == null)
                {
                    finishedRenderers.Add(renderer);
                    continue;
                }

                bool isOccluding = currentOccluders.Contains(renderer);
                float targetAlpha = isOccluding ? fadedAlpha : 1f;
                state.Alpha = Mathf.MoveTowards(state.Alpha, targetAlpha, fadeSpeed * Time.deltaTime);
                ApplyAlpha(renderer, state);

                if (!isOccluding && state.Alpha >= 1f)
                {
                    finishedRenderers.Add(renderer);
                }
            }

            foreach (var renderer in finishedRenderers)
            {
                RestoreRenderer(renderer);
            }
        }

        private void OnDisable()
        {
            // Sahneden çıkarken oluşturulan geçici materyaller temizlenir
            foreach (var renderer in new List<Renderer>(fadeStates.Keys))
            {
                RestoreRenderer(renderer);
            }
        }

        private void CollectOccluders()
        {
            currentOccluders.Clear();
            Vector3 cameraPosition = transform.position;

            foreach (var target in targets)
            {
                if (target == null) continue;

                Vector3 targetPoint = target.position + Vector3.up * targetHeightOffset;
                Vector3 toTarget = targetPoint - cameraPosition;
                float distance = toTarget.magnitude;
                if (distance <= castRadius) continue;

                int hitCount = Physics.SphereCastNonAlloc(cameraPosition, castRadius, toTarget / distance, hitBuffer,
                    distance - castRadius, occluderLayers, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < hitCount; i++)
                {
                    var hitTransform = hitBuffer[i].collider.transform;
                    if (IsTarget(hitTransform)) continue;

                    var renderer = hitBuffer[i].collider.GetComponent<Renderer>();
                    if (renderer != null) currentOccluders.Add(renderer);
                }
            }
        }

        private bool IsTarget(Transform candidate)
        {
            foreach (var target in targets)
            {
                if (target != null && candidate.IsChildOf(target)) return true;
            }
            return false;
        }

        private FadeState CreateFadeState(Renderer renderer)
        {
            var state = new FadeState
            {
                OriginalMaterials = renderer.sharedMaterials,
                OriginalShadowMode = renderer.shadowCastingMode
            };

            if (!SupportsFade(state.OriginalMaterials)) return state;

            state.FadeMaterials = new Material[state.OriginalMaterials.Length];
            for (int i = 0; i < state.OriginalMaterials.Length; i++)
            {
                state.FadeMaterials[i] = new Material(state.OriginalMaterials[i]);
                MakeTransparent(state.FadeMaterials[i]);
            }
            renderer.sharedMaterials = state.FadeMaterials;
            return state;
        }

        private static bool SupportsFade(Material[] materials)
        {
            foreach (var material in materials)
            {
                if (material == null || !material.HasProperty(SurfaceProperty) || !material.HasProperty(BaseColorProperty))
                {
                    return false;
                }
            }
            return true;
        }

        // URP Lit materyalini çalışma anında Transparent yüzey tipine çevirir
        private static void MakeTransparent(Material material)
        {
            material.SetFloat(SurfaceProperty, 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void ApplyAlpha(Renderer renderer, FadeState state)
        {
            if (state.FadeMaterials == null)
            {
                // Desteklenmeyen shader: yarı yoldan sonra obje gizlenir, gölgesi kalır
                renderer.shadowCastingMode = state.Alpha < 0.6f ? ShadowCastingMode.ShadowsOnly : state.OriginalShadowMode;
                return;
            }

            foreach (var material in state.FadeMaterials)
            {
                Color color = material.GetColor(BaseColorProperty);
                color.a = state.Alpha;
                material.SetColor(BaseColorProperty, color);
            }
        }

        private void RestoreRenderer(Renderer renderer)
        {
            if (!fadeStates.TryGetValue(renderer, out var state)) return;

            if (renderer != null)
            {
                renderer.sharedMaterials = state.OriginalMaterials;
                renderer.shadowCastingMode = state.OriginalShadowMode;
            }

            if (state.FadeMaterials != null)
            {
                foreach (var material in state.FadeMaterials)
                {
                    Destroy(material);
                }
            }

            fadeStates.Remove(renderer);
        }
    }
}
