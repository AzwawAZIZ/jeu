using System;
using UnityEngine;

namespace AetherDrift.Player
{
    /// <summary>
    /// Contrôleur du bolide runique — ÉTAPE 2.
    ///
    /// PHYSIQUE "SPHERE CAR"
    ///   Toute la poussée passe par AddForce sur le Rigidbody de la racine
    ///   (la sphère invisible est la seule surface de collision). Aucun
    ///   CharacterController, aucun WheelCollider : le comportement est
    ///   volontairement arcade — réactif, prévisible, jamais frustrant.
    ///
    ///   1. Adhérence : on annule la composante latérale de la vitesse
    ///      (l'équivalent d'un pneu qui tient la route). En drift, ce facteur
    ///      chute brutalement → le bolide glisse.
    ///   2. Direction : la vitesse angulaire en lacet est pilotée directement
    ///      (pas de couple), donc la voiture tourne exactement comme on le
    ///      demande, quelle que soit la vitesse.
    ///   3. Appui aérodynamique : une force vers le bas croît avec la vitesse,
    ///      ce qui colle la sphère au sol et évite les sauts involontaires.
    ///
    /// DRIFT & BOOST (Éther)
    ///   Maintenir Espace : adhérence latérale réduite, châssis incliné,
    ///   jauge d'éther qui monte (plus vite en virage serré).
    ///   Relâcher : impulsion de vitesse proportionnelle à la charge.
    ///
    /// ESQUIVE (ADN Souls)
    ///   Clic droit ou Shift : dash latéral instantané + 0,4 s d'invulnérabilité,
    ///   payé en endurance (voir PlayerStats). On ne peut pas enchaîner.
    ///
    /// RETOUR VISUEL
    ///   - Trails derrière les deux patins arrière et les deux lames.
    ///   - Intensité des lames proportionnelle à la charge d'éther.
    ///   - Réacteurs qui s'embrasent à l'accélération et au boost.
    ///   - Châssis qui penche (roulis) en virage et pique du nez au freinage.
    ///   - Secousse caméra à l'esquive et au boost.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerVehicle))]
    public class CarController : MonoBehaviour
    {
        // ============================================================
        // RÉGLAGES — CONDUITE
        // ============================================================

        [Header("Conduite")]
        [Tooltip("Vitesse maximale en m/s (42 ≈ 150 km/h).")]
        [SerializeField] private float maxSpeed = 42f;

        [Tooltip("Vitesse maximale en marche arrière.")]
        [SerializeField] private float maxReverseSpeed = 13f;

        [Tooltip("Accélération (m/s²).")]
        [SerializeField] private float acceleration = 26f;

        [Tooltip("Force de freinage / d'inversion de poussée.")]
        [SerializeField] private float braking = 34f;

        [Tooltip("Vitesse angulaire en lacet à pleine efficacité (degrés/seconde).")]
        [SerializeField] private float turnRate = 135f;

        [Tooltip("Vitesse en dessous de laquelle la direction perd de son efficacité.")]
        [SerializeField] private float minSpeedForFullSteer = 7f;

        // ============================================================
        // RÉGLAGES — ADHÉRENCE & DRIFT
        // ============================================================

        [Header("Adhérence et drift")]
        [Tooltip("Adhérence latérale normale (élevée = colle à la trajectoire).")]
        [SerializeField] private float grip = 9f;

        [Tooltip("Adhérence latérale pendant le drift (bas = glisse).")]
        [SerializeField] private float driftGrip = 2.1f;

        [Tooltip("Bonus de vitesse angulaire pendant le drift.")]
        [SerializeField] private float driftTurnBoost = 1.45f;

        [Tooltip("Vitesse minimale pour déclencher un drift.")]
        [SerializeField] private float minSpeedForDrift = 6f;

        [Tooltip("Force d'appui aérodynamique (colle la sphère au sol).")]
        [SerializeField] private float downforce = 14f;

        // ============================================================
        // RÉGLAGES — BOOST & ESQUIVE
        // ============================================================

        [Header("Boost (relâchement du drift)")]
        [Tooltip("Impulsion de vitesse maximale, pour une charge d'éther pleine.")]
        [SerializeField] private float boostImpulseMax = 19f;

        [Tooltip("Durée du boost visuel (trails, réacteurs), en secondes.")]
        [SerializeField] private float boostVisualDuration = 0.85f;

