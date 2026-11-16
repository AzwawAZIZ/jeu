using UnityEngine;
using AetherDrift.Player;
using AetherDrift.World;

namespace AetherDrift.Core
{
    /// <summary>
    /// Point d'entrée procédural du jeu — ÉTAPE 1.
    ///
    /// Au lancement (Awake) ce script assemble TOUTE la scène par code :
    /// aucun asset, aucune scène préparée à la main.
    ///
    ///   Sol facetté + anneau d'arène + cercle runique   (World/ArenaEnvironment)
    ///   Ruines : piliers, tambours, débris              (World/ArenaEnvironment)
    ///   Montagnes d'horizon + dôme de ciel dégradé      (World/ArenaEnvironment)
    ///   Véhicule runique low-poly                       (Player/PlayerVehicle)
    ///   Caméra chase avec zoom dynamique                (Player/CameraFollow)
    ///   Soleil bas + brouillard + ambiance              (ce fichier)
    ///
    /// UTILISATION : appuie simplement sur ▶ Play. Si la scène ne contient
    /// aucun Bootstrapper, il s'en crée un automatiquement (voir
    /// EnsureBootstrapperExists) — le menu Aether Drift reste disponible
    /// pour créer explicitement la scène Boot.unity.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Sol / Arène")]
        [Tooltip("Diamètre du sol praticable, en mètres.")]
        [SerializeField] private float groundDiameter = 220f;

        [Tooltip("Rayon de l'anneau de pierre et du cercle runique.")]
        [SerializeField] private float arenaRadius = 26f;

        [Header("Décor")]
        [Tooltip("Nombre de piliers de ruines autour de l'arène.")]
        [SerializeField] private int ruinPillarCount = 14;

        [Tooltip("Nombre de montagnes à l'horizon.")]
        [SerializeField] private int mountainCount = 26;

        [Header("Véhicule")]
        [Tooltip("Position d'apparition (y ≈ 0.5 : il se pose sur le sol).")]
        [SerializeField] private Vector3 playerSpawnPosition = new Vector3(0f, 0.5f, 0f);

        [Header("Caméra")]
        [Tooltip("Couleur de fond si aucun ciel n'est visible.")]
        [SerializeField] private Color skyColor = new Color(0.08f, 0.10f, 0.16f);

        [Header("Lumière / Ambiance")]
        [Tooltip("Intensité du soleil.")]
        [SerializeField] private float lightIntensity = 1.25f;

        [Tooltip("Couleur du soleil (aube froide et dorée).")]
        [SerializeField] private Color lightColor = new Color(1f, 0.94f, 0.86f);

        [Tooltip("Rotation du soleil : x = hauteur, y = azimut.")]
        [SerializeField] private Vector2 sunAngles = new Vector2(18f, -35f);

        [Tooltip("Couleur du brouillard (doit s'accorder au ciel).")]
        [SerializeField] private Color fogColor = new Color(0.26f, 0.30f, 0.38f);

        [Tooltip("Densité du brouillard exponentiel.")]
        [SerializeField] private float fogDensity = 0.008f;

        private Transform playerTransform;
        private GameObject worldRoot;

        // ============================================================
        // DÉMARRAGE
        // ============================================================

        /// <summary>
        /// Filet de sécurité : si la scène ne contient AUCUN GameBootstrapper,
        /// on en crée un au lancement du Play. Appuyer sur ▶ suffit toujours.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapperExists()
        {
            if (FindObjectOfType<GameBootstrapper>() != null)
                return;

            GameObject auto = new GameObject("Bootstrapper (auto)");
            auto.AddComponent<GameBootstrapper>();
            Debug.Log("[AETHER DRIFT] Aucun Bootstrapper dans la scène : création automatique.");
        }

        private void Awake()
        {
            RemoveDefaultSceneObjects();

            worldRoot = new GameObject("World (généré)");

            BuildGround();
            BuildArena();
            BuildRuins();
            BuildHorizon();
            BuildSky();
            BuildPlayer();
            BuildCamera();
            BuildSun();
            ConfigureAmbiance();

            LogDiagnostics();
            Debug.Log("[AETHER DRIFT] Bootstrapper : scène générée procéduralement (Étape 1).");
        }

        // ============================================================
        // NETTOYAGE
        // ============================================================

