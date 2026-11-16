using UnityEngine;
using AetherDrift.Player;

namespace AetherDrift.Core
{
    /// <summary>
    /// Point d'entrée procédural du jeu — ÉTAPE 1.
    ///
    /// Au lancement de la scène (Awake), ce script génère AUTOMATIQUEMENT
    /// tout le nécessaire pour voir le jeu tourner, sans aucune scène .unity
    /// ni aucun asset à placer à la main :
    ///
    ///   1. Le SOL de l'arène / monde ouvert (grand plan sombre).
    ///   2. Le VÉHICULE du joueur (sphère physique invisible + châssis visuel
    ///      en primitives avec lames latérales lumineuses).
    ///   3. La CAMÉRA principale avec CameraFollow (zoom selon la vitesse).
    ///   4. Une LUMIÈRE directionnelle avec ombres.
    ///   5. (Bonus Étape 1) Quelques ruines décoratives pour donner l'échelle
    ///      de la vallée — le vrai monde ouvert arrive à l'Étape 3.
    ///
    /// Utilisation dans Unity :
    ///   - Crée une scène vide, ajoute un GameObject "Bootstrapper",
    ///   - Glisse ce script dessus, appuie sur Play : tout apparaît.
    ///
    /// [DefaultExecutionOrder(-100)] garantit que le Bootstrapper tourne
    /// AVANT tous les autres scripts (le monde existe avant qu'on le suive).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Sol / Arène")]
        [Tooltip("Taille du sol (en mètres, côté du carré).")]
        [SerializeField] private float groundSize = 200f;

        [Tooltip("Couleur sombre du sol.")]
        [SerializeField] private Color groundColor = new Color(0.10f, 0.11f, 0.13f);

        [Header("Véhicule")]
        [Tooltip("Position d'apparition du véhicule (y ≈ 0.5 : il se pose sur le sol).")]
        [SerializeField] private Vector3 playerSpawnPosition = new Vector3(0f, 0.5f, 0f);

        [Header("Caméra")]
        [Tooltip("Couleur du ciel / fond.")]
        [SerializeField] private Color skyColor = new Color(0.08f, 0.10f, 0.16f);

        [Header("Lumière")]
        [Tooltip("Intensité de la lumière directionnelle.")]
        [SerializeField] private float lightIntensity = 1.1f;

        [Tooltip("Couleur de la lumière (lumière chaude de fin de journée).")]
        [SerializeField] private Color lightColor = new Color(1f, 0.92f, 0.78f);

        private Transform playerTransform;

        private void Awake()
        {
            // 0. Nettoyage : si l'utilisateur a créé la scène avec "Basic (Built-in)",
            //    Unity y a placé une caméra et une lumière par défaut. On les supprime
            //    pour éviter deux caméras (et deux AudioListener) qui se battent.
            RemoveDefaultSceneObjects();

            // Ordre important : le sol d'abord, puis le véhicule posé dessus,
            // puis la caméra qui le suit, puis la lumière qui l'éclaire.
            CreateGround();
            CreatePlayerVehicle();
            CreateCamera();
            CreateDirectionalLight();
            CreateDecorativeRuins();

            ConfigureAmbiance();

            Debug.Log("[AETHER DRIFT] Bootstrapper : scène générée procéduralement (Étape 1).");
        }

        // ============================================================
        // 0. NETTOYAGE DE LA SCÈNE
        // ============================================================

        /// <summary>
        /// Supprime les caméras et lumières déjà présentes dans la scène
        /// (scène "Basic" par défaut d'Unity, scène sauvegardée après un Play,
        /// ou relance du Bootstrapper). Le Bootstrapper est la seule source
        /// de vérité : la scène ne doit contenir que lui.
        /// </summary>
        private void RemoveDefaultSceneObjects()
        {
            // Caméras tierces (la nôtre n'existe pas encore à ce stade).
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in cameras)
            {
                if (cam != null && cam.gameObject != gameObject)
                    Destroy(cam.gameObject);
            }

            // Lumières tierces.
            Light[] lights = FindObjectsOfType<Light>();
            foreach (Light l in lights)
            {
                if (l != null && l.gameObject != gameObject)
                    Destroy(l.gameObject);
            }

