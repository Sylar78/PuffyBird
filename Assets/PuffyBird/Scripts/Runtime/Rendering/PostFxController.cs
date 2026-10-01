using PuffyBird.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Post-traitement URP (Volume global créé par code) : bloom, tonemapping, étalonnage
    /// jour/nuit, vignette, profondeur de champ sur le décor lointain. Porte aussi le flash
    /// blanc d'impact et le fondu au noir des transitions (§11.5), par l'exposition.
    /// </summary>
    public sealed class PostFxController
    {
        const float FlashExposure = 2.4f;
        const float LightningExposure = 0.7f;
        const float FadeExposure = -10f;

        readonly Bloom _bloom;
        readonly ColorAdjustments _color;
        readonly Vignette _vignette;
        readonly DepthOfField _dof;
        float _baseExposure;

        public PostFxController(Transform parent)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "PuffyBird Post-traitement";

            _bloom = profile.Add<Bloom>(true);
            _bloom.scatter.value = 0.65f;
            _bloom.highQualityFiltering.value = false;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;

            _color = profile.Add<ColorAdjustments>(true);
            _color.contrast.value = 8f;
            _color.saturation.value = 12f;

            _vignette = profile.Add<Vignette>(true);
            _vignette.smoothness.value = 0.45f;
            _vignette.color.value = Color.black;

            _dof = profile.Add<DepthOfField>(true);
            _dof.mode.value = DepthOfFieldMode.Gaussian;
            _dof.gaussianStart.value = 20f;
            _dof.gaussianEnd.value = 60f;
            _dof.gaussianMaxRadius.value = 1f;
            _dof.highQualitySampling.value = false;

            var go = new GameObject("Post-traitement");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        public void ApplyTheme(Palette.ThemeColors colors)
        {
            _bloom.intensity.value = colors.BloomIntensity;
            _bloom.threshold.value = colors.BloomThreshold;
            _bloom.tint.value = colors.BloomTint;
            _color.colorFilter.value = colors.ColorFilter;
            _vignette.intensity.value = colors.Vignette;
            _baseExposure = colors.Exposure;
        }

        /// <summary>Effets coûteux coupés selon la qualité ; l'exposition (flash, fondu) reste active.</summary>
        public void SetQuality(GraphicsLevel level)
        {
            _bloom.active = level != GraphicsLevel.Low;
            _vignette.active = level != GraphicsLevel.Low;
            _dof.active = level == GraphicsLevel.High;
        }

        /// <param name="flash">0..1, flash blanc d'impact.</param>
        /// <param name="fade">0..1, fondu au noir.</param>
        /// <param name="lightning">0..1, lueur d'un éclair (décor d'orage).</param>
        public void Update(float flash, float fade, float lightning, bool reduceFlash)
        {
            float f = reduceFlash ? flash * 0.25f : flash;
            _color.postExposure.value = _baseExposure + f * FlashExposure + lightning * LightningExposure + fade * FadeExposure;
        }
    }
}
