using UnityEngine;
using AetherDrift.Core;
using AetherDrift.Player;

namespace AetherDrift.Enemies
{
    /// <summary>
    /// Véhicule chasseur — ÉTAPE 3.
    ///
    /// COMPORTEMENT (ADN poursuite GTA)
    ///   1. APPROCHE : roule vers le joueur à vitesse constante.
    ///   2. CHARGE   : quand il est assez près et aligné, il accélère d'un coup
    ///                 pour tenter de le percuter.
    ///   3. RÉCUPÉRATION : après une charge ratée, il freine, se replace, et
    ///                 repart (temps de récupération = fenêtre pour le joueur).
    ///
    /// PHYSIQUE
    ///   Même approche que le joueur : sphère physique invisible + forces.
    ///   Le chasseur n'a pas besoin du modèle importé : un châssis low-poly
    ///   procédural anguleux suffit, et reste lisible de loin (silhouette
    ///   contrastée, œil rouge).
    ///
    /// DÉGÂTS
    ///   La collision est détectée par <see cref="OnCollisionEnter"/>. Un coup
    ///   n'est compté que si la vitesse relative est suffisante, et il existe un
    ///   temps de récupération pour éviter d'enchaîner plusieurs impacts sur un
    ///   seul contact. Les i-frames du joueur (PlayerStats) sont respectées :
    ///   une esquive au bon moment annule le coup.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ChaserAI : MonoBehaviour
    {
        // ============================================================
        // RÉGLAGES
        // ============================================================

        [Header("Mouvement")]
        [Tooltip("Vitesse de croisière en poursuite (m/s).")]
        [SerializeField] private float cruiseSpeed = 26f;

        [Tooltip("Vitesse maximale pendant une charge.")]
        [SerializeField] private float chargeSpeed = 40f;

        [Tooltip("Accélération.")]
        [SerializeField] private float acceleration = 24f;

        [Tooltip("Vitesse de rotation vers la cible (degrés/seconde).")]
        [SerializeField] private float turnRate = 110f;

        [Header("Charge")]
        [Tooltip("Distance à laquelle démarre une charge.")]
        [SerializeField] private float chargeRange = 26f;

        [Tooltip("Distance sous laquelle la charge est déclenchée si l'alignement est bon.")]
        [SerializeField] private float chargeEngageRange = 16f;

        [Tooltip("Durée d'une charge (secondes).")]
        [SerializeField] private float chargeDuration = 1.1f;

        [Tooltip("Temps de récupération après une charge (secondes).")]
        [SerializeField] private float recoveryDuration = 1.6f;

        [Header("Dégâts")]
        [Tooltip("Dégâts infligés lors d'une percussion.")]
        [SerializeField] private float ramDamage = 18f;

        [Tooltip("Vitesse relative minimale pour compter un coup (m/s).")]
        [SerializeField] private float minImpactSpeed = 7f;

        [Header("Apparence")]
        [Tooltip("Couleur de la carrosserie du chasseur.")]
        [SerializeField] private Color bodyColor = new Color(0.22f, 0.10f, 0.12f);

        [Tooltip("Couleur de l'œil / des lames du chasseur.")]
        [SerializeField] private Color eyeColor = new Color(1f, 0.18f, 0.15f);

        // ============================================================
        // ÉTAT
        // ============================================================

        private enum State { Approach, Charging, Recovering }

        private Rigidbody rb;
        private Transform target;
        private PlayerStats targetStats;

        private Transform visualChassis;
        private State state = State.Approach;
        private float stateTimer;
        private float impactCooldown;

        /// <summary>Le chasseur est-il encore actif ?</summary>
        public bool IsActive { get; private set; }

        /// <summary>Distance actuelle au joueur (lecture pour l'UI).</summary>
        public float DistanceToTarget { get; private set; }

        // ============================================================
        // CRÉATION
        // ============================================================

        /// <summary>
        /// Fabrique un chasseur complet à la position donnée.
        /// </summary>
        public static ChaserAI Create(Transform parent, Vector3 position, Transform target)
        {
            GameObject chaserObject = new GameObject("Chaser");
            chaserObject.transform.SetParent(parent, false);
            chaserObject.transform.position = position;
            chaserObject.tag = "Untagged";

            Rigidbody rb = chaserObject.AddComponent<Rigidbody>();
            rb.mass = 900f;
            rb.drag = 0.2f;
            rb.angularDrag = 2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // Aucune rotation physique : l'orientation est pilotée par le script.
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            // Sphère physique invisible (même principe que le joueur).
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "PhysicsSphere (invisible)";
            sphere.transform.SetParent(chaserObject.transform, false);
            sphere.transform.localPosition = new Vector3(0f, 1f, 0f);
            sphere.transform.localScale = Vector3.one * 2.2f;
            MeshRenderer sphereRenderer = sphere.GetComponent<MeshRenderer>();
            if (sphereRenderer != null)
                sphereRenderer.enabled = false;

            ChaserAI chaser = chaserObject.AddComponent<ChaserAI>();
            chaser.target = target;
            chaser.targetStats = target != null ? target.GetComponent<PlayerStats>() : null;
            chaser.BuildVisual();
            chaser.IsActive = true;

            return chaser;
        }

        /// <summary>
        /// Châssis procédural : coin agressif, aileron arrière, œil rouge.
        /// Volontairement anguleux pour se distinguer du véhicule du joueur.
        /// </summary>
        private void BuildVisual()
        {
            GameObject visual = new GameObject("VisualChassis");
            visual.transform.SetParent(transform, false);
            visualChassis = visual.transform;

            Material bodyMat = ProceduralMaterials.Create("ChaserBody", bodyColor, Color.black, 0f, 0.5f, 0.3f);
            Material eyeMat = ProceduralMaterials.Create("ChaserEye", eyeColor, eyeColor, 3.2f, 0.7f, 0f);

            LowPolyMesh mesh = new LowPolyMesh();

            // --- Coque : prisme trapézoïdal, large à l'arrière, pincé à l'avant ---
            //
            // Construite à la main (8 sommets, 6 faces) plutôt qu'avec des
            // boîtes empilées : on obtient une silhouette en coin, immédiatement
            // reconnaissable dans le rétroviseur.
            const float bottomY = 0.35f;
            const float topY = 0.9f;

            Vector3[] bottom =
            {
                new Vector3(-1.05f, bottomY, -2.0f),
                new Vector3( 1.05f, bottomY, -2.0f),
                new Vector3( 0.80f, bottomY,  1.30f),
                new Vector3(-0.80f, bottomY,  1.30f),
            };

            Vector3[] top =
            {
                new Vector3(-0.70f, topY, -1.70f),
                new Vector3( 0.70f, topY, -1.70f),
                new Vector3( 0.30f, topY,  0.80f),
                new Vector3(-0.30f, topY,  0.80f),
            };

            // Point intérieur de la coque : sert à orienter les normales.
            Vector3 inside = new Vector3(0f, (bottomY + topY) * 0.5f, -0.3f);

            mesh.AddQuad(bottom[3], bottom[2], bottom[1], bottom[0], inside);   // dessous
            mesh.AddQuad(bottom[0], bottom[1], top[1], top[0], inside);         // arrière
            mesh.AddQuad(bottom[1], bottom[2], top[2], top[1], inside);         // flanc droit
            mesh.AddQuad(bottom[2], bottom[3], top[3], top[2], inside);         // avant
            mesh.AddQuad(bottom[3], bottom[0], top[0], top[3], inside);         // flanc gauche
            mesh.AddQuad(top[0], top[1], top[2], top[3], inside);               // dessus

            // --- Nez pointu ---
            mesh.AddCone(new Vector3(0f, 0.62f, 1.2f), new Vector3(0f, 0.58f, 2.6f), 0.42f, 4, true);

            // --- Aileron arrière en V ---
            MeshUtils.AddBox(mesh, new Vector3(-0.85f, 1.15f, -1.65f), new Vector3(0.12f, 1.0f, 0.75f),
                Quaternion.Euler(0f, 0f, 20f));
            MeshUtils.AddBox(mesh, new Vector3(0.85f, 1.15f, -1.65f), new Vector3(0.12f, 1.0f, 0.75f),
                Quaternion.Euler(0f, 0f, -20f));

            // --- Lames latérales : la "menace" se lit de loin ---
            MeshUtils.AddBox(mesh, new Vector3(-1.05f, 0.5f, 0.2f), new Vector3(0.9f, 0.16f, 0.3f),
                Quaternion.Euler(0f, 12f, 0f));
            MeshUtils.AddBox(mesh, new Vector3(1.05f, 0.5f, 0.2f), new Vector3(0.9f, 0.16f, 0.3f),
                Quaternion.Euler(0f, -12f, 0f));

            GameObject bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(visual.transform, false);
            bodyObject.AddComponent<MeshFilter>().sharedMesh = mesh.Build("ChaserBody");
            bodyObject.AddComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // Œil unique à l'avant : la "menace" se lit de loin.
            LowPolyMesh eyeMesh = new LowPolyMesh();
            // Barre lumineuse à la naissance du nez : visible de face ET de profil.
            MeshUtils.AddBox(eyeMesh, new Vector3(0f, 0.72f, 1.05f), new Vector3(1.0f, 0.12f, 0.22f));
            MeshUtils.AddBox(eyeMesh, new Vector3(0f, 0.55f, 1.75f), new Vector3(0.3f, 0.10f, 0.5f),
                Quaternion.Euler(12f, 0f, 0f));

            GameObject eyeObject = new GameObject("Eye");
            eyeObject.transform.SetParent(visual.transform, false);
            eyeObject.AddComponent<MeshFilter>().sharedMesh = eyeMesh.Build("ChaserEye");
            MeshRenderer eyeRenderer = eyeObject.AddComponent<MeshRenderer>();
            eyeRenderer.sharedMaterial = eyeMat;
            eyeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ============================================================
        // BOUCLE
        // ============================================================

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (!IsActive || target == null)
                return;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            DistanceToTarget = toTarget.magnitude;

            UpdateState();

            // --- Orientation : on tourne vers la cible, avec inertie ---
            if (toTarget.sqrMagnitude > 0.1f)
            {
                Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, desired, turnRate * Time.fixedDeltaTime);
            }

            // --- Vitesse cible selon l'état ---
            float desiredSpeed = GetDesiredSpeed();
            Vector3 forward = transform.forward;
            float forwardSpeed = Vector3.Dot(rb.velocity, forward);

            UpdateVisualTilt(toTarget);

            if (forwardSpeed < desiredSpeed)
            {
                // Accélère vers l'avant, sans dépasser la vitesse de l'état.
                rb.AddForce(forward * acceleration, ForceMode.Acceleration);
            }
            else if (forwardSpeed > desiredSpeed + 2f)
            {
                // Freine si trop rapide (récupération).
                rb.AddForce(-forward * acceleration * 0.8f, ForceMode.Acceleration);
            }

            // Adhérence latérale : une voiture qui glisse ne peut pas viser.
            Vector3 right = transform.right;
            float lateral = Vector3.Dot(rb.velocity, right);
            rb.AddForce(-right * (lateral * 6f), ForceMode.Acceleration);

            if (impactCooldown > 0f)
                impactCooldown -= Time.fixedDeltaTime;

            // Sécurité : un chasseur distancé de plus de 400 m est retiré.
            // Sans ça, il resterait coincé contre un mur invisible pour l'éternité.
            if (DistanceToTarget > 400f)
            {
                Debug.Log("[AETHER DRIFT] Chasseur distancé : retrait.");
                Despawn();
            }
        }

