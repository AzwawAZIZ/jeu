using UnityEngine;

namespace AetherDrift.Player
{
    /// <summary>
    /// Fait tourner une roue autour de son axe local X, et oriente les roues
    /// avant selon le braquage.
    ///
    /// Pourquoi un script dédié plutôt que du code dans CarController ?
    /// Une roue n'a besoin que de deux informations (vitesse, braquage) : la
    /// garder autonome évite d'alourdir le contrôleur et permet de réutiliser
    /// ce composant sur n'importe quel véhicule, modèle importé ou non.
    /// </summary>
    public class WheelSpinner : MonoBehaviour
    {
        [Tooltip("Rayon de la roue en mètres : détermine la vitesse de rotation. " +
                 "0 = calculé automatiquement depuis les bounds du mesh.")]
        [SerializeField] private float radius = 0f;

        [Tooltip("Cette roue est-elle directrice (tourne avec le braquage) ?")]
        [SerializeField] private bool isSteering = false;

        [Tooltip("Angle de braquage visuel maximal, en degrés.")]
        [SerializeField] private float maxSteerAngle = 26f;

        [Tooltip("Signe de rotation : +1 ou -1 selon l'orientation du mesh.")]
        [SerializeField] private float spinDirection = 1f;

        private CarController controller;
        private Rigidbody vehicleBody;
        private float currentSteer;

        /// <summary>
        /// Configure la roue depuis le code (le modèle est généré à l'exécution :
        /// on ne peut pas passer par l'Inspector).
        /// </summary>
        public void Configure(bool steering, float wheelRadius = 0f, float spin = 1f)
        {
            isSteering = steering;
            if (wheelRadius > 0.01f)
                radius = wheelRadius;
            spinDirection = spin;
        }

        private void Awake()
        {
            // On remonte la hiérarchie pour trouver le contrôleur et le Rigidbody.
            controller = GetComponentInParent<CarController>();
            vehicleBody = GetComponentInParent<Rigidbody>();

            if (radius <= 0.01f)
                radius = EstimateRadius();
        }

        /// <summary>Déduit le rayon de la roue depuis la taille du mesh (axe Y).</summary>
        private float EstimateRadius()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                // Une roue est plus haute que large : on prend la demi-hauteur.
                float height = filter.sharedMesh.bounds.size.y;
                if (height > 0.01f)
                    return height * 0.5f;
            }

            return 0.4f; // valeur de repli plausible
        }

        private void LateUpdate()
        {
            if (vehicleBody == null)
                return;

            // --- Rotation : vitesse réelle projetée sur l'axe avant ---
            float forwardSpeed = Vector3.Dot(vehicleBody.velocity, transform.root.forward);

            // angle (degrés) = distance / rayon, converti en degrés
            float degreesPerSecond = (forwardSpeed / Mathf.Max(0.05f, radius)) * Mathf.Rad2Deg;
            transform.Rotate(Vector3.right, degreesPerSecond * spinDirection * Time.deltaTime, Space.Self);

            // --- Braquage : les roues avant pivotent autour de Y ---
            if (isSteering)
            {
                float targetSteer = controller != null ? controller.SteerInput * maxSteerAngle : 0f;
                currentSteer = Mathf.Lerp(currentSteer, targetSteer, 1f - Mathf.Exp(-12f * Time.deltaTime));

                // On conserve la rotation de roulis (X) et on remplace le lacet (Y).
                Vector3 euler = transform.localEulerAngles;
                transform.localEulerAngles = new Vector3(euler.x, currentSteer, 0f);
            }
        }
    }
}
