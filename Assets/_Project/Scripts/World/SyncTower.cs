using System;
using System.Collections.Generic;
using UnityEngine;
using AetherDrift.Core;
using AetherDrift.Player;

namespace AetherDrift.World
{
    /// <summary>
    /// Tour de synchronisation — ADN Assassin's Creed — ÉTAPE 3.
    ///
    /// PRINCIPE
    ///   Une grande tour lumineuse, accessible par une rampe en spirale.
    ///   Quand le joueur atteint son sommet, la tour :
    ///     1. change de couleur (cyan dormant → or synchronisé),
    ///     2. déclenche une impulsion visuelle (anneaux qui s'élèvent),
    ///     3. débloque sur la mini-map les icônes de boss et de contrats.
    ///
    /// DÉTECTION DU SOMMET
    ///   Une sphère de déclenchement au sommet, plus un test de position dans
    ///   Update() : on ne dépend pas de la physique (la voiture peut arriver
    ///   par les airs après un saut de rampe).
    ///
    /// L'impulsion visuelle est générée par code : anneaux qui montent et
    /// s'effacent. Aucun asset, aucune particule à configurer.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class SyncTower : MonoBehaviour
    {
        // ============================================================
        // ÉVÉNEMENTS
        // ============================================================

        /// <summary>Déclenché quand cette tour est synchronisée pour la première fois.</summary>
        public event Action<SyncTower> OnSynchronized;

        /// <summary>
        /// Déclenché quand le joueur demande à redescendre (une fois la tour
        /// synchronisée). Le Bootstrapper fournit alors l'itinéraire de retour.
        /// </summary>
        public event Action<SyncTower> OnDescentRequested;

        // ============================================================
        // RÉGLAGES
        // ============================================================

        [Header("Géométrie")]
        [Tooltip("Hauteur totale de la tour.")]
        [SerializeField] private float height = 46f;

        [Tooltip("Rayon du fût.")]
        [SerializeField] private float shaftRadius = 4.5f;

        [Tooltip("Rayon de la plateforme sommitale.")]
        [SerializeField] private float platformRadius = 9f;

        [Header("Couleurs")]
        [Tooltip("Couleur avant synchronisation (dormante).")]
        [SerializeField] private Color dormantColor = new Color(0.16f, 0.35f, 0.45f);

        [Tooltip("Couleur après synchronisation (or runique).")]
        [SerializeField] private Color syncedColor = new Color(1f, 0.72f, 0.25f);

        [Header("Interaction")]
        [Tooltip("Distance horizontale sous laquelle le sommet est considéré atteint.")]
        [SerializeField] private float triggerRadius = 8f;

        [Tooltip("Tolérance verticale autour du sommet.")]
        [SerializeField] private float triggerHeightTolerance = 5f;

        // ============================================================
        // ÉTAT
        // ============================================================

        /// <summary>Identifiant lisible (affiché dans la Console et sur la mini-map).</summary>
        public string TowerName { get; private set; } = "Tour";

        /// <summary>Position du sommet (point de déclenchement).</summary>
        public Vector3 SummitPosition => transform.position + Vector3.up * height;

        /// <summary>Position au pied de la tour (point d'arrivée de la descente).</summary>
        public Vector3 FootPosition => transform.position + new Vector3(-(SpiralExitRadius + 16f), 0.6f, 0f);

        /// <summary>Rayon extérieur de la spirale (utile au calcul de descente).</summary>
        private float SpiralExitRadius => shaftRadius + 4.5f + 3f;

        /// <summary>La tour a-t-elle été synchronisée ?</summary>
        public bool IsSynchronized { get; private set; }

        private Material beaconMaterial;
        private Material ringMaterial;
        private Transform beacon;
        private Transform player;
        private readonly List<Transform> impulseRings = new List<Transform>();
        private float impulseTimer;
        private float pulse;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        /// <summary>
        /// Construit la tour complète à la position donnée.
        /// Appelé par <see cref="WorldGenerator"/> ou par le Bootstrapper.
        /// </summary>
        public static SyncTower Create(Transform parent, Vector3 position, string towerName,
            float height, Color dormant, Color synced)
        {
            GameObject towerObject = new GameObject("SyncTower - " + towerName);
            towerObject.transform.SetParent(parent, false);
            towerObject.transform.position = position;

            SyncTower tower = towerObject.AddComponent<SyncTower>();
            tower.TowerName = towerName;
            tower.height = height;
            tower.dormantColor = dormant;
            tower.syncedColor = synced;
            tower.Build();

            return tower;
        }

        /// <summary>
        /// Assemble la géométrie : fût à gradins, rampe en spirale, plateforme,
        /// balise lumineuse, et anneaux d'impulsion (inactifs au départ).
        /// </summary>
        private void Build()
        {
            // Dimensions de la spirale, utilisées dès le socle.
            float spiralRadius = shaftRadius + 4.5f;
            const float spiralWidth = 6f;
            const float spiralThickness = 0.6f;

            // --- 1. Fût : empilement de tambours légèrement coniques ---
            LowPolyMesh shaftMesh = new LowPolyMesh();
            const int drums = 6;
            float drumHeight = height / drums;

            for (int i = 0; i < drums; i++)
            {
                float rBottom = shaftRadius * (1f - 0.05f * i);
                float rTop = shaftRadius * (1f - 0.05f * (i + 1));
                shaftMesh.AddFrustum(Vector3.up * (drumHeight * i), rBottom, rTop, drumHeight, 10, false, false);
            }

            // Couronne au pied de la tour : purement décorative, elle reste
            // à l'intérieur du rayon intérieur de la spirale (6 m) pour ne
            // jamais gêner l'accès.
            shaftMesh.AddFrustum(Vector3.zero, shaftRadius * 1.25f, shaftRadius * 1.1f, 0.8f, 10, true, false);

            GameObject shaft = new GameObject("Shaft");
            shaft.transform.SetParent(transform, false);
            shaft.AddComponent<MeshFilter>().sharedMesh = shaftMesh.Build("TowerShaft");
            MeshRenderer shaftRenderer = shaft.AddComponent<MeshRenderer>();
            shaftRenderer.sharedMaterial = ProceduralMaterials.Create(
                "TowerStone", new Color(0.36f, 0.38f, 0.44f), Color.black, 0f, 0.15f, 0f);
            shaft.AddComponent<MeshCollider>().sharedMesh = shaft.GetComponent<MeshFilter>().sharedMesh;

            // --- 2. Rampe en spirale : 2,5 tours, pente douce ---
            //
            // Elle démarre AU SOL à l'angle 180°. Avec 3,5 tours, elle arrive
            // à l'angle 180° + 1 260° = 0° : l'arrivée se fait face à
            // l'ouverture de la plateforme, sans demi-tour serré.
            //
            // 3,5 tours plutôt que 2,5 : à 2,5 tours, la pente atteignait 16°
            // (vérifié par le calcul), ce qui fait patiner la voiture en
            // approche du sommet. À 3,5 tours, elle tombe à 12°.
            LowPolyMesh rampMesh = new LowPolyMesh();
            MeshUtils.AddSpiralRamp(rampMesh, Vector3.zero, spiralRadius,
                startAngleDeg: 180f,
                totalAngleDeg: 360f * 3.5f,
                startHeight: 0f,          // au sol : on monte en roulant, sans sauter
                endHeight: height,        // pile au niveau de la plateforme
                width: spiralWidth,
                steps: 120,
                thickness: spiralThickness);

            GameObject ramp = new GameObject("SpiralRamp");
            ramp.transform.SetParent(transform, false);
            ramp.AddComponent<MeshFilter>().sharedMesh = rampMesh.Build("TowerRamp");
            MeshRenderer rampRenderer = ramp.AddComponent<MeshRenderer>();
            rampRenderer.sharedMaterial = ProceduralMaterials.Create(
                "TowerRampStone", new Color(0.32f, 0.33f, 0.38f), Color.black, 0f, 0.12f, 0f);
            ramp.AddComponent<MeshCollider>().sharedMesh = ramp.GetComponent<MeshFilter>().sharedMesh;

            // Garde-corps extérieur : empêche de tomber en cas de dérapage,
            // tout en restant franchissable (pas de blocage si on le heurte).
            LowPolyMesh railMesh = new LowPolyMesh();
            const int railSteps = 84;
            for (int i = 0; i < railSteps; i++)
            {
                float t = (float)i / railSteps;
                float angle = (180f + 1260f * t) * Mathf.Deg2Rad;
                float y = height * t;

                Vector3 pos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (spiralRadius + spiralWidth * 0.5f - 0.2f)
                              + Vector3.up * (y + 0.55f);

                MeshUtils.AddBox(railMesh, pos, new Vector3(0.3f, 0.9f, 0.3f),
                    Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f));
            }

            GameObject rail = new GameObject("RampRailing");
            rail.transform.SetParent(transform, false);
            rail.AddComponent<MeshFilter>().sharedMesh = railMesh.Build("TowerRailing");
            MeshRenderer railRenderer = rail.AddComponent<MeshRenderer>();
            railRenderer.sharedMaterial = rampRenderer.sharedMaterial;
            railRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // --- 3. Plateforme sommitale ---
            LowPolyMesh platformMesh = new LowPolyMesh();
            // Centrée 0,6 m sous le sommet : sa face supérieure tombe pile au
            // niveau de la sortie de rampe (aucune marche à franchir).
            MeshUtils.AddDisc(platformMesh, Vector3.up * (height - 0.6f), platformRadius, 1.2f, 14);
            // Garde-corps, percé d'une ouverture devant l'arrivée de la rampe.
            const int posts = 18;
            for (int i = 0; i < posts; i++)
            {
                float angle = (Mathf.PI * 2f / posts) * i;

                // Ouverture autour de 0° : la spirale arrive à 180° + 3,5 tours
                // = 0°. On perce donc le garde-corps devant, pas derrière.
                float angleDeg = angle * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(angleDeg, 0f)) < 25f)
                    continue;
                Vector3 pos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (platformRadius - 0.5f)
                              + Vector3.up * (height + 0.9f);
                MeshUtils.AddBox(platformMesh, pos, new Vector3(0.4f, 1.6f, 0.4f),
                    Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f));
            }

            GameObject platform = new GameObject("Platform");
            platform.transform.SetParent(transform, false);
            platform.AddComponent<MeshFilter>().sharedMesh = platformMesh.Build("TowerPlatform");
            MeshRenderer platformRenderer = platform.AddComponent<MeshRenderer>();
            platformRenderer.sharedMaterial = shaftRenderer.sharedMaterial;
            platform.AddComponent<MeshCollider>().sharedMesh = platform.GetComponent<MeshFilter>().sharedMesh;

            // --- 4. Balise lumineuse au sommet (matériau piloté par l'état) ---
            LowPolyMesh beaconMesh = new LowPolyMesh();
            beaconMesh.AddCone(Vector3.up * (height + 2.2f), Vector3.up * (height + 16f), 1.6f, 6, true);

            GameObject beaconObject = new GameObject("Beacon");
            beaconObject.transform.SetParent(transform, false);
            beaconObject.AddComponent<MeshFilter>().sharedMesh = beaconMesh.Build("TowerBeacon");

            beaconMaterial = ProceduralMaterials.Create("TowerBeacon", dormantColor, dormantColor, 1.8f, 0.6f, 0f);
            MeshRenderer beaconRenderer = beaconObject.AddComponent<MeshRenderer>();
            beaconRenderer.sharedMaterial = beaconMaterial;
            beaconRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            beacon = beaconObject.transform;

            // --- 5. Anneaux d'impulsion (créés, mais invisibles) ---
            ringMaterial = ProceduralMaterials.Create("TowerImpulse", syncedColor, syncedColor, 3.5f, 0.5f, 0f);

            for (int i = 0; i < 3; i++)
            {
                LowPolyMesh ringMesh = new LowPolyMesh();
                ringMesh.AddFlatAnnulus(Vector3.zero, 5.5f, 6.4f, 32);

                GameObject ringObject = new GameObject("ImpulseRing_" + i);
                ringObject.transform.SetParent(transform, false);
                ringObject.transform.position = SummitPosition;
                ringObject.AddComponent<MeshFilter>().sharedMesh = ringMesh.Build("ImpulseRing");

                MeshRenderer ringRenderer = ringObject.AddComponent<MeshRenderer>();
                ringRenderer.sharedMaterial = ringMaterial;
                ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                ringObject.SetActive(false);
                impulseRings.Add(ringObject.transform);
            }
        }

        // ============================================================
        // BOUCLE
        // ============================================================

        private void Start()
        {
            // Le joueur est créé après les tours : on le cherche au démarrage.
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        private void Update()
        {
            AnimateBeacon();
            AnimateImpulse();

            if (player == null)
                return;

            // R permet de redescendre proprement — mais SEULEMENT depuis le
            // haut de la tour. Sans ce test, appuyer sur R n'importe où dans le
            // monde téléporterait le joueur au sommet.
            if (IsSynchronized && Input.GetKeyDown(KeyCode.R) && IsPlayerOnTower())
            {
                OnDescentRequested?.Invoke(this);
                return;
            }

            if (!IsSynchronized)
                CheckSummitReached();
        }

        /// <summary>La balise pulse lentement tant que la tour est dormante.</summary>
        private void AnimateBeacon()
        {
            if (beaconMaterial == null)
                return;

            pulse += Time.deltaTime;

            if (IsSynchronized)
            {
                // Synchronisée : lueur stable et forte.
                SetEmission(beaconMaterial, syncedColor, 3.2f);
                if (beacon != null)
                    beacon.localRotation = Quaternion.Euler(0f, Time.time * 40f, 0f);
            }
            else
            {
                // Dormante : respiration lente (2 s), pour attirer l'œil de loin.
                float t = (Mathf.Sin(pulse * 1.6f) + 1f) * 0.5f;
                SetEmission(beaconMaterial, dormantColor, 0.8f + t * 1.2f);
            }
        }

        /// <summary>Les anneaux d'impulsion montent et s'effacent.</summary>
        private void AnimateImpulse()
        {
            if (impulseTimer <= 0f)
                return;

            impulseTimer -= Time.deltaTime;

            for (int i = 0; i < impulseRings.Count; i++)
            {
                Transform ring = impulseRings[i];
                if (ring == null || !ring.gameObject.activeSelf)
                    continue;

                // Décalage temporel entre les anneaux : effet de vague.
                float delay = i * 0.18f;
                float local = Mathf.Max(0f, (0.9f - impulseTimer) - delay);
                if (local <= 0f)
                    continue;

                float t = Mathf.Clamp01(local / 1.4f);
                if (t >= 1f)
                {
                    ring.gameObject.SetActive(false);
                    continue;
                }

                ring.position = SummitPosition + Vector3.up * (t * 26f);
                float scale = Mathf.Lerp(0.4f, 4.5f, t);
                ring.localScale = new Vector3(scale, 1f, scale);
            }

            if (impulseTimer <= 0f)
            {
                for (int i = 0; i < impulseRings.Count; i++)
                    if (impulseRings[i] != null)
                        impulseRings[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Teste si le joueur est au sommet. Basé sur la position, pas sur la
        /// physique : fonctionne aussi après un saut de rampe.
        /// </summary>
        private void CheckSummitReached()
        {
            Vector3 summit = SummitPosition;
            Vector3 flatDelta = new Vector3(player.position.x - summit.x, 0f, player.position.z - summit.z);

            if (flatDelta.magnitude > triggerRadius)
                return;

            if (Mathf.Abs(player.position.y - summit.y) > triggerHeightTolerance)
                return;

            Synchronize();
        }

        /// <summary>Le joueur est-il en haut de cette tour ?</summary>
        private bool IsPlayerOnTower()
        {
            if (player == null)
                return false;

            Vector3 summit = SummitPosition;
            Vector3 flatDelta = new Vector3(player.position.x - summit.x, 0f, player.position.z - summit.z);

            bool closeHorizontally = flatDelta.magnitude < triggerRadius + 8f;
            bool highEnough = player.position.y > summit.y - height * 0.45f;

            return closeHorizontally && highEnough;
        }

        // ============================================================
        // SYNCHRONISATION
        // ============================================================

        /// <summary>
        /// Active la tour : changement de couleur, impulsion visuelle, événement.
        /// N'a d'effet qu'une seule fois.
        /// </summary>
        public void Synchronize()
        {
            if (IsSynchronized)
                return;

            IsSynchronized = true;
            impulseTimer = 1.9f;

            for (int i = 0; i < impulseRings.Count; i++)
            {
                if (impulseRings[i] == null)
                    continue;

                impulseRings[i].gameObject.SetActive(true);
                impulseRings[i].position = SummitPosition;
                impulseRings[i].localScale = Vector3.one * 0.4f;
            }

            // La balise passe à l'or runique.
            if (beaconMaterial != null)
                SetEmission(beaconMaterial, syncedColor, 3.2f);

            CameraFollow camera = FindObjectOfType<CameraFollow>();
            if (camera != null)
                camera.AddShake(0.35f);

            Debug.Log("[AETHER DRIFT] Tour synchronisée : " + TowerName
                + " — boss et contrats révélés sur la carte. Appuie sur R pour redescendre.");

            OnSynchronized?.Invoke(this);
        }

        /// <summary>
        /// Itinéraire de retour : le véhicule glisse du sommet jusqu'au pied de
        /// la tour, en s'écartant de la spirale pour ne pas la heurter.
        ///
        /// Deux points suffisent : la trajectoire est une courbe lisse calculée
        /// par <see cref="DescentHelper"/>. Le joueur peut toujours redescendre
        /// la spirale à la main s'il préfère.
        /// </summary>
        public List<Vector3> GetDescentRoute()
        {
            Vector3 foot = FootPosition;

            // Point intermédiaire : sous le sommet, décalé vers l'extérieur,
            // pour que la descente passe à côté de la rampe et non dessus.
            Vector3 mid = new Vector3(
                transform.position.x - (SpiralExitRadius + 8f),
                height * 0.5f,
                transform.position.z);

            return new List<Vector3> { SummitPosition, mid, foot };
        }

        private static void SetEmission(Material material, Color color, float intensity)
        {
            Color emissive = color * intensity;
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emissive);
            if (material.HasProperty("_Emission"))
                material.SetColor("_Emission", emissive);
        }

        // ============================================================
        // GIZMOS (aide au placement dans l'éditeur)
        // ============================================================

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            Gizmos.DrawWireSphere(SummitPosition, triggerRadius);
            Gizmos.DrawLine(transform.position, SummitPosition);
        }
    }
}
