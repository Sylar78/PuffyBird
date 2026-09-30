using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Caméra en perspective légère, de profil (2.5D). Le champ de vision s'adapte au format de
    /// l'écran pour montrer toujours la totalité des 288 × 512 px logiques : un téléphone plus
    /// haut montre plus de ciel et de sol, une tablette plus large montre plus de décor (§21.3,
    /// option « extension »). Au-delà du 3:4, l'image est bordée de bandes (option « letterbox ») :
    /// le décor et les tuyaux ne sont prévus que jusqu'à cette largeur.
    /// </summary>
    public sealed class CameraRig
    {
        /// <summary>Format le plus large affiché sans bandes (largeur / hauteur) : tablette 3:4.</summary>
        public const float MaxAspect = 0.75f;
        /// <summary>
        /// Marge en px ajoutée à la largeur visible : la face avant des tuyaux, plus proche de la
        /// caméra, déborde un peu en perspective, et la secousse d'impact décale la vue.
        /// </summary>
        const float SafetyMarginPx = 8f;

        readonly Camera _camera;
        readonly Camera _bars;
        readonly WorldSpace _space;
        float _lastScreenAspect = -1f;
        Rect _lastSafeArea;
        float _shakeTime;
        float _shakeDuration;
        float _shakeAmplitude;

        public CameraRig(Camera camera, WorldSpace space)
        {
            _camera = camera;
            _space = space;
            _camera.orthographic = false;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 220f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.allowHDR = true;
            _camera.transform.SetPositionAndRotation(space.CameraPosition, Quaternion.identity);

            var data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.None;

            // Caméra de fond qui ne rend rien et peint les bandes : en URP, la zone hors du
            // viewport de la caméra principale n'est sinon jamais effacée.
            var barsGo = new GameObject("Bandes");
            _bars = barsGo.AddComponent<Camera>();
            _bars.depth = _camera.depth - 1f;
            _bars.cullingMask = 0;
            _bars.clearFlags = CameraClearFlags.SolidColor;
            _bars.backgroundColor = Color.black;
            _bars.enabled = false;
            var barsData = _bars.GetUniversalAdditionalCameraData();
            barsData.renderPostProcessing = false;
            barsData.renderShadows = false;
        }

        public Camera Camera => _camera;

        /// <summary>
        /// Largeur visible au-delà de l'écran logique, de chaque côté, en px logiques : les tuyaux
        /// apparaissent et disparaissent au-delà (voir <c>GameSimulation.ViewMargin</c>).
        /// </summary>
        public float SideMarginPx { get; private set; }

        /// <summary>
        /// Haut de la zone sûre de l'écran (sous l'encoche ou la barre d'état), en y logique dans le
        /// plan de jeu. Négatif sur un téléphone plus haut que 9:16, qui montre plus de ciel.
        /// </summary>
        public float SafeTopPx { get; private set; }

        /// <summary>
        /// Adapte le viewport (bandes au-delà du 3:4) et le champ de vision au format de l'écran.
        /// À appeler avant d'avancer la simulation, qui a besoin de <see cref="SideMarginPx"/>.
        /// </summary>
        public void UpdateViewport()
        {
            float screenAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : MaxAspect;
            var safeArea = Screen.safeArea;
            if (Mathf.Approximately(screenAspect, _lastScreenAspect) && safeArea == _lastSafeArea) return;
            _lastScreenAspect = screenAspect;
            _lastSafeArea = safeArea;

            float aspect = Mathf.Min(screenAspect, MaxAspect);
            bool bars = screenAspect > MaxAspect;
            float width = bars ? MaxAspect / screenAspect : 1f;
            _camera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            _bars.enabled = bars;

            float halfHeight = Mathf.Max(_space.HalfHeight, _space.HalfWidth / Mathf.Max(aspect, 0.01f));
            _camera.fieldOfView = 2f * Mathf.Atan(halfHeight / WorldSpace.CameraDistance) * Mathf.Rad2Deg;
            float visibleHalfWidth = halfHeight * aspect;
            SideMarginPx = Mathf.Max(0f, visibleHalfWidth - _space.HalfWidth) * WorldSpace.PixelsPerUnit + SafetyMarginPx;

            float visibleHeightPx = halfHeight * 2f * WorldSpace.PixelsPerUnit;
            float topInset = Screen.height > 0 ? Mathf.Clamp01((Screen.height - safeArea.yMax) / Screen.height) : 0f;
            SafeTopPx = _space.Config.Height * 0.5f - visibleHeightPx * 0.5f + topInset * visibleHeightPx;
        }

        /// <summary>Demi-largeur visible à la profondeur z, pour le format le plus large géré.</summary>
        public static float HalfWidthAt(float z, float verticalFov, float aspect)
        {
            float halfV = Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad);
            return (z + WorldSpace.CameraDistance) * halfV * aspect;
        }

        public void Shake(float amplitude, float duration)
        {
            _shakeAmplitude = amplitude;
            _shakeDuration = duration;
            _shakeTime = duration;
        }

        public void Update(float deltaTime, bool reduceMotion)
        {
            UpdateViewport();

            var position = _space.CameraPosition;
            if (_shakeTime > 0f && !reduceMotion)
            {
                _shakeTime = Mathf.Max(0f, _shakeTime - deltaTime);
                float k = _shakeTime / _shakeDuration;
                position += (Vector3)(Random.insideUnitCircle * (_shakeAmplitude * k));
            }
            _camera.transform.position = position;
        }
    }
}
