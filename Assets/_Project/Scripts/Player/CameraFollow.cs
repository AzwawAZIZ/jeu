using UnityEngine;

namespace AetherDrift.Player
{
    /// <summary>
    /// Caméra de suivi fluide pour le véhicule du joueur.
    /// - Suit la cible en LateUpdate() (après la physique) pour éviter tout tremblement.
    /// - Recule et élargit le champ de vision (FOV) selon la vitesse du véhicule :
    ///   plus on va vite, plus on voit loin (sensation de vitesse).
    /// - Lissage par SmoothDamp : aucun à-coup, même à haute vitesse.
    ///
    /// À l'Étape 1 le véhicule est statique : la caméra reste donc stable derrière lui.
    /// À l'Étape 2 (conduite), le zoom dynamique deviendra visible automatiquement.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Header("Cible")]
        [Tooltip("Transform suivi par la caméra. Si vide, la caméra cherche automatiquement le GameObject taggé 'Player'.")]
        [SerializeField] private Transform target;

        [Header("Position de base (offset local)")]
        [Tooltip("Décalage de la caméra par rapport à la cible (en arrière, en hauteur).")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 6f, -10f);

        [Tooltip("Décalage du point de regard par rapport au centre de la cible.")]
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Lissage")]
        [Tooltip("Temps de lissage du déplacement (plus petit = plus réactif).")]
        [SerializeField] private float positionSmoothTime = 0.12f;

        [Tooltip("Temps de lissage de la rotation / du regard.")]
        [SerializeField] private float rotationSmoothTime = 0.08f;

        [Header("Zoom dynamique selon la vitesse")]
        [Tooltip("Distance minimale (à l'arrêt).")]
        [SerializeField] private float minDistance = 10f;

        [Tooltip("Distance maximale (à pleine vitesse).")]
        [SerializeField] private float maxDistance = 18f;

        [Tooltip("Vitesse à partir de laquelle le zoom est maximal (m/s).")]
        [SerializeField] private float maxSpeedForZoom = 30f;

        [Tooltip("FOV minimal (à l'arrêt).")]
        [SerializeField] private float minFov = 60f;

        [Tooltip("FOV maximal (à pleine vitesse).")]
        [SerializeField] private float maxFov = 78f;

        [Tooltip("Temps de lissage du FOV.")]
        [SerializeField] private float fovSmoothTime = 0.25f;

        [Header("Retours d'impact (boost / esquive)")]
        [Tooltip("Amplitude maximale de la secousse, en degrés.")]
        [SerializeField] private float maxShakeAngle = 3.5f;

        [Tooltip("Vitesse d'amortissement de la secousse.")]
        [SerializeField] private float shakeDamping = 6.5f;

        private Camera cam;
        private Rigidbody targetRigidbody;
        private float shakeAmount;
        private float fovKick;
        private float fovKickVelocity;
        private Vector3 positionVelocity; // état interne de SmoothDamp
        private float currentFov;
        private float fovVelocity;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            currentFov = cam != null ? cam.fieldOfView : minFov;
        }

        private void Start()
        {
            ResolveTarget();

            // Placement immédiat au démarrage (pas de glissement depuis l'origine).
            if (target != null)
            {
                transform.position = target.position + offset.normalized * minDistance;
                transform.LookAt(target.position + lookAtOffset);
            }
        }

        /// <summary>
        /// Ajoute une secousse (boost, esquive, impact de boss).
        /// </summary>
        /// <param name="amount">0 = rien, 1 = secousse maximale.</param>
        public void AddShake(float amount)
        {
            shakeAmount = Mathf.Clamp(shakeAmount + Mathf.Clamp01(amount), 0f, 1.5f);
        }

        /// <summary>
        /// Ajoute un coup de FOV : positif = accélération (ouverture),
        /// négatif = resserrement (esquive).
        /// </summary>
        public void AddFovKick(float degrees)
        {
            fovKick = Mathf.Clamp(fovKick + degrees, -14f, 22f);
        }

        /// <summary>
        /// Définit (ou remplace) la cible suivie. Appelé par le Bootstrapper.
        /// </summary>
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

            // Recherche automatique : le véhicule est taggé "Player" par le Bootstrapper.
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                targetRigidbody = player.GetComponent<Rigidbody>();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                ResolveTarget();
                if (target == null)
                    return;
            }

            float speed = GetTargetSpeed();
            float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeedForZoom));

            // --- Distance : recule quand on accélère ---
            // On zoome le long de la direction de l'offset : la caméra recule ET
            // monte légèrement, ce qui garde le véhicule bien cadré.
            float desiredDistance = Mathf.Lerp(minDistance, maxDistance, speedRatio);
            Vector3 desiredOffset = offset.normalized * desiredDistance;

            Vector3 desiredPosition = target.position + desiredOffset;

            // --- Position lissée (SmoothDamp = fluide, sans dépassement) ---
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime);

            // --- Regard lissé vers la cible ---
            Vector3 lookTarget = target.position + lookAtOffset;
            Vector3 direction = lookTarget - transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(direction);
                float t = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.001f, rotationSmoothTime));
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
            }

            // --- FOV dynamique : sensation de vitesse + coups de FOV ponctuels ---
            if (cam != null)
            {
                float desiredFov = Mathf.Lerp(minFov, maxFov, speedRatio) + fovKick;
                currentFov = Mathf.SmoothDamp(currentFov, desiredFov, ref fovVelocity, fovSmoothTime);
                cam.fieldOfView = currentFov;

                // Le coup de FOV se résorbe tout seul.
                fovKick = Mathf.SmoothDamp(fovKick, 0f, ref fovKickVelocity, 0.35f);
            }

            // --- Secousse : bruit appliqué en rotation, jamais en position ---
            // (une secousse de position casse le lissage et donne mal au cœur)
            if (shakeAmount > 0.001f)
            {
                float shake = shakeAmount * maxShakeAngle;
                float noiseX = (Mathf.PerlinNoise(Time.time * 22f, 0f) - 0.5f) * 2f;
                float noiseY = (Mathf.PerlinNoise(0f, Time.time * 19f) - 0.5f) * 2f;

                transform.rotation *= Quaternion.Euler(noiseX * shake, noiseY * shake, 0f);
                shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, shakeDamping * Time.deltaTime);
            }
        }

        /// <summary>
        /// Vitesse actuelle de la cible (m/s). 0 si pas de Rigidbody.
        /// </summary>
        private float GetTargetSpeed()
        {
            if (targetRigidbody != null)
                return targetRigidbody.velocity.magnitude;

            // Fallback : pas de Rigidbody → la cible est considérée immobile.
            return 0f;
        }
    }
}
