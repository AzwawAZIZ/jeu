using UnityEngine;

namespace AetherDrift.Player
{
    /// <summary>
    /// Véhicule runique du joueur — ÉTAPE 1 (base physique + visuel).
    ///
    /// Principe (demandé dans le cahier des charges) :
    /// - Une SPHÈRE INVISIBLE porte le Rigidbody et le SphereCollider :
    ///   c'est elle qui interagit avec le monde (collisions, rebonds).
    /// - Un CHÂSSIS VISUEL en primitives (boîtes, sphère, cylindres) est parenté
    ///   à la sphère : il suit automatiquement ses mouvements.
    /// - Les LAMES LATÉRALES sont des boîtes fines avec matériau émissif cyan :
    ///   elles brilleront plus fort pendant le drift (Étape 2).
    ///
    /// ⚠️ ÉTAPE 1 : le véhicule est STATIQUE (pas de conduite).
    /// La conduite arcade, le drift, la jauge d'endurance/boost et le dash
    /// d'invulnérabilité seront ajoutés à l'Étape 2 dans ce même script.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerVehicle : MonoBehaviour
    {
        [Header("Physique (Étape 1)")]
        [Tooltip("Masse du véhicule. Plus lourde = plus stable, moins réactive.")]
        [SerializeField] private float mass = 1200f;

        [Tooltip("Rayon de la sphère physique invisible.")]
        [SerializeField] private float physicsRadius = 1.2f;

        [Header("Visuel")]
        [Tooltip("Couleur principale du châssis.")]
        [SerializeField] private Color bodyColor = new Color(0.16f, 0.18f, 0.22f);

        [Tooltip("Couleur des lames énergétiques latérales.")]
        [SerializeField] private Color bladeColor = new Color(0.1f, 0.9f, 1f);

        [Tooltip("Intensité de l'émission des lames (au repos).")]
        [SerializeField] private float bladeEmissionIntensity = 2.2f;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            // --- Configuration physique de base ---
            rb.mass = mass;
            rb.useGravity = true;
            rb.drag = 0.1f;
            rb.angularDrag = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate; // fluidité visuelle
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // pas de traversée à haute vitesse

            // À l'Étape 1 on fige la ROTATION (le véhicule ne bascule pas)
            // mais on laisse la position libre : il repose sur le sol.
            // (L'Étape 2 remplacera ce verrouillage par la conduite.)
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.isKinematic = false;

            BuildPhysicsSphere();
            BuildVisualChassis();
        }

