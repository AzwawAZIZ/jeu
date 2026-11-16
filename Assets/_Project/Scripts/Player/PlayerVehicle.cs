using UnityEngine;
using AetherDrift.Core;

namespace AetherDrift.Player
{
    /// <summary>
    /// Véhicule runique du joueur — ÉTAPE 1 (physique + rendu procédural).
    ///
    /// ARCHITECTURE (demandée au cahier des charges)
    /// - La racine porte le Rigidbody ; une SPHÈRE invisible (SphereCollider,
    ///   MeshRenderer désactivé) est la seule surface de collision.
    /// - Le CHÂSSIS VISUEL est un maillage low-poly généré à la main dans
    ///   LowPolyMesh.cs : fuselage effilé, ailerons, cockpits, lames, réacteurs.
    ///   Aucun collider sur les pièces visuelles.
    ///
    /// GÉOMÉTRIE : repère local du véhicule
    ///   +Z = avant, -Z = arrière, +Y = haut, +X = droite.
    ///
    /// ⚠️ ÉTAPE 1 : véhicule statique (pas de conduite). Le drift, la jauge
    /// d'endurance et le dash arrivent à l'Étape 2, dans ce même script.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerVehicle : MonoBehaviour
    {
        [Header("Physique (Étape 1)")]
        [Tooltip("Masse du véhicule. Plus lourde = plus stable, moins réactive.")]
        [SerializeField] private float mass = 1200f;

        [Tooltip("Rayon de la sphère physique invisible.")]
        [SerializeField] private float physicsRadius = 1.1f;

        [Header("Livrée / couleurs")]
        [Tooltip("Couleur de la carrosserie.")]
        [SerializeField] private Color bodyColor = new Color(0.13f, 0.15f, 0.19f);

        [Tooltip("Couleur des plaques d'armure (plus claire).")]
        [SerializeField] private Color armorColor = new Color(0.27f, 0.30f, 0.35f);

        [Tooltip("Couleur d'accent (liserés).")]
        [SerializeField] private Color accentColor = new Color(0.85f, 0.45f, 0.15f);

        [Tooltip("Couleur des lames énergétiques latérales.")]
        [SerializeField] private Color bladeColor = new Color(0.15f, 0.9f, 1f);

        [Tooltip("Couleur des réacteurs arrière.")]
        [SerializeField] private Color thrusterColor = new Color(1f, 0.5f, 0.12f);

        [Tooltip("Intensité d'émission des éléments lumineux.")]
        [SerializeField] private float emissionIntensity = 2.6f;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            // --- Physique ---
            rb.mass = mass;
            rb.useGravity = true;
            rb.drag = 0.1f;
            rb.angularDrag = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Étape 1 : la rotation est figée, la position reste libre (le véhicule se pose).
            // L'Étape 2 remplacera ce verrouillage par la conduite.
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.isKinematic = false;

            BuildPhysicsSphere();
            BuildVisualChassis();
        }

        // ============================================================
        // PHYSIQUE
        // ============================================================

