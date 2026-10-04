using UnityEditor;
using UnityEditor.CrashReporting;
using UnityEngine;

namespace PuffyBird.Editor
{
    /// <summary>
    /// Projet Unity Cloud du jeu (cloud.unity.com), nécessaire à Unity Analytics, à Unity IAP
    /// (rapports) et au rapport de plantages (Diagnostics). <c>ProjectSettings/</c> n'est pas
    /// versionné : la liaison faite dans l'éditeur (Edit > Project Settings > Services) ne
    /// suit pas dans les builds de la CI, d'où ces identifiants recopiés dans le code et appliqués
    /// par <see cref="ProjectSetup.Configure"/> avant chaque build. Valeurs publiques (ni clé ni
    /// secret) ; vides : pas de mesure d'audience ni de rapport de plantages. Mise en place :
    /// <c>docs/mesure-audience.md</c>.
    /// </summary>
    public static class UnityCloudProject
    {
        /// <summary>ID du projet (cloud.unity.com > Projects > PuffyBird > Project ID, de la forme d'un GUID).</summary>
        public const string ProjectId = "";

        /// <summary>ID de l'organisation (cloud.unity.com > Administration > Organization ID).</summary>
        public const string OrganizationId = "";

        public const string ProjectName = "PuffyBird";

        public static bool Configured => ProjectId.Length > 0;

        /// <summary>
        /// Relie le projet à Unity Cloud s'il ne l'est pas déjà (sans toucher à une liaison faite à la
        /// main dans l'éditeur) et active le rapport de plantages.
        /// </summary>
        public static void Apply()
        {
            if (!Configured)
            {
                if (string.IsNullOrEmpty(CloudProjectSettings.projectId))
                    Debug.Log("PuffyBird : projet Unity Cloud non renseigné (UnityCloudProject), pas de mesure d'audience ni de rapport de plantages.");
                else
                    CrashReportingSettings.enabled = true;
                return;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("PuffyBird : ProjectSettings.asset illisible, liaison Unity Cloud impossible.");
                return;
            }
            var so = new SerializedObject(assets[0]);
            bool changed = SetString(so, "cloudProjectId", ProjectId);
            if (OrganizationId.Length > 0) changed |= SetString(so, "organizationId", OrganizationId);
            changed |= SetString(so, "projectName", ProjectName);
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"PuffyBird : projet relié à Unity Cloud ({ProjectId}).");
            }

            // Plantages et exceptions envoyés à Unity Cloud > Diagnostics ; jamais ceux de l'éditeur.
            CrashReportingSettings.enabled = true;
            CrashReportingSettings.captureEditorExceptions = false;
        }

        static bool SetString(SerializedObject so, string name, string value)
        {
            var property = so.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning($"PuffyBird : réglage « {name} » introuvable dans ProjectSettings.asset.");
                return false;
            }
            if (property.stringValue == value) return false;
            property.stringValue = value;
            return true;
        }
    }
}