        /// <summary>
        /// Crée la sphère physique invisible : SphereCollider + MeshRenderer désactivé.
        /// Le Rigidbody est porté par le GameObject racine (ce script).
        /// </summary>
        private void BuildPhysicsSphere()
        {
            GameObject physicsSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            physicsSphere.name = "PhysicsSphere (invisible)";
            physicsSphere.transform.SetParent(transform, false);
            physicsSphere.transform.localPosition = new Vector3(0f, physicsRadius, 0f);
            physicsSphere.transform.localScale = Vector3.one * (physicsRadius * 2f);

            // Invisible : on coupe le rendu, on garde la collision.
            MeshRenderer renderer = physicsSphere.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = false;

            // Le collider de la primitive Sphere est déjà un SphereCollider : on ajuste son rayon.
            SphereCollider collider = physicsSphere.GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.radius = 0.5f; // 0.5 car la sphère primitive fait 1m de diamètre avant scale
                collider.center = Vector3.zero;
            }
        }

        /// <summary>
        /// Construit le châssis visuel en primitives :
        /// corps principal, cockpit, nez avant, lames latérales lumineuses,
        /// patins anti-gravité et propulseur arrière.
        /// Aucune pièce visuelle ne porte de collider (seule la sphère physique collisionne).
        /// </summary>
        private void BuildVisualChassis()
        {
            GameObject visualRoot = new GameObject("VisualChassis");
            visualRoot.transform.SetParent(transform, false);
            visualRoot.transform.localPosition = new Vector3(0f, 0.55f, 0f);

            Material bodyMat = CreateMaterial(bodyColor, Color.black, 0f);
            Material darkMat = CreateMaterial(new Color(0.08f, 0.09f, 0.11f), Color.black, 0f);
            Material bladeMat = CreateMaterial(bladeColor, bladeColor, bladeEmissionIntensity);
            Material glowMat = CreateMaterial(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.45f, 0.1f), 1.6f);

            // --- Corps principal : boîte aplatie et allongée ---
            CreatePart(PrimitiveType.Cube, "Body", visualRoot.transform,
                new Vector3(0f, 0.25f, 0f), new Vector3(2.2f, 0.5f, 3.6f), bodyMat);

            // --- Nez avant : boîte plus étroite, légèrement inclinée ---
            GameObject nose = CreatePart(PrimitiveType.Cube, "Nose", visualRoot.transform,
                new Vector3(0f, 0.18f, 2.1f), new Vector3(1.5f, 0.35f, 1.2f), bodyMat);
            nose.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);

            // --- Cockpit : sphère écrasée sur le dessus ---
            CreatePart(PrimitiveType.Sphere, "Cockpit", visualRoot.transform,
                new Vector3(0f, 0.55f, -0.3f), new Vector3(1.1f, 0.6f, 1.4f), darkMat);

            // --- Lames latérales lumineuses (signature du bolide runique) ---
            CreatePart(PrimitiveType.Cube, "LeftBlade", visualRoot.transform,
                new Vector3(-1.35f, 0.25f, 0.2f), new Vector3(0.18f, 0.28f, 3.2f), bladeMat);
            CreatePart(PrimitiveType.Cube, "RightBlade", visualRoot.transform,
                new Vector3(1.35f, 0.25f, 0.2f), new Vector3(0.18f, 0.28f, 3.2f), bladeMat);

            // Pointes de lames (petits prismes visuels à l'avant)
            CreatePart(PrimitiveType.Cube, "LeftBladeTip", visualRoot.transform,
                new Vector3(-1.35f, 0.25f, 2.0f), new Vector3(0.18f, 0.28f, 0.5f), bladeMat);
            CreatePart(PrimitiveType.Cube, "RightBladeTip", visualRoot.transform,
                new Vector3(1.35f, 0.25f, 2.0f), new Vector3(0.18f, 0.28f, 0.5f), bladeMat);

            // --- Patins anti-gravité : 4 cylindres sous le châssis ---
            Vector3[] padPositions = new[]
            {
                new Vector3(-0.9f, -0.15f, 1.4f),
                new Vector3(0.9f, -0.15f, 1.4f),
                new Vector3(-0.9f, -0.15f, -1.4f),
                new Vector3(0.9f, -0.15f, -1.4f),
            };
            for (int i = 0; i < padPositions.Length; i++)
            {
                CreatePart(PrimitiveType.Cylinder, "HoverPad_" + i, visualRoot.transform,
                    padPositions[i], new Vector3(0.35f, 0.25f, 0.35f), bladeMat);
            }

            // --- Propulseur arrière : cylindre horizontal lumineux orange ---
            GameObject thruster = CreatePart(PrimitiveType.Cylinder, "Thruster", visualRoot.transform,
                new Vector3(0f, 0.25f, -1.95f), new Vector3(0.6f, 0.5f, 0.6f), glowMat);
            thruster.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // --- Aileron arrière ---
            GameObject spoiler = CreatePart(PrimitiveType.Cube, "Spoiler", visualRoot.transform,
                new Vector3(0f, 0.65f, -1.7f), new Vector3(2.0f, 0.12f, 0.5f), darkMat);
            spoiler.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
        }

        /// <summary>
        /// Crée une pièce visuelle (primitive) sans collider, avec matériau donné.
        /// </summary>
        private GameObject CreatePart(PrimitiveType type, string partName, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            // Les pièces visuelles ne doivent PAS collisionner : seul la sphère physique le fait.
            Collider col = part.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false; // désactivation immédiate (avant la 1re passe physique)
                Destroy(col);
            }

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            return part;
        }

        /// <summary>
        /// Crée un matériau compatible Built-in, URP et HDRP :
        /// essaie le shader URP/Lit, sinon Standard, avec émission optionnelle.
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
                // L'intensité se joue sur la couleur d'émission (compatible Standard + URP).
                mat.SetColor("_EmissionColor", emissionColor * emissionIntensity);
                mat.SetColor("_Emission", emissionColor * emissionIntensity);
            }

            return mat;
        }
    }
}
