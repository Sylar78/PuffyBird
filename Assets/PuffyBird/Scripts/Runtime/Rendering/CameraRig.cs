using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Caméra en perspective légère, de profil (2.5D). Le champ de vision s'adapte au format de
    /// l'écran pour montrer toujours la totalité des 288 × 512 px logiques : un téléphone plus
    /// haut montre plus de ciel et de sol, une tablette plus large montre plus de décor (§21.3).
    /// </summary>
    public sealed class CameraRig
    {
        readonly Camera _camera;
        readonly WorldSpace _space;
        float _lastAspect = -1f;
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
        }

        public Camera Camera => _camera;

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
            float aspect = _camera.aspect;
            if (!Mathf.Approximately(aspect, _lastAspect))
            {
                _lastAspect = aspect;
                float halfHeight = Mathf.Max(_space.HalfHeight, _space.HalfWidth / Mathf.Max(aspect, 0.01f));
                _camera.fieldOfView = 2f * Mathf.Atan(halfHeight / WorldSpace.CameraDistance) * Mathf.Rad2Deg;
            }

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