        /// <summary>
        /// Supprime caméras, lumières et AudioListener déjà présents
        /// (scène "Basic" d'Unity, ou ancien Play). Le Bootstrapper est
        /// la seule source de vérité du contenu de la scène.
        /// </summary>
        private void RemoveDefaultSceneObjects()
        {
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in cameras)
            {
                if (cam != null && cam.gameObject != gameObject)
                {
                    cam.gameObject.SetActive(false);
                    Destroy(cam.gameObject);
                }
            }

            Light[] lights = FindObjectsOfType<Light>();
            foreach (Light l in lights)
            {
                if (l != null && l.gameObject != gameObject)
                {
                    l.gameObject.SetActive(false);
                    Destroy(l.gameObject);
                }
            }

            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
            foreach (AudioListener listener in listeners)
            {
                if (listener != null && listener.gameObject != gameObject)
                    Destroy(listener);
            }
        }

        // ============================================================
        // MONDE
        // ============================================================

        private void BuildGround()
        {
            ArenaEnvironment.CreateGround(groundDiameter, worldRoot.transform);
        }

        private void BuildArena()
        {
            ArenaEnvironment.CreateArenaRing(arenaRadius, worldRoot.transform);
        }

        private void BuildRuins()
        {
            ArenaEnvironment.CreateRuins(arenaRadius, worldRoot.transform, ruinPillarCount);
        }

        private void BuildHorizon()
        {
            ArenaEnvironment.CreateDistantMountains(groundDiameter * 0.45f, worldRoot.transform, mountainCount);
        }

        private void BuildSky()
        {
            ArenaEnvironment.CreateSkyDome(groundDiameter * 3.2f, worldRoot.transform);
        }

        // ============================================================
        // VÉHICULE + CAMÉRA
        // ============================================================

        private void BuildPlayer()
        {
            GameObject player = new GameObject("PlayerVehicle");
            player.tag = "Player";
            player.transform.position = playerSpawnPosition;
            player.transform.rotation = Quaternion.identity;

            // --- Ordre d'ajout IMPORTANT ---
            // 1. Rigidbody : requis par PlayerVehicle et CarController.
            Rigidbody rb = player.AddComponent<Rigidbody>();
            rb.mass = 1200f;

            // 2. PlayerStats : ressources (éther, endurance, i-frames).
            //    Ajouté avant CarController, qui en dépend ([RequireComponent]).
            player.AddComponent<PlayerStats>();

            // 3. PlayerVehicle : construit la sphère physique et le châssis low-poly.
            player.AddComponent<PlayerVehicle>();

            // 4. CarController : la conduite. Son Start() récupère le châssis
            //    construit par PlayerVehicle.Awake().
            player.AddComponent<CarController>();

            playerTransform = player.transform;
        }

        private void BuildCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            Camera cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = skyColor;
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 900f;

            CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
            if (playerTransform != null)
                follow.SetTarget(playerTransform);

            Vector3 startOffset = new Vector3(0f, 5.5f, -9.5f);
            Vector3 startPosition = (playerTransform != null ? playerTransform.position : Vector3.zero) + startOffset;
            cameraObject.transform.position = startPosition;
            if (playerTransform != null)
                cameraObject.transform.LookAt(playerTransform.position + new Vector3(0f, 1.2f, 0f));
        }

        // ============================================================
        // LUMIÈRE / AMBIANCE
        // ============================================================

        private void BuildSun()
        {
            GameObject lightObject = new GameObject("Directional Light (Soleil)");

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = lightColor;
            light.intensity = lightIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
            light.shadowStrength = 0.85f;

            lightObject.transform.rotation = Quaternion.Euler(sunAngles.x, sunAngles.y, 0f);
        }

        /// <summary>
        /// Brouillard + ambiance froide : donne de la profondeur à la vallée
        /// et fait ressortir les néons cyan du véhicule.
        /// </summary>
        private void ConfigureAmbiance()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.24f, 0.33f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.16f, 0.21f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.06f, 0.08f);
        }

        // ============================================================
        // DIAGNOSTIC (Éditeur uniquement)
        // ============================================================

        /// <summary>
        /// Affiche version d'Unity, pipeline et shader retenu.
        /// Utile si le rendu apparaît rose (shader manquant) ou noir.
        /// </summary>
        private void LogDiagnostics()
        {
#if UNITY_EDITOR
            Debug.Log("[AETHER DRIFT] Unity " + Application.unityVersion
                + " | pipeline : " + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                    ? "Built-in"
                    : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name)
                + " | shader lit : " + (ProceduralMaterials.LitShader != null
                    ? ProceduralMaterials.LitShader.name
                    : "AUCUN"));
#endif
        }
    }
}