            // Éventuel AudioListener orphelin (deux listeners = avertissement Unity).
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
            foreach (AudioListener listener in listeners)
            {
                if (listener != null && listener.gameObject != gameObject)
                    Destroy(listener);
            }
        }

        // ============================================================
        // 1. SOL
        // ============================================================

        /// <summary>
        /// Grand plan sombre qui sert de sol à l'arène / vallée.
        /// Le MeshCollider de la primitive Plane est conservé : le véhicule
        /// (et plus tard les ennemis) peuvent rouler dessus.
        /// </summary>
        private void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground (Arène)";
            ground.tag = "Untagged";

            // Le plan Unity fait 10x10 m : on le met à l'échelle pour couvrir groundSize.
            float scale = groundSize / 10f;
            ground.transform.localScale = new Vector3(scale, 1f, scale);
            ground.transform.position = Vector3.zero;

            MeshRenderer renderer = ground.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateMaterial(groundColor, Color.black, 0f);

            // Lignes de repérage au sol : indispensables pour "sentir" la vitesse.
            CreateGroundLines();
        }

        /// <summary>
        /// Trace une grille de lignes fines au sol (façon piste / circuit).
        /// On utilise de vrais cubes plutôt qu'un plan transparent : c'est
        /// fiable sur Built-in ET URP, sans réglage de blending fragile.
        /// Purement visuel : aucun collider.
        /// </summary>
        private void CreateGroundLines()
        {
            GameObject gridRoot = new GameObject("GroundLines (repères)");
            gridRoot.transform.SetParent(transform, false);

            Material lineMat = CreateMaterial(new Color(0.20f, 0.55f, 0.70f), new Color(0.2f, 0.7f, 0.9f), 0.35f);

            float half = groundSize * 0.5f;
            int steps = Mathf.Max(1, Mathf.RoundToInt(groundSize / 20f)); // une ligne tous les 20 m
            float thickness = 0.12f;
            float height = 0.02f;

            for (int i = -steps; i <= steps; i++)
            {
                float t = (half / steps) * i;

                // Ligne parallèle à Z.
                CreateLine(gridRoot.transform, "LineZ_" + i, lineMat,
                    new Vector3(t, height * 0.5f, 0f), new Vector3(thickness, height, groundSize));

                // Ligne parallèle à X.
                CreateLine(gridRoot.transform, "LineX_" + i, lineMat,
                    new Vector3(0f, height * 0.5f, t), new Vector3(groundSize, height, thickness));
            }
        }

        private void CreateLine(Transform parent, string name, Material material, Vector3 position, Vector3 scale)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.SetParent(parent, false);
            line.transform.localPosition = position;
            line.transform.localScale = scale;

            Collider col = line.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
                Destroy(col);
            }

            MeshRenderer renderer = line.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        // ============================================================
        // 2. VÉHICULE
        // ============================================================

        /// <summary>
        /// Crée le véhicule du joueur :
        /// - Racine "PlayerVehicle" taggée "Player" (la caméra la retrouve).
        /// - Rigidbody + PlayerVehicle (qui construit la sphère invisible
        ///   et le châssis visuel dans son Awake).
        /// </summary>
        private void CreatePlayerVehicle()
        {
            GameObject player = new GameObject("PlayerVehicle");
            player.tag = "Player";
            player.transform.position = playerSpawnPosition;
            player.transform.rotation = Quaternion.identity;

            // Rigidbody porté par la racine (la sphère invisible est enfant).
            Rigidbody rb = player.AddComponent<Rigidbody>();
            rb.mass = 1200f;

            // Le script PlayerVehicle configure la physique complète et le visuel.
            player.AddComponent<PlayerVehicle>();

            playerTransform = player.transform;
        }

        // ============================================================
        // 3. CAMÉRA
        // ============================================================

        /// <summary>
        /// Caméra principale taggée "MainCamera" avec le script CameraFollow.
        /// Position initiale derrière le véhicule, regard vers lui.
        /// </summary>
        private void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            Camera cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = skyColor;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 500f;

            CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
            if (playerTransform != null)
                follow.SetTarget(playerTransform);

            // Placement initial : derrière et au-dessus du véhicule.
            // CameraFollow prend le relais à partir de son Start().
            Vector3 startOffset = new Vector3(0f, 6f, -10f);
            Vector3 startPosition = (playerTransform != null ? playerTransform.position : Vector3.zero) + startOffset;
            cameraObject.transform.position = startPosition;
            if (playerTransform != null)
                cameraObject.transform.LookAt(playerTransform.position + new Vector3(0f, 1.5f, 0f));
        }

        // ============================================================
        // 4. LUMIÈRE
        // ============================================================

        /// <summary>
        /// Lumière directionnelle (soleil) avec ombres dynamiques.
        /// Angle bas pour une ambiance "fin de journée" médiévale-fantastique.
        /// </summary>
        private void CreateDirectionalLight()
        {
            GameObject lightObject = new GameObject("Directional Light (Soleil)");
            lightObject.tag = "Untagged";

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = lightColor;
            light.intensity = lightIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;

            // Soleil bas sur l'horizon, en biais pour des ombres longues.
            lightObject.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        }

        // ============================================================
        // 5. RUINES DÉCORATIVES (aperçu du monde ouvert — Étape 3)
        // ============================================================

        /// <summary>
        /// Quelques piliers de ruines en cercle autour de l'arène.
        /// Purement décoratif à l'Étape 1 : donne l'échelle de la vallée
        /// et annonce le thème "ruines escaladables" (grappin = Étape 5).
        /// Le vrai terrain procédural arrive à l'Étape 3 (dossier World/).
        /// </summary>
        private void CreateDecorativeRuins()
        {
            GameObject ruinsRoot = new GameObject("Ruins (décor Étape 1)");
            ruinsRoot.tag = "Untagged";

            Material stoneMat = CreateMaterial(new Color(0.32f, 0.33f, 0.36f), Color.black, 0f);
            Material mossMat = CreateMaterial(new Color(0.22f, 0.38f, 0.24f), Color.black, 0f);

            int pillarCount = 10;
            float radius = groundSize * 0.32f;

            for (int i = 0; i < pillarCount; i++)
            {
                float angle = (360f / pillarCount) * i;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);

                // Hauteur variable : certaines colonnes sont brisées (thème ruines).
                float height = 6f + ((i * 37) % 10); // pseudo-aléatoire déterministe
                float width = 2.2f;

                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "RuinPillar_" + i;
                pillar.transform.SetParent(ruinsRoot.transform, false);
                pillar.transform.position = new Vector3(pos.x, height * 0.5f, pos.z);
                pillar.transform.localScale = new Vector3(width, height, width);
                pillar.transform.rotation = Quaternion.Euler(0f, angle, 0f);

                MeshRenderer renderer = pillar.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sharedMaterial = (i % 3 == 0) ? mossMat : stoneMat;

                // Chapiteau : petit cube au sommet des piliers entiers.
                if (height > 10f)
                {
                    GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cap.name = "RuinCap_" + i;
                    cap.transform.SetParent(ruinsRoot.transform, false);
                    cap.transform.position = new Vector3(pos.x, height + 0.6f, pos.z);
                    cap.transform.localScale = new Vector3(width * 1.4f, 1.2f, width * 1.4f);
                    cap.transform.rotation = Quaternion.Euler(0f, angle, 0f);

                    MeshRenderer capRenderer = cap.GetComponent<MeshRenderer>();
                    if (capRenderer != null)
                        capRenderer.sharedMaterial = stoneMat;
                }
            }
        }

        // ============================================================
        // AMBIANCE
        // ============================================================

        /// <summary>
        /// Brouillard + lumière ambiante pour l'atmosphère de vallée brumeuse.
        /// </summary>
        private void ConfigureAmbiance()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.55f, 0.58f, 0.62f);
            RenderSettings.fogDensity = 0.012f;

            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.45f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        // ============================================================
        // OUTILS
        // ============================================================

        /// <summary>
        /// Matériau compatible Built-in, URP et HDRP (voir PlayerVehicle).
        /// </summary>
        private Material CreateMaterial(Color color, Color emissionColor, float emissionIntensity)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            Material mat = new Material(shader);
            mat.color = color;

            if (emissionIntensity > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor * emissionIntensity);
                mat.SetColor("_Emission", emissionColor * emissionIntensity);
            }

            return mat;
        }
    }
}
