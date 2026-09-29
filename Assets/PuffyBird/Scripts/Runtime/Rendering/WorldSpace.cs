using PuffyBird.Core;
using UnityEngine;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Passage du repère logique de la spec (288 × 512 px, y vers le bas) au monde Unity
    /// (1 unité = 100 px, y vers le haut). Le sol (y logique 400) est à y = 0 et le plan de jeu
    /// à z = 0 ; la caméra regarde vers +z.
    /// </summary>
    public sealed class WorldSpace
    {
        public const float PixelsPerUnit = 100f;
        /// <summary>Distance entre la caméra et le plan de jeu.</summary>
        public const float CameraDistance = 8f;

        readonly GameConfig _cfg;

        public WorldSpace(GameConfig cfg)
        {
            _cfg = cfg;
        }

        public GameConfig Config => _cfg;

        public float X(float px) => (px - _cfg.Width * 0.5f) / PixelsPerUnit;

        public float Y(float py) => (_cfg.GroundY - py) / PixelsPerUnit;

        public static float Length(float px) => px / PixelsPerUnit;

        public Vector3 ToWorld(float px, float py, float z = 0f) => new Vector3(X(px), Y(py), z);

        /// <summary>Position de la caméra : centrée sur l'écran logique, devant le plan de jeu.</summary>
        public Vector3 CameraPosition => new Vector3(0f, Y(_cfg.Height * 0.5f), -CameraDistance);

        /// <summary>Demi-hauteur et demi-largeur de l'écran logique dans le plan de jeu.</summary>
        public float HalfHeight => _cfg.Height * 0.5f / PixelsPerUnit;
        public float HalfWidth => _cfg.Width * 0.5f / PixelsPerUnit;

        /// <summary>
        /// Point à la profondeur <paramref name="z"/> qui apparaît à l'écran exactement à la position
        /// logique (px, py). <paramref name="scale"/> est le facteur à appliquer aux tailles pour
        /// qu'elles gardent leur taille apparente en pixels logiques.
        /// </summary>
        public Vector3 OnScreen(float px, float py, float z, out float scale)
        {
            var cam = CameraPosition;
            var onPlane = ToWorld(px, py, 0f);
            scale = (z - cam.z) / (0f - cam.z);
            return cam + (onPlane - cam) * scale;
        }
    }
}
