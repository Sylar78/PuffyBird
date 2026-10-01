using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PuffyBird.Rendering
{
    /// <summary>Niveau de qualité graphique (réglage QUALITY), choisi automatiquement au premier lancement.</summary>
    public enum GraphicsLevel
    {
        /// <summary>Sans ombres ni bloom, vignette ni flou du lointain ; rendu à 80 % ; sans anticrénelage.</summary>
        Low,
        /// <summary>Ombres nettes, bloom et vignette, sans flou du lointain ; rendu à 90 % ; anticrénelage 2×.</summary>
        Medium,
        /// <summary>Tout : ombres douces, bloom, vignette, profondeur de champ ; anticrénelage 4×.</summary>
        High,
    }

    /// <summary>
    /// Applique le niveau de qualité : ombres du soleil (<see cref="LightingRig"/>), effets de
    /// post-traitement (<see cref="PostFxController"/>, le fondu et le flash restent toujours
    /// actifs) et, dans les builds, échelle de rendu et anticrénelage de l'asset URP (dans
    /// l'éditeur, l'asset n'est pas modifié pour ne pas toucher au fichier du projet).
    /// </summary>
    public static class GraphicsQuality
    {
        public const int Count = 3;

        /// <summary>Niveau conseillé selon la mémoire de l'appareil (les téléphones modestes en ont peu).</summary>
        public static GraphicsLevel Auto()
        {
            int memoryMb = SystemInfo.systemMemorySize;
            if (memoryMb > 0 && memoryMb < 3500) return GraphicsLevel.Low;
            if (memoryMb > 0 && memoryMb < 5500) return GraphicsLevel.Medium;
            return GraphicsLevel.High;
        }

        public static void Apply(GraphicsLevel level, LightingRig lighting, PostFxController postFx)
        {
            lighting.SetQuality(level);
            postFx.SetQuality(level);
#if !UNITY_EDITOR
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = level == GraphicsLevel.Low ? 0.8f : (level == GraphicsLevel.Medium ? 0.9f : 1f);
                urp.msaaSampleCount = level == GraphicsLevel.Low ? 1 : (level == GraphicsLevel.Medium ? 2 : 4);
            }
#endif
        }
    }
}
