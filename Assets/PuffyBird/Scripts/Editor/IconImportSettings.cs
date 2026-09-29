using UnityEditor;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Réglages d'import de l'icône : l'App Store refuse une icône qui contient un canal alpha,
    /// et Unity doit disposer de l'image pleine résolution pour générer toutes les tailles.
    /// </summary>
    sealed class IconImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath != ProjectSetup.IconPath) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