        /// <summary>
        /// Incline le châssis selon le sol et le virage.
        /// Sans ça, le chasseur paraît « flotter » au-dessus des rampes.
        /// </summary>
        private void UpdateVisualTilt(Vector3 toTarget)
        {
            if (visualChassis == null)
                return;

            // Roulis : proportionnel à l'angle entre le cap et la cible.
            float roll = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
                roll = Mathf.Clamp(Vector3.SignedAngle(transform.forward, toTarget.normalized, Vector3.up) * 0.15f, -10f, 10f);

            // Tangage : suit la pente sous le véhicule.
            float pitch = 0f;
            if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 4f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 groundForward = Vector3.ProjectOnPlane(transform.forward, hit.normal);
                if (groundForward.sqrMagnitude > 0.01f)
                {
                    float slope = Vector3.SignedAngle(
                        new Vector3(transform.forward.x, 0f, transform.forward.z),
                        new Vector3(groundForward.x, 0f, groundForward.z),
                        transform.right);
                    pitch = Mathf.Clamp(slope, -25f, 25f);
                }
            }

            Quaternion target = Quaternion.Euler(pitch, 0f, roll);
            visualChassis.localRotation = Quaternion.Slerp(visualChassis.localRotation, target, 8f * Time.fixedDeltaTime);
        }

        /// <summary>Transitions d'état : approche → charge → récupération → approche.</summary>
        private void UpdateState()
        {
            stateTimer -= Time.fixedDeltaTime;

            switch (state)
            {
                case State.Approach:
                    // Déclenche une charge si assez proche et bien aligné.
                    if (DistanceToTarget < chargeRange)
                    {
                        float alignment = Vector3.Dot(transform.forward, (target.position - transform.position).normalized);
                        bool closeEnough = DistanceToTarget < chargeEngageRange;

                        if (closeEnough || alignment > 0.85f)
                        {
                            state = State.Charging;
                            stateTimer = chargeDuration;
                        }
                    }
                    break;

                case State.Charging:
                    if (stateTimer <= 0f || DistanceToTarget < 3.5f)
                    {
                        state = State.Recovering;
                        stateTimer = recoveryDuration;
                    }
                    break;

                case State.Recovering:
                    if (stateTimer <= 0f)
                    {
                        state = State.Approach;
                    }
                    break;
            }
        }

        private float GetDesiredSpeed()
        {
            switch (state)
            {
                case State.Charging: return chargeSpeed;
                case State.Recovering: return cruiseSpeed * 0.35f;
                default: return cruiseSpeed;
            }
        }

        // ============================================================
        // IMPACT
        // ============================================================

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsActive || target == null || impactCooldown > 0f)
                return;

            // Seul le joueur compte.
            if (!collision.collider.transform.IsChildOf(target) && collision.transform != target)
                return;

            float relativeSpeed = collision.relativeVelocity.magnitude;
            if (relativeSpeed < minImpactSpeed)
                return;

            impactCooldown = 1.2f;

            // Les i-frames du joueur sont respectées : une esquive annule le coup.
            if (targetStats != null)
            {
                bool applied = targetStats.TryTakeDamage(ramDamage);

                if (!applied)
                {
                    Debug.Log("[AETHER DRIFT] Percussion esquivée (i-frames) à " + relativeSpeed.ToString("0") + " m/s.");
                }
                else
                {
                    Debug.Log("[AETHER DRIFT] Percussion subie : " + ramDamage + " dégâts à " + relativeSpeed.ToString("0") + " m/s.");
                }
            }

            // Le chasseur est projeté : il recule après le choc.
            Vector3 push = (transform.position - target.position).normalized;
            rb.AddForce(push * 6f, ForceMode.VelocityChange);

            state = State.Recovering;
            stateTimer = recoveryDuration;
        }

        /// <summary>Désactive proprement le chasseur (fin de contrat).</summary>
        public void Despawn()
        {
            IsActive = false;
            Destroy(gameObject);
        }
    }
}
