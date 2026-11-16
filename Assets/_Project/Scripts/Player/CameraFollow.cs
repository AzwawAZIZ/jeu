using UnityEngine;

namespace AetherDrift.Player
{
    /// <summary>
    /// Caméra de suivi orbitale — ÉTAPES 1 à 3 (refonte « façon GTA »).
    ///
    /// CE QUI CHANGE (retour utilisateur) : la caméra était VERROUILLÉE dans le
    /// dos du véhicule. Impossible de regarder à gauche, à droite, ni de faire
    /// un tour complet. Elle peut maintenant :
    ///
    ///   • tourner à 360° autour de la voiture à la souris,
    ///   • s'incliner de -15° (vue rasante) à +70° (vue plongeante),
    ///   • revenir toute seule derrière le véhicule après un temps de pause,
    ///     ce qui évite de conduire « à l'aveugle » après un coup d'œil,
    ///   • se recaler instantanément derrière la voiture avec une touche.
    ///
    /// POURQUOI PAS UN COLLIDER DE CAMÉRA
    ///   La caméra est plaquée contre la voiture en monde ouvert (ruines, piliers,
    ///   rampes). Une caméra qui traverse un pilier donne un écran noir d'une
    ///   image. On lance donc un raycast depuis le véhicule vers la position
    ///   voulue, et on rapproche la caméra si un obstacle s'interpose.
    ///
    /// DÉTAILS DE MISE EN ŒUVRE
    ///   • La rotation est calculée en ANGLE (yaw/pitch), jamais en quaternion
    ///     absolu : c'est ce qui permet de tourner indéfiniment sans blocage
    ///     aux pôles, et de lisser le recentrage proprement.
    ///   • On lit uniquement « Mouse X », « Mouse Y » et « Mouse ScrollWheel » :
    ///     ce sont des axes INTÉGRÉS à Unity, présents dans tout projet même
    ///     sans InputManager.asset versionné. Si le projet est réglé sur
    ///     « Input System Package (New) » seul, l'ancien Input lève une
    ///     exception : elle est attrapée, signalée en Console, et la caméra
    ///     continue de suivre la voiture (simplement sans orbite).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)] // après la physique et après CarController
    public class CameraFollow : MonoBehaviour
    {
        // ============================================================
        // CIBLE
        // ============================================================

        [Header("Cible")]
        [Tooltip("Transform suivi. Si vide, la caméra cherche le GameObject taggé 'Player'.")]
        [SerializeField] private Transform target;

        [Tooltip("Point regardé, par rapport au centre du véhicule.")]
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.4f, 0f);

        // ============================================================
        // ORBITE (le cœur du nouveau comportement)
        // ============================================================

        [Header("Orbite libre (souris)")]
        [Tooltip("Sensibilité horizontale, en degrés par unité de souris.")]
        [SerializeField] private float yawSensitivity = 4.2f;

        [Tooltip("Sensibilité verticale, en degrés par unité de souris.")]
        [SerializeField] private float pitchSensitivity = 3.0f;

        [Tooltip("Inverse l'axe vertical de la souris.")]
        [SerializeField] private bool invertVertical = false;

        [Tooltip("Angle vertical minimal : en dessous, la caméra passe sous la voiture. " +
                 "-15 = vue légèrement rasante.")]
        [SerializeField] private float minPitch = -15f;

        [Tooltip("Angle vertical maximal : 70 = vue plongeante au-dessus du véhicule.")]
        [SerializeField] private float maxPitch = 70f;

        [Tooltip("Durée d'attente sans mouvement de souris avant que la caméra revienne derrière la voiture (s). " +
                 "0 = jamais.")]
        [SerializeField] private float recenterDelay = 1.6f;

        [Tooltip("Vitesse du recentrage automatique (degrés par seconde).")]
        [SerializeField] private float recenterSpeed = 110f;

        [Tooltip("Ne recentre pas tant que le véhicule roule sous cette vitesse (m/s) : " +
                 "on peut admirer le paysage à l'arrêt.")]
        [SerializeField] private float recenterMinSpeed = 5f;

        [Tooltip("Touche de recalage instantané derrière le véhicule.")]
        [SerializeField] private KeyCode recenterKey = KeyCode.C;

        // ============================================================
        // DISTANCE / HAUTEUR
        // ============================================================

        [Header("Distance")]
        [Tooltip("Distance à l'arrêt (m).")]
        [SerializeField] private float minDistance = 9f;

