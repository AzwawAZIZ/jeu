#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AetherDrift.Core;

namespace AetherDrift.EditorTools
{
    /// <summary>
    /// Outil d'ÉDITEUR (ne fait pas partie du build) — Étape 1.
    ///
    /// Crée en un clic la scène de démarrage :
    ///   1. une scène vide,
    ///   2. un GameObject "Bootstrapper" avec GameBootstrapper.cs,
    ///   3. sauvegarde dans Assets/_Project/Scenes/Boot.unity,
    ///   4. l'ajoute aux scenes de build (elle se lancera en premier).
    ///
    /// Menu : Aether Drift ▸ Créer la scène de boot (Étape 1)
    /// </summary>
    public static class BootSceneCreator
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";
        private const string ScenePath = ScenesFolder + "/Boot.unity";
        private const string BootstrapperName = "Bootstrapper";

        [MenuItem("Aether Drift/Créer la scène de boot (Étape 1)", priority = 0)]
        public static void CreateBootScene()
        {
            // 1. S'assurer que le dossier des scènes existe.
            if (!Directory.Exists(ScenesFolder))
            {
                Directory.CreateDirectory(ScenesFolder);
                AssetDatabase.Refresh();
            }

            // 2. Scène vide (sans caméra ni lumière : tout est généré par le Bootstrapper).
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. GameObject Bootstrapper + composant.
            GameObject bootstrapper = new GameObject(BootstrapperName);
            bootstrapper.AddComponent<GameBootstrapper>();

            // 4. Sauvegarde.
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                Debug.LogError("[AETHER DRIFT] Impossible de sauvegarder la scène dans " + ScenePath);
                return;
            }

            // 5. Scène ajoutée (et placée en premier) dans les Build Settings.
            AddSceneToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AETHER DRIFT] Scène de boot créée : " + ScenePath + " → appuie sur ▶ Play.");
            EditorUtility.DisplayDialog(
                "AETHER DRIFT — Étape 1",
                "Scène « Boot.unity » créée et ajoutée aux Build Settings.\n\nAppuie sur ▶ Play : le monde se génère automatiquement.",
                "C'est parti");
        }

        private static void AddSceneToBuildSettings()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;

            // Déjà présente ? On la remonte en premier et on s'arrête là.
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path == ScenePath)
                {
                    EditorBuildSettingsScene[] reordered = new EditorBuildSettingsScene[existing.Length];
                    reordered[0] = new EditorBuildSettingsScene(ScenePath, true);
                    int write = 1;
                    for (int j = 0; j < existing.Length; j++)
                    {
                        if (existing[j].path == ScenePath)
                            continue;
                        reordered[write++] = existing[j];
                    }
                    EditorBuildSettings.scenes = reordered;
                    return;
                }
            }

            // Sinon : on l'insère en tête.
            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[existing.Length + 1];
            updated[0] = new EditorBuildSettingsScene(ScenePath, true);
            for (int i = 0; i < existing.Length; i++)
                updated[i + 1] = existing[i];
            EditorBuildSettings.scenes = updated;
        }
    }
}
#endif