        [Tooltip("Multiplicateur de vitesse maximale autorisée pendant un boost.")]
        [SerializeField] private float boostSpeedMultiplier = 1.32f;

        [Header("Esquive (dash d'invulnérabilité)")]
        [Tooltip("Impulsion latérale du dash.")]
        [SerializeField] private float dashImpulse = 17f;

        [Tooltip("Vitesse de rotation visuelle du châssis vers la trajectoire.")]
        [SerializeField] private float visualRotationSpeed = 12f;

        [Tooltip("Angle de roulis maximal du châssis en virage (degrés).")]
        [SerializeField] private float maxRoll = 16f;

        [Tooltip("Angle de tangage maximal du châssis à l'accélération (degrés).")]
        [SerializeField] private float maxPitch = 8f;

        // ============================================================
        // RÉGLAGES — TRAILS & DEBUG
        // ============================================================

        [Header("Trails (effets de traînée)")]
        [Tooltip("Couleur des trails de drift (lames).")]
        [SerializeField] private Color driftTrailColor = new Color(0.15f, 0.9f, 1f);

        [Tooltip("Couleur des trails de boost (réacteurs).")]
        [SerializeField] private Color boostTrailColor = new Color(1f, 0.55f, 0.15f);

        [Tooltip("Durée de vie des trails (secondes).")]
        [SerializeField] private float trailTime = 0.42f;

        [Header("Interface de test")]
        [Tooltip("Affiche les jauges d'éther et d'endurance à l'écran (sera remplacé par le HUD de l'Étape 5).")]
        [SerializeField] private bool showDebugGauges = true;

        // ============================================================
        // RÉFÉRENCES
        // ============================================================

        private Rigidbody rb;
        private PlayerStats stats;
        private PlayerVehicle vehicle;
        private Transform chassis;
        private CameraFollow cameraFollow;
        private Camera mainCamera;

        private TrailRenderer[] trails;
        private Material[] trailMaterials;

        // ============================================================
        // ÉTAT INTERNE
        // ============================================================

        private float throttleInput;
        private float steerInput;
        private bool driftHeld;
        private bool dashRequested;

        private bool isDrifting;
        private bool wasDrifting;
        private float driftIntensity;
        private float boostTimer;
        private float dashTimer;
        private float currentSpeed;

        private Vector3 groundNormal = Vector3.up;
        private bool isGrounded;

        // Tampon réutilisé par le raycast : zéro allocation par frame (GC).
        private static readonly RaycastHit[] groundHitBuffer = new RaycastHit[8];

        private bool initialized;

        // ============================================================
        // INITIALISATION
        // ============================================================

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            stats = GetComponent<PlayerStats>();
            vehicle = GetComponent<PlayerVehicle>();

            // Le contrôleur est la source de vérité des contraintes : les axes
            // X/Z sont figés (jamais de tonneau), le lacet (Y) reste piloté.
            // PlayerVehicle applique sa propre valeur dans son Awake ; on la
            // réaffirme ici pour ne dépendre d'aucun ordre d'exécution.
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void Start()
        {
            // PlayerVehicle a construit le châssis dans son Awake : on peut le récupérer.
            if (vehicle != null)
                chassis = vehicle.VisualChassis;

            mainCamera = Camera.main;
            if (mainCamera != null)
                cameraFollow = mainCamera.GetComponent<CameraFollow>();

            // Les effets visuels ne doivent JAMAIS pouvoir empêcher de conduire :
            // une exception dans la création des trails (shader absent, etc.)
            // laisserait la voiture immobile et le joueur sans explication.
            try
            {
                CreateTrails();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AETHER DRIFT] Trails indisponibles (conduite non affectée) : " + e.Message);
                trails = null;
            }

            initialized = true;

            Debug.Log("[AETHER DRIFT] CarController prêt — ZQSD/WASD + Espace (drift) + Shift/Clic droit (esquive).");
        }

        // ============================================================
        // LECTURE DES ENTRÉES
        // ============================================================