        [Tooltip("Distance à pleine vitesse (m).")]
        [SerializeField] private float maxDistance = 16f;

        [Tooltip("Vitesse à partir de laquelle la distance est maximale (m/s).")]
        [SerializeField] private float maxSpeedForZoom = 30f;

        [Tooltip("Hauteur de base de la caméra au-dessus du point regardé.")]
        [SerializeField] private float baseHeight = 2.8f;

        [Header("Lissage")]
        [Tooltip("Temps de lissage du déplacement (petit = réactif, grand = mou).")]
        [SerializeField] private float positionSmoothTime = 0.09f;

        [Tooltip("Temps de lissage de l'orientation.")]
        [SerializeField] private float rotationSmoothTime = 0.03f;

        // ============================================================
        // EFFETS
        // ============================================================

        [Header("Effet de vitesse (FOV)")]
        [SerializeField] private float minFov = 60f;
        [SerializeField] private float maxFov = 78f;
        [SerializeField] private float fovSmoothTime = 0.22f;

        [Header("Impacts (boost, esquive, tour synchronisée)")]
        [Tooltip("Amplitude maximale de la secousse, en degrés.")]
        [SerializeField] private float maxShakeAngle = 3.5f;

        [Tooltip("Vitesse d'amortissement de la secousse.")]
        [SerializeField] private float shakeDamping = 6.5f;

        // ============================================================
        // COLLISION
        // ============================================================

        [Header("Anti-traversée de décor")]
        [Tooltip("Rapproche la caméra si un obstacle s'interpose. À décocher si le rendu " +
                 "« saute » près des ruines.")]
        [SerializeField] private bool avoidObstacles = true;

        [Tooltip("Rayon de la sonde : évite que la caméra frôle un mur.")]
        [SerializeField] private float collisionRadius = 0.6f;

        [Tooltip("Distance minimale : la caméra ne colle jamais au véhicule.")]
        [SerializeField] private float minCollisionDistance = 3.5f;


        // ============================================================
        // ÉTAT INTERNE
        // ============================================================

        private Camera cam;
        private Rigidbody targetRigidbody;

        private float yaw;              // angle horizontal autour du véhicule
        private float pitch = 12f;      // angle vertical (12° = léger survol)
        private float currentDistance;
        private float currentFov;

        private static readonly RaycastHit[] obstacleHits = new RaycastHit[16];
        private bool legacyInputAvailable = true;
        private float distanceAdjust;        // décalage de distance demandé à la molette
        private float timeSinceLook = 99f;   // temps écoulé depuis le dernier mouvement de caméra
        private Vector3 positionVelocity;
        private float fovVelocity;
        private float fovKick;
        private float fovKickVelocity;
        private float shakeAmount;

        /// <summary>Angle horizontal courant (lecture/debug).</summary>
        public float Yaw => yaw;