        /// <summary>
        /// Sphère invisible : MeshRenderer coupé, SphereCollider conservé.
        /// C'est la SEULE surface de collision du véhicule.
        /// </summary>
        private void BuildPhysicsSphere()
        {
            GameObject physicsSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            physicsSphere.name = "PhysicsSphere (invisible)";
            physicsSphere.transform.SetParent(transform, false);
            physicsSphere.transform.localPosition = new Vector3(0f, physicsRadius, 0f);
            physicsSphere.transform.localScale = Vector3.one * (physicsRadius * 2f);

            MeshRenderer renderer = physicsSphere.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = false;

            SphereCollider collider = physicsSphere.GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.radius = 0.5f; // la primitive fait 1 m avant mise à l'échelle
                collider.center = Vector3.zero;
            }
        }

        // ============================================================
        // CHÂSSIS VISUEL
        // ============================================================

        /// <summary>
        /// Assemble le bolide : un maillage low-poly pour la coque,
        /// plus quelques pièces générées séparément pour varier les matériaux.
        /// </summary>
        private void BuildVisualChassis()
        {
            GameObject visualRoot = new GameObject("VisualChassis");
            visualRoot.transform.SetParent(transform, false);
            visualRoot.transform.localPosition = new Vector3(0f, 0.15f, 0f);

            // --- Matériaux ---
            Material bodyMat = ProceduralMaterials.Create("Body", bodyColor, Color.black, 0f, 0.55f, 0.35f);
            Material armorMat = ProceduralMaterials.Create("Armor", armorColor, Color.black, 0f, 0.35f, 0.15f);
            Material accentMat = ProceduralMaterials.Create("Accent", accentColor, accentColor, emissionIntensity * 0.5f, 0.4f, 0.2f);
            Material bladeMat = ProceduralMaterials.Create("Blade", bladeColor, bladeColor, emissionIntensity, 0.8f, 0f);
            Material thrusterMat = ProceduralMaterials.Create("Thruster", thrusterColor, thrusterColor, emissionIntensity, 0.6f, 0f);
            Material canopyMat = ProceduralMaterials.Create("Canopy", new Color(0.05f, 0.08f, 0.11f), new Color(0.1f, 0.5f, 0.7f), 0.6f, 0.95f, 0.2f);

            // --- 1. Coque principale (un seul maillage, facettes assumées) ---
            BuildHull(visualRoot.transform, bodyMat);

            // --- 2. Plaques d'armure latérales (maillage séparé, matériau plus clair) ---
            BuildArmorPlates(visualRoot.transform, armorMat);

            // --- 3. Verrière (canopée) ---
            GameObject canopy = CreateMeshPart("Canopy", visualRoot.transform, canopyMat);
            LowPolyMesh canopyMesh = new LowPolyMesh();
            canopyMesh.AddFrustum(new Vector3(0f, 0.62f, 0.35f), 0.62f, 0.30f, 0.45f, 6, false, true, 30f);
            canopyMesh.AddFrustum(new Vector3(0f, 0.62f, 0.75f), 0.30f, 0.0f, 0.28f, 6, false, false, 30f);
            ApplyMesh(canopy, canopyMesh, "Canopy");

            // --- 4. Lames latérales énergétiques (signature du bolide) ---
            BuildBlades(visualRoot.transform, bladeMat);

            // --- 5. Réacteurs arrière + cônes de poussée ---
            BuildThrusters(visualRoot.transform, thrusterMat, armorMat);

            // --- 6. Patins anti-gravité sous la coque ---
            BuildHoverPads(visualRoot.transform, bladeMat);

            // --- 7. Liseré d'accent sur le nez ---
            BuildNoseAccent(visualRoot.transform, accentMat);
        }

        /// <summary>
        /// Coque : profil effilé à l'avant, large à l'arrière, facetté.
        /// Construite en "stations" (sections transversales) reliées par des quads.
        /// </summary>
        private void BuildHull(Transform parent, Material material)
        {
            GameObject hull = CreateMeshPart("Hull", parent, material);
            LowPolyMesh mesh = new LowPolyMesh();

            // Chaque section : position Z, demi-largeur X, hauteur Y (bas), hauteur Y (haut)
            // Profil : nez fin → cockpit large → bloc moteur arrière.
            HullSection[] sections =
            {
                new HullSection(-2.20f, 0.62f, 0.10f, 0.38f), // arrière (moteurs)
                new HullSection(-1.40f, 0.95f, 0.02f, 0.52f), // bloc principal
                new HullSection(-0.40f, 1.02f, 0.00f, 0.55f), // milieu
                new HullSection( 0.80f, 0.86f, 0.04f, 0.44f), // avant du cockpit
                new HullSection( 1.70f, 0.55f, 0.08f, 0.26f), // nez
                new HullSection( 2.25f, 0.14f, 0.12f, 0.16f), // pointe
            };

            // Pour chaque section on définit 4 points de contour (gauche-bas, droite-bas,
            // droite-haut, gauche-haut) → 4 faces par tronçon.
            Vector3[][] rings = new Vector3[sections.Length][];
            for (int i = 0; i < sections.Length; i++)
            {
                HullSection s = sections[i];
                rings[i] = new Vector3[]
                {
                    new Vector3(-s.halfWidth, s.bottom, s.z),
                    new Vector3( s.halfWidth, s.bottom, s.z),
                    new Vector3( s.halfWidth * 0.72f, s.top, s.z),
                    new Vector3(-s.halfWidth * 0.72f, s.top, s.z),
                };
            }

            // Centre de la forme (pour orienter les normales vers l'extérieur).
            Vector3 inside = new Vector3(0f, 0.25f, 0f);

            for (int i = 0; i < rings.Length - 1; i++)
            {
                Vector3[] a = rings[i];
                Vector3[] b = rings[i + 1];

                for (int k = 0; k < 4; k++)
                {
                    int next = (k + 1) % 4;
                    mesh.AddQuad(a[k], a[next], b[next], b[k], inside);
                }
            }

            // Bouchons avant/arrière (fermeture de la coque).
            mesh.AddQuad(rings[0][0], rings[0][3], rings[0][2], rings[0][1], inside);
            mesh.AddQuad(rings[rings.Length - 1][0], rings[rings.Length - 1][1],
                         rings[rings.Length - 1][2], rings[rings.Length - 1][3], inside);

            // Aileron arrière : deux dérives verticales.
            AddWing(mesh, new Vector3(-0.85f, 0.45f, -1.75f), 0.10f, 0.55f, 0.85f);
            AddWing(mesh, new Vector3(0.85f, 0.45f, -1.75f), 0.10f, 0.55f, 0.85f);

            ApplyMesh(hull, mesh, "Hull");
        }

        /// <summary>Petite dérive verticale (boîte effilée) ajoutée au maillage de la coque.</summary>
        private void AddWing(LowPolyMesh mesh, Vector3 basePosition, float thickness, float height, float length)
        {
            float halfT = thickness * 0.5f;
            float halfL = length * 0.5f;

            Vector3 bl = basePosition + new Vector3(-halfT, 0f, -halfL);
            Vector3 br = basePosition + new Vector3( halfT, 0f, -halfL);
            Vector3 fl = basePosition + new Vector3(-halfT, 0f,  halfL);
            Vector3 fr = basePosition + new Vector3( halfT, 0f,  halfL);

            Vector3 blT = bl + new Vector3(0f, height, 0.12f);
            Vector3 brT = br + new Vector3(0f, height, 0.12f);
            Vector3 flT = fl + new Vector3(0f, height * 0.45f, -0.10f);
            Vector3 frT = fr + new Vector3(0f, height * 0.45f, -0.10f);

            Vector3 inside = basePosition + Vector3.up * (height * 0.3f);

            mesh.AddQuad(bl, fl, flT, blT, inside); // face gauche
            mesh.AddQuad(br, brT, frT, fr, inside); // face droite
            mesh.AddQuad(bl, blT, brT, br, inside); // face arrière
            mesh.AddQuad(fl, fr, frT, flT, inside); // face avant
            mesh.AddQuad(blT, flT, frT, brT, inside); // sommet
        }

        /// <summary>
        /// Plaques d'armure : deux volumes anguleux sur les flancs,
        /// qui cassent la monotonie de la coque et marquent les lames.
        /// </summary>
        private void BuildArmorPlates(Transform parent, Material material)
        {
            GameObject plates = CreateMeshPart("ArmorPlates", parent, material);
            LowPolyMesh mesh = new LowPolyMesh();

            for (int side = -1; side <= 1; side += 2)
            {
                float x = 1.02f * side;
                Vector3 inside = new Vector3(x * 0.55f, 0.25f, 0f);

                Vector3 a = new Vector3(x, 0.12f, -1.55f);
                Vector3 b = new Vector3(x, 0.12f, 1.15f);
                Vector3 c = new Vector3(x + 0.16f * side, 0.34f, 1.05f);
                Vector3 d = new Vector3(x + 0.16f * side, 0.34f, -1.45f);

                Vector3 a2 = new Vector3(x - 0.12f * side, 0.12f, -1.55f);
                Vector3 b2 = new Vector3(x - 0.12f * side, 0.12f, 1.15f);
                Vector3 c2 = new Vector3(x - 0.12f * side, 0.34f, 1.05f);
                Vector3 d2 = new Vector3(x - 0.12f * side, 0.34f, -1.45f);

                mesh.AddQuad(a, b, c, d, inside);   // extérieur
                mesh.AddQuad(a2, d2, c2, b2, inside); // intérieur
                mesh.AddQuad(a, d, d2, a2, inside); // arrière
                mesh.AddQuad(b, b2, c2, c, inside); // avant
                mesh.AddQuad(d, c, c2, d2, inside); // dessus
            }

            ApplyMesh(plates, mesh, "ArmorPlates");
        }

        /// <summary>
        /// Lames énergétiques : fines lames effilées sur les flancs,
        /// en émission forte (elles s'intensifieront au drift à l'Étape 2).
        /// </summary>
        private void BuildBlades(Transform parent, Material material)
        {
            GameObject blades = CreateMeshPart("Blades", parent, material);
            LowPolyMesh mesh = new LowPolyMesh();

            for (int side = -1; side <= 1; side += 2)
            {
                float x = 1.30f * side;
                Vector3 inside = new Vector3(x * 0.4f, 0.3f, 0f);

                // Lame = longue arête effilée : épaisse à l'arrière, pointue à l'avant.
                Vector3 rearTop = new Vector3(x, 0.42f, -1.35f);
                Vector3 rearBottom = new Vector3(x, 0.18f, -1.35f);
                Vector3 midTop = new Vector3(x + 0.05f * side, 0.40f, 0.10f);
                Vector3 midBottom = new Vector3(x + 0.05f * side, 0.16f, 0.10f);
                Vector3 tip = new Vector3(x + 0.02f * side, 0.30f, 1.45f);

                // Face extérieure
                mesh.AddQuad(rearTop, midTop, midBottom, rearBottom, inside);
                mesh.AddTriangle(midTop, tip, midBottom, inside);

                // Face intérieure (fine épaisseur)
                float t = 0.06f * side;
                mesh.AddQuad(rearTop - new Vector3(t, 0f, 0f), rearBottom - new Vector3(t, 0f, 0f),
                             midBottom - new Vector3(t, 0f, 0f), midTop - new Vector3(t, 0f, 0f), inside);
                mesh.AddTriangle(midTop - new Vector3(t, 0f, 0f), midBottom - new Vector3(t, 0f, 0f),
                                 tip - new Vector3(t, 0f, 0f), inside);

                // Tranche supérieure
                mesh.AddQuad(rearTop, rearTop - new Vector3(t, 0f, 0f),
                             midTop - new Vector3(t, 0f, 0f), midTop, inside);
            }

            ApplyMesh(blades, mesh, "Blades");
        }

        /// <summary>
        /// Deux réacteurs cylindriques à l'arrière + cônes de poussée lumineux.
        /// </summary>
        private void BuildThrusters(Transform parent, Material thrusterMat, Material armorMat)
        {
            // Corps des réacteurs (métal sombre)
            GameObject housings = CreateMeshPart("ThrusterHousings", parent, armorMat);
            LowPolyMesh housingMesh = new LowPolyMesh();
            housingMesh.AddFrustum(new Vector3(-0.55f, 0.06f, -2.28f), 0.32f, 0.36f, 0.42f, 8, true, true);
            housingMesh.AddFrustum(new Vector3(0.55f, 0.06f, -2.28f), 0.32f, 0.36f, 0.42f, 8, true, true);
            ApplyMesh(housings, housingMesh, "ThrusterHousings");

            // Cônes de poussée (émission) — pointés vers l'arrière (-Z).
            GameObject flames = CreateMeshPart("ThrusterFlames", parent, thrusterMat);
            LowPolyMesh flameMesh = new LowPolyMesh();
            flameMesh.AddCone(new Vector3(-0.55f, 0.06f, -2.30f), new Vector3(-0.55f, 0.06f, -3.05f), 0.30f, 8, false);
            flameMesh.AddCone(new Vector3(0.55f, 0.06f, -2.30f), new Vector3(0.55f, 0.06f, -3.05f), 0.30f, 8, false);
            ApplyMesh(flames, flameMesh, "ThrusterFlames");
        }

        /// <summary>
        /// Quatre patins anti-gravité : petits cylindres émissifs sous la coque.
        /// </summary>
        private void BuildHoverPads(Transform parent, Material material)
        {
            GameObject pads = CreateMeshPart("HoverPads", parent, material);
            LowPolyMesh mesh = new LowPolyMesh();

            Vector2[] positions =
            {
                new Vector2(-0.80f, 1.25f),
                new Vector2( 0.80f, 1.25f),
                new Vector2(-0.80f, -1.45f),
                new Vector2( 0.80f, -1.45f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                mesh.AddFrustum(new Vector3(positions[i].x, -0.42f, positions[i].y), 0.20f, 0.26f, 0.20f, 6, true, true);
            }

            ApplyMesh(pads, mesh, "HoverPads");
        }

        /// <summary>Liseré lumineux sur l'arête du nez.</summary>
        private void BuildNoseAccent(Transform parent, Material material)
        {
            GameObject accent = CreateMeshPart("NoseAccent", parent, material);
            LowPolyMesh mesh = new LowPolyMesh();

            Vector3 inside = new Vector3(0f, 0.15f, 1.6f);

            Vector3 l = new Vector3(-0.20f, 0.155f, 1.55f);
            Vector3 r = new Vector3(0.20f, 0.155f, 1.55f);
            Vector3 lf = new Vector3(0.0f, 0.145f, 2.24f);
            mesh.AddTriangle(l, r, lf, inside);

            ApplyMesh(accent, mesh, "NoseAccent");
        }

        // ============================================================
        // OUTILS
        // ============================================================

        /// <summary>Enregistre le maillage généré sur le GameObject et libère les ressources.</summary>
        private void ApplyMesh(GameObject target, LowPolyMesh lowPolyMesh, string meshName)
        {
            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter == null)
                filter = target.AddComponent<MeshFilter>();

            Mesh mesh = lowPolyMesh.Build(meshName);
            filter.sharedMesh = mesh;

            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        /// <summary>Crée un GameObject enfant prêt à recevoir un maillage et un matériau.</summary>
        private GameObject CreateMeshPart(string partName, Transform parent, Material material)
        {
            GameObject part = new GameObject(partName);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>();
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();

            // Tri des triangles explicite : le maillage est déjà correctement orienté.
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            return part;
        }

        /// <summary>Section transversale de la coque.</summary>
        private readonly struct HullSection
        {
            public readonly float z;
            public readonly float halfWidth;
            public readonly float bottom;
            public readonly float top;

            public HullSection(float z, float halfWidth, float bottom, float top)
            {
                this.z = z;
                this.halfWidth = halfWidth;
                this.bottom = bottom;
                this.top = top;
            }
        }
    }
}