        private void ReadInput()
        {
            // Support AZERTY et QWERTY :
            //   Unity associe KeyCode à la POSITION physique de la touche.
            //   Sur un clavier AZERTY, la touche « Z » (en haut à gauche) renvoie
            //   donc KeyCode.W. On accepte les deux pour ne léser personne.
            bool forward = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.UpArrow);
            bool backward = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow);
            bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

            throttleInput = (forward ? 1f : 0f) - (backward ? 1f : 0f);
            steerInput = (right ? 1f : 0f) - (left ? 1f : 0f);

            driftHeld = Input.GetKey(KeyCode.Space);

            bool dashKey = Input.GetKeyDown(KeyCode.LeftShift)
                        || Input.GetKeyDown(KeyCode.RightShift)
                        || Input.GetMouseButtonDown(1);
            if (dashKey)
                dashRequested = true;
        }

        // ============================================================
        // BOUCLE PRINCIPALE
        // ============================================================

        private void Update()
        {
            if (!initialized)
                return;

            ReadInput();
            UpdateGroundState();
            UpdateDriftState();
            UpdateTimers();
            HandleDashRequest();
            UpdateTrails();
            UpdateMaterials();
            // Le transform du châssis est mis à jour dans LateUpdate()
            // (UpdateChassisTransform), après la résolution de la physique.
        }

        private void FixedUpdate()
        {
            if (!initialized)
                return;

            currentSpeed = rb.velocity.magnitude;

            ApplyAcceleration();
            ApplySteering();
            ApplyGrip();
            ApplyDownforce();
        }

        private void LateUpdate()
        {
            if (!initialized)
                return;

            UpdateChassisTransform();
        }

        // ============================================================
        // ÉTAT : SOL
        // ============================================================

        /// <summary>
        /// Vérifie la présence du sol sous la sphère et récupère sa normale.
        /// On filtre les colliders du véhicule lui-même.
        /// </summary>
        private void UpdateGroundState()
        {
            isGrounded = false;
            groundNormal = Vector3.up;

            int count = Physics.RaycastNonAlloc(
                transform.position + Vector3.up * 0.2f,
                Vector3.down,
                groundHitBuffer,
                2.6f,
                ~0,
                QueryTriggerInteraction.Ignore);

            float closest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                // Ignore nos propres colliders (la sphère physique).
                if (groundHitBuffer[i].collider.transform.IsChildOf(transform))
                    continue;

                if (groundHitBuffer[i].distance < closest)
                {
                    closest = groundHitBuffer[i].distance;
                    groundNormal = groundHitBuffer[i].normal;
                    isGrounded = true;
                }
            }
        }

        // ============================================================
        // ÉTAT : DRIFT
        // ============================================================

        /// <summary>
        /// Le drift s'active si Espace est maintenu, qu'on roule assez vite
        /// et qu'on est au sol. L'intensité sert à la charge d'éther et au VFX.
        /// </summary>
        private void UpdateDriftState()
        {
            bool wantsDrift = driftHeld && isGrounded && currentSpeed > minSpeedForDrift;
            wasDrifting = isDrifting;
            isDrifting = wantsDrift;

            // Transition drift -> pas drift = relâchement : on convertit la
            // charge d'éther accumulée en impulsion de vitesse.
            if (wasDrifting && !isDrifting)
                ReleaseBoost();

            if (isDrifting)
            {
                // Intensité = part de vitesse latérale (0 = tout droit, 1 = glisse pure).
                Vector3 flatVelocity = Vector3.ProjectOnPlane(rb.velocity, Vector3.up);
                float forwardSpeed = Vector3.Dot(flatVelocity, transform.forward);
                float lateralSpeed = Vector3.Dot(flatVelocity, transform.right);

                float slipRatio = Mathf.Abs(lateralSpeed) / Mathf.Max(1f, flatVelocity.magnitude);
                // Un virage serré compte aussi : on combine glisse et braquage.
                driftIntensity = Mathf.Clamp01(slipRatio * 1.6f + Mathf.Abs(steerInput) * 0.45f);

                stats.ChargeEther(Time.deltaTime, driftIntensity);
            }
            else
            {
                driftIntensity = 0f;
            }
        }

        // ============================================================
        // MINUTEURS & ESQUIVE
        // ============================================================

        private void UpdateTimers()
        {
            if (boostTimer > 0f)
                boostTimer -= Time.deltaTime;

            if (dashTimer > 0f)
                dashTimer -= Time.deltaTime;
        }

        private void HandleDashRequest()
        {
            if (!dashRequested)
                return;

            dashRequested = false;

            if (!stats.CanDash)
            {
                // Endurance insuffisante : on le signale une fois pour que ce
                // soit lisible en jeu (remplacé par un son/secousse à l'Étape 5).
                if (stats.StaminaRatio < 0.34f)
                    Debug.Log("[AETHER DRIFT] Esquive impossible : endurance insuffisante.");
                return;
            }

            if (!stats.TrySpendStaminaForDash())
                return;

            PerformDash();
        }

        /// <summary>
        /// Dash latéral instantané + i-frames. Le côté est choisi par le
        /// braquage au moment de l'esquive (par défaut : à droite).
        /// </summary>
        private void PerformDash()
        {
            float side = Mathf.Abs(steerInput) > 0.1f ? Mathf.Sign(steerInput) : 1f;

            // Vecteur latéral perpendiculaire à la trajectoire (et non au châssis) :
            // l'esquive reste lisible même en plein dérapage.
            Vector3 travel = Vector3.ProjectOnPlane(rb.velocity, Vector3.up);
            Vector3 sideDirection = travel.sqrMagnitude > 1f
                ? Vector3.Cross(Vector3.up, travel.normalized) * -side
                : transform.right * side;

            rb.velocity += sideDirection * dashImpulse;

            // L'esquive ne fait pas dépasser la vitesse de pointe : elle repositionne.
            rb.velocity = Vector3.ClampMagnitude(rb.velocity, maxSpeed * boostSpeedMultiplier);

            stats.TriggerInvulnerability();
            dashTimer = 0.28f;

            if (cameraFollow != null)
            {
                cameraFollow.AddShake(0.45f);
                cameraFollow.AddFovKick(-4f); // léger resserrement = sensation de coup de rein
            }

            Debug.Log("[AETHER DRIFT] Esquive — i-frames " + stats.InvulnerabilityRemaining.ToString("0.00") + " s");
        }

        // ============================================================
        // PHYSIQUE
        // ============================================================

        /// <summary>
        /// Accélération / freinage le long de l'axe avant du véhicule,
        /// projeté sur le plan du sol.
        /// </summary>
        private void ApplyAcceleration()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            float forwardSpeed = Vector3.Dot(rb.velocity, forward);

            if (throttleInput > 0.01f)
            {
                if (forwardSpeed < maxSpeed)
                    rb.AddForce(forward * (acceleration * throttleInput), ForceMode.Acceleration);
            }
            else if (throttleInput < -0.01f)
            {
                // Marche arrière limitée, sinon freinage franc.
                if (forwardSpeed > 1f)
                    rb.AddForce(-forward * braking, ForceMode.Acceleration);
                else if (forwardSpeed > -maxReverseSpeed)
                    rb.AddForce(forward * (acceleration * 0.55f * throttleInput), ForceMode.Acceleration);
            }
            else
            {
                // Résistance au roulement : le bolide ralentit doucement sans input.
                rb.AddForce(-Vector3.ProjectOnPlane(rb.velocity, Vector3.up) * 0.55f, ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// Direction : vitesse angulaire en lacet pilotée, proportionnelle à
        /// la vitesse (on ne tourne pas sur place) et dopée en drift.
        /// </summary>
        private void ApplySteering()
        {
            float speedFactor = Mathf.Clamp01(currentSpeed / minSpeedForFullSteer);

            // En drift, la voiture pivote plus vite : c'est ce qui rend le
            // contre-braquage possible et grisant.
            float turnMultiplier = isDrifting ? driftTurnBoost : 1f;

            // Direction inversée en marche arrière (comportement naturel).
            float directionSign = Vector3.Dot(rb.velocity, transform.forward) < -1f ? -1f : 1f;

            float yawDegrees = steerInput * turnRate * turnMultiplier * speedFactor * directionSign;
            rb.angularVelocity = new Vector3(0f, yawDegrees * Mathf.Deg2Rad, 0f);
        }

        /// <summary>
        /// Adhérence : annule progressivement la vitesse latérale.
        /// C'est le seul réglage qui distingue "colle à la route" et "drift".
        /// </summary>
        private void ApplyGrip()
        {
            if (!isGrounded)
                return;

            Vector3 right = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;
            float lateralSpeed = Vector3.Dot(rb.velocity, right);

            float gripFactor = isDrifting ? driftGrip : grip;

            // Force opposée à la glisse, proportionnelle à la vitesse latérale.
            rb.AddForce(-right * (lateralSpeed * gripFactor), ForceMode.Acceleration);
        }

        /// <summary>Appui aérodynamique : plaque la sphère au sol à haute vitesse.</summary>
        private void ApplyDownforce()
        {
            if (!isGrounded)
                return;

            rb.AddForce(-groundNormal * (downforce * Mathf.Clamp01(currentSpeed / maxSpeed)), ForceMode.Acceleration);
        }

        // ============================================================
        // BOOST (déclenché au relâchement du drift)
        // ============================================================

        /// <summary>
        /// Convertit la charge d'éther en impulsion de vitesse.
        /// Appelé par UpdateDriftState via la transition de relâchement.
        /// </summary>
        private void ReleaseBoost()
        {
            float charge = stats.ConsumeEther();
            if (charge <= 1f)
                return;

            float ratio = Mathf.Clamp01(charge / Mathf.Max(1f, stats.MaxEther));

            // Impulsion le long de la trajectoire réelle (et non du châssis) :
            // un boost en plein virage ne part donc pas dans le décor.
            Vector3 travel = Vector3.ProjectOnPlane(rb.velocity, Vector3.up);
            Vector3 boostDirection = travel.sqrMagnitude > 1f ? travel.normalized : transform.forward;

            rb.velocity += boostDirection * (boostImpulseMax * ratio);
            rb.velocity = Vector3.ClampMagnitude(rb.velocity, maxSpeed * boostSpeedMultiplier);

            boostTimer = boostVisualDuration;

            if (cameraFollow != null)
            {
                cameraFollow.AddShake(0.28f * ratio);
                cameraFollow.AddFovKick(7f * ratio);
            }

            Debug.Log("[AETHER DRIFT] Boost libéré : " + (ratio * 100f).ToString("0") + " % de charge.");
        }

        // ============================================================
        // VISUEL DU CHÂSSIS
        // ============================================================

        /// <summary>
        /// Oriente le châssis vers la direction du mouvement, avec roulis en
        /// virage et tangage à l'accélération/freinage.
        /// </summary>
        private void UpdateChassisTransform()
        {
            if (chassis == null)
                return;

            // --- Position : suit la sphère physique (+ décalage vertical) ---
            Vector3 targetPosition = transform.position + Vector3.up * 0.15f;
            chassis.position = Vector3.Lerp(chassis.position, targetPosition, 1f - Mathf.Exp(-18f * Time.deltaTime));

            // --- Lacet : direction de la trajectoire ---
            Vector3 travel = Vector3.ProjectOnPlane(rb.velocity, Vector3.up);
            float yaw = transform.eulerAngles.y;
            if (travel.magnitude > 2.5f)
            {
                // On mélange l'axe du châssis et la trajectoire : le bolide
                // "regarde" légèrement vers là où il va (contre-braquage visible).
                float travelYaw = Mathf.Atan2(travel.x, travel.z) * Mathf.Rad2Deg;
                float blend = Mathf.Clamp01(travel.magnitude / (maxSpeed * 0.6f));
                yaw = Mathf.LerpAngle(yaw, travelYaw, blend * 0.75f);
            }

            // --- Roulis : le châssis s'incline vers l'extérieur du virage ---
            float rollTarget = -steerInput * maxRoll * (isDrifting ? 1.5f : 1f);
            rollTarget = Mathf.Clamp(rollTarget, -maxRoll * 1.6f, maxRoll * 1.6f);

            // --- Tangage : nez qui se lève à l'accélération, plonge au freinage ---
            float pitchTarget = -throttleInput * maxPitch * 0.6f;
            if (boostTimer > 0f)
                pitchTarget -= maxPitch * 0.9f * (boostTimer / Mathf.Max(0.01f, boostVisualDuration));
            if (isDrifting)
                pitchTarget += 2.5f;

            Quaternion targetRotation = Quaternion.Euler(pitchTarget, yaw, rollTarget);

            // Lissage exponentiel indépendant du framerate.
            float t = 1f - Mathf.Exp(-visualRotationSpeed * Time.deltaTime);
            chassis.rotation = Quaternion.Slerp(chassis.rotation, targetRotation, t);
        }

        // ============================================================
        // TRAILS
        // ============================================================

        /// <summary>
        /// Crée quatre TrailRenderer par code : deux derrière les patins
        /// arrière, deux derrière les lames latérales.
        /// </summary>
        private void CreateTrails()
        {
            if (chassis == null)
                return;

            // Le modèle importé n'a pas de lames latérales : on garde malgré tout
            // les trails (ils partent des flancs et des roues arrière), c'est ce
            // qui rend le drift lisible.


            Shader trailShader = Shader.Find("Sprites/Default");
            if (trailShader == null)
                trailShader = Shader.Find("Unlit/Transparent");
            if (trailShader == null)
                trailShader = Shader.Find("Unlit/Color");

            // Ancres fournies par le véhicule : une seule source de vérité
            // pour la géométrie (si les lames bougent, les trails suivent).
            Vector3[] anchors =
            {
                vehicle != null ? vehicle.GetHoverPadAnchor(-1) : new Vector3(-0.80f, -0.22f, -1.55f),
                vehicle != null ? vehicle.GetHoverPadAnchor(1) : new Vector3(0.80f, -0.22f, -1.55f),
                vehicle != null ? vehicle.GetBladeAnchor(-1) : new Vector3(-1.30f, 0.30f, -1.30f),
                vehicle != null ? vehicle.GetBladeAnchor(1) : new Vector3(1.30f, 0.30f, -1.30f),
            };

            trails = new TrailRenderer[anchors.Length];
            trailMaterials = new Material[anchors.Length];

            for (int i = 0; i < anchors.Length; i++)
            {
                GameObject trailObject = new GameObject("Trail_" + i);
                trailObject.transform.SetParent(chassis, false);
                trailObject.transform.localPosition = anchors[i];

                TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
                trail.time = trailTime;
                trail.minVertexDistance = 0.12f;
                trail.autodestruct = false;
                trail.emitting = false;
                trail.numCapVertices = 2;
                trail.numCornerVertices = 2;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.alignment = LineAlignment.View;

                // Épaisseur : forte au départ, effilée jusqu'à la pointe.
                AnimationCurve width = new AnimationCurve(
                    new Keyframe(0f, 0.55f),
                    new Keyframe(0.35f, 0.32f),
                    new Keyframe(1f, 0f));
                trail.widthCurve = width;
                trail.widthMultiplier = 1f;

                // Couleur : cyan (drift) → orange (boost), alpha qui s'éteint.
                bool isBlade = i >= 2;
                Color baseColor = isBlade ? driftTrailColor : boostTrailColor;

                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(baseColor, 0f),
                        new GradientColorKey(Color.Lerp(baseColor, Color.white, 0.45f), 0.25f),
                        new GradientColorKey(baseColor * 0.6f, 1f),
                    },
                    new[]
                    {
                        new GradientAlphaKey(0.95f, 0f),
                        new GradientAlphaKey(0.55f, 0.4f),
                        new GradientAlphaKey(0f, 1f),
                    });
                trail.colorGradient = gradient;

                // Matériau additif : les trails "brillent" au lieu de salir.
                Material material = new Material(trailShader) { name = "TrailMat_" + i };
                material.color = Color.white;
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", baseColor);
                trail.sharedMaterial = material;

                trails[i] = trail;
                trailMaterials[i] = material;
            }
        }

        /// <summary>Active/coupe les trails selon l'état (drift, boost, dash).</summary>
        private void UpdateTrails()
        {
            if (trails == null)
                return;

            bool boostActive = boostTimer > 0f;
            bool dashActive = dashTimer > 0f;

            // Lames : allumées en drift (et un peu en boost).
            // Patins : allumés en boost et en esquive.
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null)
                    continue;

                bool isBlade = i >= 2;
                bool emitting = isBlade
                    ? (isDrifting || boostActive)
                    : (boostActive || dashActive || isDrifting);

                trails[i].emitting = emitting;

                // Un trail plus long quand ça va vite = traînée plus spectaculaire.
                trails[i].time = Mathf.Lerp(trailTime * 0.6f, trailTime * 1.8f,
                    Mathf.Clamp01(currentSpeed / maxSpeed));
            }
        }

        /// <summary>
        /// Pilote l'intensité lumineuse des lames, des réacteurs et de la
        /// canopée : la machine raconte son état sans passer par le HUD.
        /// </summary>
        private void UpdateMaterials()
        {
            if (vehicle == null)
                return;

            // --- Lames : brillent avec la charge d'éther ---
            if (vehicle.BladeMaterial != null)
            {
                float baseIntensity = 2.2f;
                float charged = baseIntensity + stats.EtherRatio * 3.4f;
                if (isDrifting)
                    charged += 1.2f * driftIntensity;
                SetEmission(vehicle.BladeMaterial, driftTrailColor, charged);
            }

            // --- Réacteurs : s'embrasent à l'accélération et au boost ---
            if (vehicle.ThrusterMaterial != null)
            {
                float throttle = Mathf.Clamp01(Mathf.Max(0f, throttleInput));
                float intensity = 1.4f + throttle * 2.2f + (boostTimer > 0f ? 4.5f : 0f);
                SetEmission(vehicle.ThrusterMaterial, boostTrailColor, intensity);
            }

            // --- Canopée : flash pendant les i-frames ---
            if (vehicle.CanopyMaterial != null)
            {
                float iFrameGlow = stats.IsInvulnerable ? 2.4f : 0.35f;
                SetEmission(vehicle.CanopyMaterial, new Color(0.2f, 0.75f, 1f), iFrameGlow);
            }
        }

        private void SetEmission(Material material, Color color, float intensity)
        {
            Color emissive = color * intensity;
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emissive);
            if (material.HasProperty("_Emission"))
                material.SetColor("_Emission", emissive);
        }

        // ============================================================
        // HOOKS EXTERNES (boss, pièges, futurs scripts de dégâts)
        // ============================================================

        /// <summary>Vitesse actuelle en m/s (lecture pour l'UI et les boss).</summary>
        public float Speed => currentSpeed;

        /// <summary>Vitesse en km/h (affichage).</summary>
        public float SpeedKmh => currentSpeed * 3.6f;

        /// <summary>Vrai pendant un drift actif.</summary>
        public bool IsDrifting => isDrifting;

        /// <summary>Vrai pendant le boost visuel.</summary>
        public bool IsBoosting => boostTimer > 0f;

        /// <summary>Braquage courant, de -1 (gauche) à +1 (droite). Lu par WheelSpinner.</summary>
        public float SteerInput => steerInput;

        /// <summary>
        /// Applique une poussée externe (explosion de boss, tremplin runique…).
        /// </summary>
        public void AddExternalImpulse(Vector3 impulse)
        {
            rb.velocity += impulse;
            rb.velocity = Vector3.ClampMagnitude(rb.velocity, maxSpeed * boostSpeedMultiplier * 1.2f);
        }

        // ============================================================
        // JAUGES DE TEST (temporaire, remplacé par le HUD à l'Étape 5)
        // ============================================================

        private void OnGUI()
        {
            if (!showDebugGauges || stats == null)
                return;

            // --- Témoin d'entrées : confirme que le clavier est bien lu ---
            GUIStyle inputStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.75f, 0.85f, 1f) }
            };
            GUI.Label(new Rect(24f, Screen.height - 172f, 420f, 22f),
                "THROTTLE " + throttleInput.ToString("+0;-0;0")
                + "   STEER " + steerInput.ToString("+0;-0;0")
                + "   DRIFT " + (driftHeld ? "ON" : "off")
                + "   STATE " + (initialized ? "OK" : "NON INIT")
                + "   GROUND " + (isGrounded ? "yes" : "no")
                + (cameraFollow != null
                    ? "   CAM " + cameraFollow.Yaw.ToString("+0;-0;0") + "°"
                    : "   CAM absente"), inputStyle);

            DrawGauge(new Rect(24f, Screen.height - 96f, 260f, 22f),
                "ÉTHER", stats.EtherRatio, new Color(0.15f, 0.9f, 1f));

            DrawGauge(new Rect(24f, Screen.height - 62f, 260f, 22f),
                "ENDURANCE", stats.StaminaRatio, new Color(1f, 0.65f, 0.2f));

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(24f, Screen.height - 128f, 320f, 24f),
                SpeedKmh.ToString("0") + " km/h" + (isDrifting ? "  —  DRIFT" : ""), style);

            if (stats.IsInvulnerable)
            {
                GUIStyle iFrame = new GUIStyle(style)
                {
                    normal = { textColor = new Color(0.4f, 0.95f, 1f) }
                };
                GUI.Label(new Rect(24f, Screen.height - 150f, 320f, 24f), "I-FRAMES", iFrame);
            }
        }

        private void DrawGauge(Rect rect, string label, float ratio, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            Rect fill = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * Mathf.Clamp01(ratio), rect.height - 4f);
            GUI.color = color;
            GUI.DrawTexture(fill, Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            GUI.Label(new Rect(rect.x + 8f, rect.y - 2f, 200f, 24f), label, style);
        }
    }
}