        /// <summary>La caméra est-elle revenue derrière le véhicule ?</summary>
        public bool IsCentered => Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)) < 2f;

        // ============================================================
        // INITIALISATION
        // ============================================================

        private void Awake()
        {
            cam = GetComponent<Camera>();
            currentFov = cam != null ? cam.fieldOfView : minFov;
            currentDistance = minDistance;
        }

        private void Start()
        {
            ResolveTarget();

            if (target != null)
            {
                // On démarre caméra dans le dos du véhicule.
                yaw = 0f;
                pitch = 12f;

                Vector3 anchor = GetLookAtPoint();
                transform.position = anchor + ComputeOrbitOffset(yaw, pitch, currentDistance);
                transform.LookAt(anchor);
            }
        }

        /// <summary>Définit (ou remplace) la cible suivie. Appelé par le Bootstrapper.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            ResolveTarget();
        }

        private void ResolveTarget()
        {
            if (target != null)
            {
                targetRigidbody = target.GetComponent<Rigidbody>();
                return;
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                targetRigidbody = player.GetComponent<Rigidbody>();
            }
        }

        // ============================================================
        // RÉGLAGES PUBLICS (appelés par les autres scripts)
        // ============================================================

        /// <summary>Ajoute une secousse (boost, esquive, impact, tour synchronisée).</summary>
        /// <param name="amount">0 = rien, 1 = secousse maximale.</param>
        public void AddShake(float amount)
        {
            shakeAmount = Mathf.Clamp(shakeAmount + Mathf.Clamp01(amount), 0f, 1.5f);
        }

        /// <summary>Ajoute un coup de FOV : positif = ouverture, négatif = resserrement.</summary>
        public void AddFovKick(float degrees)
        {
            fovKick = Mathf.Clamp(fovKick + degrees, -14f, 22f);
        }

        /// <summary>Recale la caméra dans le dos du véhicule, instantanément.</summary>
        public void RecenterBehindVehicle()
        {
            yaw = 0f;
            timeSinceLook = 99f;
        }

        // ============================================================
        // BOUCLE
        // ============================================================

        private void LateUpdate()
        {
            if (target == null)
            {
                ResolveTarget();
                if (target == null)
                    return;
            }

            float deltaTime = Time.deltaTime;
            float speed = GetTargetSpeed();

            ReadOrbitInput(deltaTime, speed);
            UpdateRecenter(deltaTime, speed);

            // --- Distance : recule quand on accélère ---
            // Le recul automatique ne sert qu'en vue arrière (caméra dans le dos).
            // Si le joueur a tourné la caméra sur le côté ou devant, on garde une
            // distance stable : sinon le cadrage « respire » sans raison.
            float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeedForZoom));
            float centeredFactor = Mathf.Clamp01(Mathf.Cos(yaw * Mathf.Deg2Rad));
            float desiredDistance = Mathf.Lerp(minDistance, maxDistance, speedRatio * centeredFactor) + distanceAdjust;
            desiredDistance = Mathf.Max(minCollisionDistance, desiredDistance);

            currentDistance = Mathf.Lerp(
                currentDistance, desiredDistance, 1f - Mathf.Exp(-4f * deltaTime));

            // --- Position voulue autour du véhicule ---
            Vector3 anchor = GetLookAtPoint();
            Vector3 wantedOffset = ComputeOrbitOffset(yaw, pitch, currentDistance);

            // --- Anti-traversée : on rapproche si un obstacle gêne ---
            float allowedDistance = avoidObstacles
                ? ComputeAllowedDistance(anchor, wantedOffset)
                : currentDistance;

            Vector3 finalPosition = anchor + ComputeOrbitOffset(
                yaw, pitch, Mathf.Max(minCollisionDistance, allowedDistance));

            // --- Jamais sous le terrain ---
            // Avec un pitch très bas ou dans une descente, la caméra pourrait
            // passer sous le sol : on la remonte juste au-dessus du relief.
            float groundHeight = GetGroundHeight(anchor);
            if (finalPosition.y < groundHeight + 1.2f)
                finalPosition.y = groundHeight + 1.2f;

            // --- Lissage de position (sans dépassement) ---
            // Asymétrique et volontaire : quand un obstacle force la caméra à se
            // rapprocher, elle obéit PRESQUE immédiatement (sinon un pilier reste
            // une image ou deux devant l'écran). Quand elle s'éloigne à nouveau,
            // elle revient en douceur.
            bool mustBackOff = finalPosition.sqrMagnitude < transform.position.sqrMagnitude
                               && Vector3.Distance(finalPosition, anchor) < Vector3.Distance(transform.position, anchor);

            float smoothTime = mustBackOff ? positionSmoothTime * 0.15f : positionSmoothTime;

            transform.position = Vector3.SmoothDamp(
                transform.position, finalPosition, ref positionVelocity, smoothTime);

            // --- Regard lissé, puis secousse ---
            // La rotation est reconstruite depuis la position finale de l'image :
            // le lissage ne peut donc pas s'accumuler d'une image à l'autre.
            Quaternion desiredRotation = Quaternion.LookRotation(anchor - finalPosition);
            float rotationT = Mathf.Clamp01(deltaTime / Mathf.Max(0.001f, rotationSmoothTime));
            Quaternion smoothedRotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);

            transform.rotation = AddShakeToRotation(smoothedRotation, deltaTime);

            // --- FOV dynamique + coups ponctuels ---
            UpdateFov(speedRatio, deltaTime);
        }

        // ============================================================
        // ENTRÉES
        // ============================================================

        /// <summary>
        /// Lit la souris et fait tourner l'orbite.
        ///
        /// AXES UTILISÉS : uniquement « Mouse X » et « Mouse Y ». Ce sont des
        /// axes INTÉGRÉS à Unity : ils existent dans tout projet, sans rien
        /// configurer. (Les axes nommés « Horizontal (Right Stick) » ou
        /// « Mouse ScrollWheel » d'un projet personnalisé, eux, n'existent pas
        /// par défaut : les appeler lèverait une ArgumentException.)
        ///
        /// CAS PARTICULIER : si le projet est réglé sur « Input System Package
        /// (Nouveau) », l'ancien Input lève une exception. On l'attrape UNE fois,
        /// on prévient dans la Console, et la caméra reste simplement fixe au
        /// lieu de casser le jeu.
        /// </summary>
        private void ReadOrbitInput(float deltaTime, float speed)
        {
            if (!legacyInputAvailable)
                return;

            float mouseX;
            float mouseY;

            try
            {
                mouseX = Input.GetAxisRaw("Mouse X");
                mouseY = Input.GetAxisRaw("Mouse Y");
            }
            catch (System.Exception exception)
            {
                legacyInputAvailable = false;
                Debug.LogWarning("[AETHER DRIFT] Souris indisponible pour la caméra (" + exception.GetType().Name
                    + "). Passe « Project Settings > Player > Active Input Handling » sur « Both » "
                    + "pour retrouver l'orbite à la souris. La caméra reste fonctionnelle en suivi simple.");
                return;
            }

            bool hasMouseMotion = Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f;

            // Touche de recalage instantané : utilisable même sans bouger la souris.
            if (Input.GetKeyDown(recenterKey))
            {
                RecenterBehindVehicle();
                return;
            }

            // Molette : rapproche / éloigne la caméra (zoom manuel).
            // « Mouse ScrollWheel » fait partie des axes intégrés à Unity.
            float wheel = 0f;
            try
            {
                wheel = Input.GetAxisRaw("Mouse ScrollWheel");
            }
            catch (System.Exception)
            {
                wheel = 0f;   // axe absent (projet personnalisé) : on ignore
            }

            if (Mathf.Abs(wheel) > 0.001f)
                distanceAdjust = Mathf.Clamp(distanceAdjust - wheel * 4f, -6f, 10f);

            if (!hasMouseMotion)
                return;

            if (invertVertical)
                mouseY = -mouseY;

            // Le yaw n'est jamais borné : plusieurs tours complets sont permis.
            // On le ramène simplement dans [-180, 180] pour éviter que la valeur
            // ne grossisse indéfiniment pendant une longue partie.
            yaw = Mathf.Repeat(yaw + mouseX * yawSensitivity + 180f, 360f) - 180f;
            pitch = Mathf.Clamp(pitch - mouseY * pitchSensitivity, minPitch, maxPitch);

            timeSinceLook = 0f;

            // Le zoom automatique reprend la main dès qu'on relâche la molette.
            if (speed < 0.1f)
                currentDistance = minDistance;
        }

        /// <summary>
        /// Ramène la caméra derrière le véhicule après un temps d'inactivité.
        /// Ne le fait qu'en roulant : à l'arrêt, on peut contempler le décor.
        /// </summary>
        private void UpdateRecenter(float deltaTime, float speed)
        {
            timeSinceLook += deltaTime;

            if (recenterDelay <= 0f || timeSinceLook < recenterDelay)
                return;

            if (speed < recenterMinSpeed)
                return;

            // La vitesse de retour est proportionnelle à l'écart : franc quand
            // on est tourné de 180°, doux en fin de course (aucun à-coup).
            float step = Mathf.Max(recenterSpeed * deltaTime, Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)) * 2.2f * deltaTime);
            yaw = Mathf.MoveTowardsAngle(yaw, 0f, step);

            // On ne recentre pas le pitch de force : l'angle vertical choisi par
            // le joueur est conservé, seul le cap revient derrière le véhicule.
        }

        // ============================================================
        // OUTILS INTERNES
        // ============================================================

        /// <summary>
        /// Point que la caméra regarde (au-dessus du véhicule, jamais son centre).
        /// </summary>
        private Vector3 GetLookAtPoint()
        {
            return target.position + lookAtOffset;
        }

        /// <summary>
        /// Convertit (yaw, pitch, distance) en déplacement, dans le repère du
        /// véhicule, depuis le point regardé.
        ///
        /// C'est le cœur de l'orbite : on décrit la position de la caméra sur
        /// une sphère autour de la voiture.
        ///   • yaw   = 0 → pile derrière le véhicule ; 180 → devant ; ±90 → sur les côtés.
        ///   • pitch =  0 → à hauteur du point regardé ; +70 → au-dessus (vue plongeante).
        /// La rotation du véhicule est appliquée à la fin : « derrière » suit
        /// toujours son cap, et tourner la souris reste indépendant de la conduite.
        /// </summary>
        private Vector3 ComputeOrbitOffset(float yawDeg, float pitchDeg, float distance)
        {
            // Direction horizontale : -Z local = arrière du véhicule, pivotée du yaw.
            Quaternion yawRotation = Quaternion.AngleAxis(yawDeg, Vector3.up);
            Vector3 horizontalDirection = yawRotation * Vector3.back;

            float pitchRad = pitchDeg * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(pitchRad) * distance;
            float vertical = Mathf.Sin(pitchRad) * distance + baseHeight;

            Vector3 localOffset = horizontalDirection * horizontal + Vector3.up * vertical;

            // Passage du repère du véhicule au repère monde.
            return target.rotation * localOffset;
        }

        /// <summary>
        /// Hauteur du sol sous un point (pour ne jamais passer sous le terrain).
        /// </summary>
        private float GetGroundHeight(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 200f, Vector3.down, out RaycastHit hit,
                    400f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return position.y - lookAtOffset.y;
        }

        /// <summary>
        /// Distance maximale autorisée entre le véhicule et la caméra, limitée
        /// par le premier obstacle rencontré (pilier, mur, rampe…).
        ///
        /// On utilise SphereCastNonAlloc puis on filtre MANUELLEMENT les
        /// colliders du véhicule : la sphère physique du joueur est sur le layer
        /// « Default », il n'existe aucune couche « Player » par défaut. Un
        /// simple LayerMask ne suffirait donc pas — la caméra se collerait à la
        /// voiture à cause de son propre collider.
        /// </summary>
        private float ComputeAllowedDistance(Vector3 anchor, Vector3 wantedOffset)
        {
            Vector3 direction = wantedOffset.normalized;
            float wantedDistance = wantedOffset.magnitude;

            // SphereCast plutôt que Raycast : une caméra a un volume, un rayon
            // simple passe à travers les arêtes et donne des à-coups.
            int count = Physics.SphereCastNonAlloc(
                anchor, collisionRadius, direction, obstacleHits,
                wantedDistance, ~0, QueryTriggerInteraction.Ignore);

            float closest = wantedDistance;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = obstacleHits[i];

                // Objet sans collider utile (SphereCastNonAlloc renvoie des
                // entrées vides quand il y a moins de résultats que le buffer).
                if (hit.collider == null)
                    continue;

                // On ignore le véhicule lui-même.
                if (hit.collider.transform.IsChildOf(target))
                    continue;

                // hit.distance vaut 0 quand le point de départ est déjà dans le
                // collider : on l'écarte, il ne s'agit pas d'un vrai obstacle.
                if (hit.distance <= 0.01f)
                    continue;

                if (hit.distance < closest)
                    closest = hit.distance;
            }

            return Mathf.Max(minCollisionDistance, closest - 0.15f);
        }

        private void UpdateFov(float speedRatio, float deltaTime)
        {
            if (cam == null)
                return;

            float desiredFov = Mathf.Lerp(minFov, maxFov, speedRatio) + fovKick;
            currentFov = Mathf.SmoothDamp(currentFov, desiredFov, ref fovVelocity, fovSmoothTime);
            cam.fieldOfView = currentFov;

            // Le coup de FOV se résorbe tout seul.
            fovKick = Mathf.SmoothDamp(fovKick, 0f, ref fovKickVelocity, 0.35f);
        }

        /// <summary>
        /// Secousse appliquée à la rotation finale.
        ///
        /// Point important : la secousse est calculée à partir de la rotation
        /// « propre » de cette image, jamais accumulée. L'ancienne version
        /// multipliait la rotation courante (déjà secouée) par la nouvelle
        /// secousse : le bruit s'accumulait et la caméra finissait par pencher.
        /// </summary>
        private Quaternion AddShakeToRotation(Quaternion cleanRotation, float deltaTime)
        {
            if (shakeAmount <= 0.001f)
                return cleanRotation;

            float shake = shakeAmount * maxShakeAngle;
            float noiseX = (Mathf.PerlinNoise(Time.time * 22f, 0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0f, Time.time * 19f) - 0.5f) * 2f;

            shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, shakeDamping * deltaTime);

            return cleanRotation * Quaternion.Euler(noiseX * shake, noiseY * shake, 0f);
        }

        /// <summary>Vitesse actuelle de la cible (m/s). 0 si pas de Rigidbody.</summary>
        private float GetTargetSpeed()
        {
            return targetRigidbody != null ? targetRigidbody.velocity.magnitude : 0f;
        }
    }
}
