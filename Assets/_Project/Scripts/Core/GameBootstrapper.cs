using System.Collections.Generic;
using UnityEngine;
using AetherDrift.Player;
using AetherDrift.World;
using AetherDrift.Enemies;

namespace AetherDrift.Core
{
    /// <summary>
    /// Point d'entrée procédural du jeu — ÉTAPES 1 à 3.
    ///
    /// Au lancement (Awake) ce script assemble TOUTE la scène par code :
    /// sol, monde ouvert en trois zones, tours de synchronisation, véhicule,
    /// caméra, contrats et chasseurs. Aucune scène préparée à la main.
    ///
    /// En cas de souci, décoche « Open World Enabled » dans l'Inspector :
    /// le jeu revient à la petite arène de l'Étape 1.
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
        [Header("Monde ouvert (Étape 3)")]
        [Tooltip("Activer le monde ouvert généré (zones, tours, contrats). " +
                 "Désactiver pour revenir à la petite arène de l'Étape 1.")]
        [SerializeField] private bool openWorldEnabled = true;

        [Tooltip("Demi-taille de la carte, en mètres (600 = 1,2 km de côté).")]
        [SerializeField] private float worldHalfSize = 600f;

        [Tooltip("Nombre de tours de synchronisation.")]
        [SerializeField] private int syncTowerCount = 3;

        [Tooltip("Nombre de contrats de livraison.")]
        [SerializeField] private int contractCount = 3;

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

        // Étape 3
        private WorldGenerator world;
        private ContractManager contractManager;
        private readonly List<SyncTower> towers = new List<SyncTower>();
        private int synchronizedTowerCount;
        private DescentHelper descentHelper;

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

            if (openWorldEnabled)
            {
                // --- Monde ouvert (Étape 3) ---
                // L'ordre n'est pas décoratif :
                //   sol → monde (tours et contrats réservent leurs places AVANT
                //   la génération du décor) → tours → horizon → ciel → joueur
                //   → caméra → soleil → ambiance → balises de contrats (elles
                //   ont besoin du joueur pour les tests de distance).
                BuildVastGround();
                BuildWorld();
                BuildSyncTowers();
                BuildHorizon();
                BuildSky();
                BuildPlayer();
                BuildCamera();
                BuildSun();
                ConfigureAmbiance();
                BuildContracts();
            }
            else
            {
                // --- Arène de l'Étape 1 (mode réduit) ---
                BuildGround();
                BuildArena();
                BuildRuins();
                BuildHorizon();
                BuildSky();
                BuildPlayer();
                BuildCamera();
                BuildSun();
                ConfigureAmbiance();
            }

            LogDiagnostics();
            LogOpenWorldSummary();
            Debug.Log("[AETHER DRIFT] Bootstrapper : monde généré procéduralement"
                + (openWorldEnabled ? " (Étape 3 — monde ouvert)." : " (Étape 1 — arène)."));
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
            // Monde ouvert : les montagnes doivent rester DERRIÈRE la limite de
            // la carte (sinon elles poussent au milieu du terrain praticable).
            float radius = openWorldEnabled ? worldHalfSize * 1.25f : groundDiameter * 0.45f;
            ArenaEnvironment.CreateDistantMountains(radius, worldRoot.transform, mountainCount);
        }

        private void BuildSky()
        {
            float radius = openWorldEnabled ? worldHalfSize * 2.6f : groundDiameter * 3.2f;
            ArenaEnvironment.CreateSkyDome(radius, worldRoot.transform);
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
            // Monde ouvert : le brouillard cache la limite de la carte bien
            // avant la distance d'affichage maximale (évite de voir "le vide").
            cam.farClipPlane = openWorldEnabled ? 1600f : 900f;

            CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
            if (playerTransform != null)
                follow.SetTarget(playerTransform);

            // Placement de départ : derrière et au-dessus du véhicule.
            // CameraFollow recalcule cette position dès sa première image
            // (orbite à 360°), ceci évite juste un saut visible au lancement.
            Vector3 startOffset = new Vector3(0f, 4.2f, -10f);
            Vector3 startPosition = (playerTransform != null ? playerTransform.position : Vector3.zero) + startOffset;
            cameraObject.transform.position = startPosition;
            if (playerTransform != null)
                cameraObject.transform.LookAt(playerTransform.position + new Vector3(0f, 1.4f, 0f));
        }

        // ============================================================
        // ÉTAPE 3 — MONDE OUVERT
        // ============================================================

        /// <summary>Sol immense (1,2 km) avec texture répétée et collider.</summary>
        private void BuildVastGround()
        {
            ArenaEnvironment.CreateVastGround(worldHalfSize * 2f, 24, worldRoot.transform);
        }

        /// <summary>Génère les trois zones (ruines, rampes, chemins) et les rochers.</summary>
        private void BuildWorld()
        {
            GameObject worldObject = new GameObject("OpenWorld");
            worldObject.transform.SetParent(worldRoot.transform, false);

            world = worldObject.AddComponent<WorldGenerator>();

            // ORDRE CRITIQUE :
            //  1. les tours réservent leur emplacement,
            //  2. les contrats choisissent leurs points de prise et de livraison,
            //  3. SEULEMENT ENSUITE on génère le décor.
            // Sans ça, un pilier peut pousser dans la rampe d'une tour ou dans
            // une balise de contrat : le joueur se retrouve bloqué.
            ReserveTowerSpots();
            PlanContracts();

            world.Generate(worldObject.transform);
        }

        /// <summary>Emplacements des tours : répartis autour du centre, en triangle.</summary>
        private void ReserveTowerSpots()
        {
            for (int i = 0; i < syncTowerCount; i++)
            {
                Vector3 position = GetTowerPosition(i);
                world.ReserveSpot(position, 26f);
            }
        }

        /// <summary>
        /// Position d'une tour : triangle autour du cœur des ruines.
        /// L'angle de départ est 45° et non 0°/90°/180°/270° : à ces angles
        /// passent les quatre grands chemins, et une tour au milieu d'une route
        /// serait un obstacle absurde.
        /// </summary>
        private Vector3 GetTowerPosition(int index)
        {
            float angle = 45f + (360f / Mathf.Max(1, syncTowerCount)) * index;
            float radius = 150f;

            float rad = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
        }

        /// <summary>Crée les tours de synchronisation et écoute leur événement.</summary>
        private void BuildSyncTowers()
        {
            GameObject towersRoot = new GameObject("SyncTowers");
            towersRoot.transform.SetParent(worldRoot.transform, false);

            string[] names = { "Tour de l'Aube", "Tour du Gouffre", "Tour des Cendres" };

            for (int i = 0; i < syncTowerCount; i++)
            {
                Vector3 position = GetTowerPosition(i);
                string towerName = i < names.Length ? names[i] : "Tour " + (i + 1);

                SyncTower tower = SyncTower.Create(
                    towersRoot.transform,
                    position,
                    towerName,
                    42f + i * 4f,                                   // hauteur croissante
                    new Color(0.16f, 0.35f, 0.45f),                 // dormante
                    new Color(1f, 0.72f, 0.25f));                   // synchronisée

                tower.OnSynchronized += HandleTowerSynchronized;
                tower.OnDescentRequested += HandleDescentRequested;
                towers.Add(tower);
            }
        }

        /// <summary>
        /// Réaction à la synchronisation d'une tour : révèle boss et contrats.
        /// L'affichage réel arrive avec la mini-map (Étape 5) ; ici on journalise
        /// et on confirme visuellement au joueur.
        /// </summary>
        private void HandleTowerSynchronized(SyncTower tower)
        {
            synchronizedTowerCount++;

            // Le joueur doit comprendre que la carte vient de s'ouvrir : sans
            // retour immédiat, la synchronisation passe inaperçue.
            PlayerStats stats = FindPlayerStats();
            if (stats != null)
                stats.TriggerInvulnerability(1.5f);

            Debug.Log("[AETHER DRIFT] Progression : " + synchronizedTowerCount + "/" + towers.Count
                + " tours synchronisées. Les icônes de boss et de contrats sont révélées sur la carte.");

            if (contractManager != null)
            {
                Debug.Log("[AETHER DRIFT] Contrats visibles : " + contractManager.Contracts.Count
                    + " (dont " + CountAvailableContracts() + " disponibles).");
            }
        }

        /// <summary>Récupère les jauges du joueur (créées dans BuildPlayer).</summary>
        private PlayerStats FindPlayerStats()
        {
            if (playerTransform == null)
                return null;

            return playerTransform.GetComponent<PlayerStats>();
        }

        /// <summary>
        /// Le joueur demande à redescendre d'une tour : on lui fournit
        /// l'itinéraire et on lance la descente guidée.
        /// </summary>
        private void HandleDescentRequested(SyncTower tower)
        {
            if (playerTransform == null || tower == null)
                return;

            if (descentHelper == null)
                descentHelper = DescentHelper.Create(playerTransform);

            if (descentHelper == null)
                return;

            descentHelper.BeginDescent(tower.GetDescentRoute());
        }

        /// <summary>Nombre de contrats ni pris ni terminés.</summary>
        private int CountAvailableContracts()
        {
            if (contractManager == null)
                return 0;

            int count = 0;
            for (int i = 0; i < contractManager.Contracts.Count; i++)
            {
                ContractManager.Contract contract = contractManager.Contracts[i];
                if (!contract.IsTaken && !contract.IsCompleted)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Choisit les emplacements des contrats AVANT la génération du décor,
        /// puis réserve ces positions pour qu'aucun pilier n'y apparaisse.
        /// </summary>
        private void PlanContracts()
        {
            GameObject contractsRoot = new GameObject("ContractManager");
            contractsRoot.transform.SetParent(worldRoot.transform, false);

            contractManager = contractsRoot.AddComponent<ContractManager>();

            // Graine dérivée : les contrats changent si la taille du monde change.
            System.Random random = new System.Random((worldHalfSize * 7f).GetHashCode() ^ 0x5EED);
            contractManager.PlanContracts(random, world, contractCount);

            for (int i = 0; i < contractManager.Contracts.Count; i++)
            {
                ContractManager.Contract contract = contractManager.Contracts[i];
                world.ReserveSpot(contract.PickupPosition, 10f);
                world.ReserveSpot(contract.DeliveryPosition, 10f);
            }
        }

        /// <summary>Construit la géométrie des contrats (balises lumineuses).</summary>
        private void BuildContracts()
        {
            if (contractManager == null || playerTransform == null)
                return;

            contractManager.Initialize(playerTransform);
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

            // Monde ouvert : la caméra voit très loin, mais les ombres au-delà
            // de ~150 m sont invisibles à l'écran et coûtent cher. On garde donc
            // une zone d'ombre courte et nette autour du joueur.
            if (openWorldEnabled)
            {
                light.shadowStrength = 0.8f;
                QualitySettings.shadowDistance = 150f;
                QualitySettings.shadowCascades = 2;
            }
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

            // Monde ouvert : brouillard plus léger pour laisser voir les tours
            // au loin (elles servent de repère de navigation). Sans lui, à
            // 700 m de distance, tout serait uniformément gris.
            RenderSettings.fogDensity = openWorldEnabled ? 0.0012f : fogDensity;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.24f, 0.33f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.16f, 0.21f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.06f, 0.08f);
        }

        // ============================================================
        // DIAGNOSTIC (Éditeur uniquement)
        // ============================================================

        /// <summary>
        /// Résumé lisible du monde généré, dans la Console Unity.
        /// Utile quand on ouvre le projet pour la première fois : on voit
        /// immédiatement ce qui a été créé et où se trouve quoi.
        /// </summary>
        private void LogOpenWorldSummary()
        {
            if (!openWorldEnabled)
            {
                Debug.Log("[AETHER DRIFT] Mode arène (Étape 1) : monde ouvert désactivé.");
                return;
            }

            int triangles = 0;
            MeshFilter[] filters = worldRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh != null)
                    triangles += filters[i].sharedMesh.triangles.Length / 3;
            }

            Debug.Log("[AETHER DRIFT] MONDE OUVERT — " + (worldHalfSize * 2f) + " m de côté.\n"
                + "  • Zones : ruines (centre), rampes de saut (nord), chemins anciens (anneau).\n"
                + "  • Tours de synchronisation : " + towers.Count + " — monte la spirale, puis R pour redescendre.\n"
                + "  • Contrats : " + (contractManager != null ? contractManager.Contracts.Count : 0)
                + " balises cyan (traverse-les).\n"
                + "  • " + filters.Length + " objets de décor, " + triangles + " triangles au total.");
        }

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
