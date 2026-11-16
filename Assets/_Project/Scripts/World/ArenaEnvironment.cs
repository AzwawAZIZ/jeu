using UnityEngine;
using AetherDrift.Core;

namespace AetherDrift.World
{
    /// <summary>
    /// Décor de l'arène de l'Étape 1, généré intégralement par code.
    ///
    /// Objectif : sortir du "plan gris + cubes" pour donner une vraie
    /// identité visuelle low-poly (Sable / Art of Rally) :
    ///
    ///   - SOL      : grand disque sombre, facetté (pas un plan uniforme).
    ///   - ARÈNE    : anneau de pierre claire, cercle lumineux runique au centre.
    ///   - RUINES   : piliers de sections variables, tambours, chapiteaux,
    ///                certains brisés et penchés — en cercle autour de la zone.
    ///   - RELIEF   : montagnes coniques basses à l'horizon (silhouettes).
    ///   - CIEL     : dégradé vertical par bandes + brouillard assorti.
    ///
    /// ⚠️ Ce fichier ne contient AUCUNE logique de jeu : c'est du décor.
    /// Le vrai monde ouvert (terrain jouable, ruines escaladables) arrive
    /// à l'Étape 3, dans ce même dossier World/.
    /// </summary>
    public static class ArenaEnvironment
    {
        // ============================================================
        // PALETTE — un seul endroit pour tout retoucher
        // ============================================================

        private static readonly Color GroundDeep = new Color(0.20f, 0.21f, 0.25f);
        private static readonly Color GroundNear = new Color(0.27f, 0.28f, 0.32f);
        private static readonly Color GroundEdge = new Color(0.14f, 0.15f, 0.19f);
        private static readonly Color StoneLight = new Color(0.44f, 0.45f, 0.48f);
        private static readonly Color StoneDark = new Color(0.28f, 0.29f, 0.33f);
        private static readonly Color Moss = new Color(0.26f, 0.36f, 0.28f);
        private static readonly Color RuneCyan = new Color(0.16f, 0.85f, 1f);
        private static readonly Color Horizon = new Color(0.30f, 0.34f, 0.44f);
        private static readonly Color SkyTop = new Color(0.04f, 0.06f, 0.12f);

        // ============================================================
        // SOL
        // ============================================================

        /// <summary>
        /// Sol : disque facetté (plus joli qu'un plan carré) + son collider.
        /// </summary>
        public static GameObject CreateGround(float diameter, Transform parent)
        {
            GameObject ground = new GameObject("Ground (Arène)");
            ground.transform.SetParent(parent, false);

            // --- Maillage : disque subdivisé en anneaux pour les facettes ---
            LowPolyMesh mesh = new LowPolyMesh();
            BuildDisc(mesh, Vector3.zero, diameter * 0.5f, 64, 6);
            AttachMesh(ground, mesh, "GroundDisc");

            MeshRenderer renderer = ground.GetComponent<MeshRenderer>();
            Material mat = ProceduralMaterials.Create("GroundDeep", Color.white, Color.black, 0f, 0.10f, 0f);

            // Texture procédurale : gravier/terre battue. Sans elle, le sol reste
            // un aplat uniforme qui "flotte" visuellement.
            Texture2D groundTexture = CreateGroundTexture(512);
            if (groundTexture != null)
            {
                groundTexture.wrapMode = TextureWrapMode.Repeat;

                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", groundTexture);
                    mat.SetTextureScale("_BaseMap", new Vector2(diameter / 8f, diameter / 8f));
                }
                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", groundTexture);
                    mat.SetTextureScale("_MainTex", new Vector2(diameter / 8f, diameter / 8f));
                }
            }

            renderer.sharedMaterial = mat;
            renderer.receiveShadows = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // --- Collider : mesh collider sur le disque (sol praticable) ---
            MeshCollider collider = ground.AddComponent<MeshCollider>();
            collider.sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;

            return ground;
        }

        /// <summary>
        /// Texture procédurale du sol : bruit de gravier + taches sombres.
        /// Générée une seule fois (512x512), tileable grossièrement.
        /// </summary>
        private static Texture2D CreateGroundTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.name = "GroundGravel";

            Color[] pixels = new Color[size * size];
            System.Random rng = new System.Random(4242);

            // Bruit de valeur grossier, puis lissage : donne des taches naturelles
            // plutôt qu'un grain régulier qui scintille au loin.
            int cells = 24;
            float[] noise = new float[cells * cells];
            for (int i = 0; i < noise.Length; i++)
                noise[i] = (float)rng.NextDouble();

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = (float)x / size * cells;
                    float fy = (float)y / size * cells;

                    int ix = Mathf.FloorToInt(fx);
                    int iy = Mathf.FloorToInt(fy);
                    float tx = fx - ix;
                    float ty = fy - iy;

                    float n00 = noise[(iy % cells) * cells + (ix % cells)];
                    float n10 = noise[(iy % cells) * cells + ((ix + 1) % cells)];
                    float n01 = noise[((iy + 1) % cells) * cells + (ix % cells)];
                    float n11 = noise[((iy + 1) % cells) * cells + ((ix + 1) % cells)];

                    // Interpolation lissée (smoothstep) pour éviter les arêtes.
                    float sx = tx * tx * (3f - 2f * tx);
                    float sy = ty * ty * (3f - 2f * ty);

                    float top = Mathf.Lerp(n00, n10, sx);
                    float bottom = Mathf.Lerp(n01, n11, sx);
                    float value = Mathf.Lerp(top, bottom, sy);

                    // Grain fin par-dessus.
                    value += ((float)rng.NextDouble() - 0.5f) * 0.16f;
                    value = Mathf.Clamp01(value);

                    Color color = Color.Lerp(GroundDeep, GroundNear, value);

                    // Taches plus sombres (humidité, usure).
                    if (value < 0.28f)
                        color = Color.Lerp(color, GroundEdge, 0.55f);

                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>Disque plat subdivisé (centre + anneaux) → donne des facettes d'éclairage.</summary>
        private static void BuildDisc(LowPolyMesh mesh, Vector3 center, float radius, int segments, int rings)
        {
            float step = (Mathf.PI * 2f) / segments;

            for (int ring = 0; ring < rings; ring++)
            {
                float r0 = radius * ((float)ring / rings);
                float r1 = radius * ((float)(ring + 1) / rings);

                for (int i = 0; i < segments; i++)
                {
                    float a0 = step * i;
                    float a1 = step * (i + 1);

                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                    Vector3 p00 = center + d0 * r0;
                    Vector3 p01 = center + d1 * r0;
                    Vector3 p10 = center + d0 * r1;
                    Vector3 p11 = center + d1 * r1;

                    mesh.AddQuadFacing(p00, p10, p11, p01, Vector3.up);
                }
            }
        }

        // ============================================================
        // ARÈNE : anneau + cercle runique
        // ============================================================

        /// <summary>
        /// Anneau de pierre claire délimitant la zone de combat,
        /// plus un fin cercle runique cyan lumineux au centre.
        /// </summary>
        public static void CreateArenaRing(float outerRadius, Transform parent)
        {
            GameObject ring = new GameObject("ArenaRing");
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(0f, 0.012f, 0f);

            LowPolyMesh mesh = new LowPolyMesh();
            mesh.AddFlatAnnulus(Vector3.zero, outerRadius - 1.6f, outerRadius, 72);
            AttachMesh(ring, mesh, "ArenaRing");

            Material mat = ProceduralMaterials.Create("ArenaStone", StoneLight, Color.black, 0f, 0.12f, 0f);
            MeshRenderer renderer = ring.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // --- Cercle runique lumineux ---
            GameObject rune = new GameObject("RuneCircle");
            rune.transform.SetParent(parent, false);
            rune.transform.localPosition = new Vector3(0f, 0.018f, 0f);

            LowPolyMesh runeMesh = new LowPolyMesh();
            runeMesh.AddFlatAnnulus(Vector3.zero, outerRadius - 0.45f, outerRadius - 0.30f, 72);
            runeMesh.AddFlatAnnulus(Vector3.zero, 13.9f, 14.1f, 96);
            runeMesh.AddFlatAnnulus(Vector3.zero, 7.9f, 8.1f, 96);
            AttachMesh(rune, runeMesh, "RuneCircle");

            Material runeMat = ProceduralMaterials.Create("RuneGlow", RuneCyan, RuneCyan, 2.2f, 0.5f, 0f);
            MeshRenderer runeRenderer = rune.GetComponent<MeshRenderer>();
            runeRenderer.sharedMaterial = runeMat;
            runeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ============================================================
        // RUINES
        // ============================================================

        /// <summary>
        /// Piliers de ruines en cercle. Chaque pilier est composé de plusieurs
        /// tambours empilés (comme une vraie colonne antique brisée).
        /// Certains sont entiers (avec chapiteau), d'autres cassés et penchés.
        /// </summary>
        public static void CreateRuins(float arenaRadius, Transform parent, int pillarCount = 14)
        {
            GameObject root = new GameObject("Ruins (décor Étape 1)");
            root.transform.SetParent(parent, false);

            Material stoneMat = ProceduralMaterials.Create("Stone", StoneLight, Color.black, 0f, 0.10f, 0f);
            Material stoneDarkMat = ProceduralMaterials.Create("StoneDark", StoneDark, Color.black, 0f, 0.10f, 0f);
            Material mossMat = ProceduralMaterials.Create("Moss", Moss, Color.black, 0f, 0.08f, 0f);

            // Générateur pseudo-aléatoire déterministe : même décor à chaque Play,
            // mais aspect naturel (aucun besoin de graine éditable à l'Étape 1).
            System.Random rng = new System.Random(20261116);

            for (int i = 0; i < pillarCount; i++)
            {
                float angle = (360f / pillarCount) * i + (float)(rng.NextDouble() * 8.0 - 4.0);
                float rad = angle * Mathf.Deg2Rad;

                // Distance légèrement variable pour éviter l'effet "cercle parfait".
                float radius = arenaRadius + 6f + (float)(rng.NextDouble() * 12.0);
                Vector3 position = new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);

                bool broken = rng.NextDouble() < 0.45;
                bool mossy = rng.NextDouble() < 0.30;

                GameObject pillar = new GameObject("RuinPillar_" + i);
                pillar.transform.SetParent(root.transform, false);
                pillar.transform.localPosition = position;

                // Inclinaison des piliers brisés (effondrement).
                float tilt = broken ? (float)(rng.NextDouble() * 14.0 - 7.0) : (float)(rng.NextDouble() * 2.0 - 1.0);
                pillar.transform.localRotation = Quaternion.Euler(tilt, (float)(rng.NextDouble() * 360.0), tilt * 0.6f);

                LowPolyMesh mesh = new LowPolyMesh();

                float height = broken
                    ? 2.5f + (float)(rng.NextDouble() * 5.0)
                    : 8.0f + (float)(rng.NextDouble() * 5.0);

                float baseRadius = 0.85f;
                int drums = Mathf.Max(1, Mathf.RoundToInt(height / 1.6f));
                float drumHeight = height / drums;

                for (int d = 0; d < drums; d++)
                {
                    // Léger rétrécissement vers le haut, tambours facettés (8 côtés).
                    float rBottom = baseRadius * (1f - 0.035f * d);
                    float rTop = baseRadius * (1f - 0.035f * (d + 1));
                    float y = drumHeight * d;

                    // Tambour légèrement décalé : effet "colonnes qui bougent".
                    Vector3 offset = new Vector3(
                        (float)(rng.NextDouble() * 0.12 - 0.06),
                        y,
                        (float)(rng.NextDouble() * 0.12 - 0.06));

                    mesh.AddFrustum(offset, rBottom, rTop, drumHeight * 0.98f, 8, false, false);
                }

                // Socle plus large à la base.
                mesh.AddFrustum(new Vector3(0f, 0f, 0f), baseRadius * 1.35f, baseRadius * 1.1f, 0.45f, 8, true, false);

                // Chapiteau uniquement si le pilier est entier.
                if (!broken)
                {
                    mesh.AddFrustum(new Vector3(0f, height - 0.5f, 0f), baseRadius * 1.05f, baseRadius * 1.3f, 0.55f, 8, false, true);
                    // Abaque carrée au sommet.
                    AddBox(mesh, new Vector3(0f, height + 0.10f, 0f), new Vector3(1.9f, 0.25f, 1.9f));
                }

                // Blocs tombés autour des piliers brisés.
                if (broken)
                {
                    int debris = 1 + rng.Next(0, 3);
                    for (int d = 0; d < debris; d++)
                    {
                        Vector3 dPos = new Vector3(
                            (float)(rng.NextDouble() * 3.4 - 1.7),
                            0.22f,
                            (float)(rng.NextDouble() * 3.4 - 1.7));
                        AddBox(mesh, dPos, new Vector3(
                            0.5f + (float)rng.NextDouble() * 0.5f,
                            0.35f + (float)rng.NextDouble() * 0.3f,
                            0.5f + (float)rng.NextDouble() * 0.5f));
                    }
                }

                AttachMesh(pillar, mesh, "Pillar_" + i);

                MeshRenderer renderer = pillar.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = mossy ? mossMat : (broken ? stoneDarkMat : stoneMat);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;

                // Collider simple : capsule autour du pilier (le véhicule ne le traverse pas).
                CapsuleCollider capsule = pillar.AddComponent<CapsuleCollider>();
                capsule.radius = baseRadius * 1.1f;
                capsule.height = height + 1f;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);
            }
        }

        /// <summary>Ajoute une boîte axis-aligned au maillage (socle, débris…).</summary>
        private static void AddBox(LowPolyMesh mesh, Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;

            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p100 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p110 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z);
            Vector3 p101 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p111 = center + new Vector3(h.x, h.y, h.z);
            Vector3 p011 = center + new Vector3(-h.x, h.y, h.z);

            mesh.AddQuad(p000, p010, p110, p100, center); // -Z
            mesh.AddQuad(p001, p101, p111, p011, center); // +Z
            mesh.AddQuad(p000, p001, p011, p010, center); // -X
            mesh.AddQuad(p100, p110, p111, p101, center); // +X
            mesh.AddQuad(p000, p100, p101, p001, center); // -Y
            mesh.AddQuad(p010, p011, p111, p110, center); // +Y
        }

        // ============================================================
        // RELIEF / HORIZON
        // ============================================================

        /// <summary>
        /// Montagnes coniques basses à l'horizon : donnent une silhouette de
        /// vallée et empêchent le sol de "finir dans le vide".
        /// Purement décoratif : aucun collider (le joueur reste dans l'arène).
        /// </summary>
        public static void CreateDistantMountains(float radius, Transform parent, int count = 26)
        {
            GameObject root = new GameObject("Mountains (horizon)");
            root.transform.SetParent(parent, false);

            Material farMat = ProceduralMaterials.Create("MountainFar",
                new Color(0.17f, 0.19f, 0.25f), Color.black, 0f, 0.02f, 0f);
            Material nearMat = ProceduralMaterials.Create("MountainNear",
                new Color(0.12f, 0.14f, 0.19f), Color.black, 0f, 0.02f, 0f);

            System.Random rng = new System.Random(7788);

            for (int i = 0; i < count; i++)
            {
                float angle = (360f / count) * i + (float)(rng.NextDouble() * 5.0 - 2.5);
                float rad = angle * Mathf.Deg2Rad;

                float dist = radius - 12f + (float)(rng.NextDouble() * 40.0);
                float coneRadius = 26f + (float)(rng.NextDouble() * 44.0);
                float coneHeight = 22f + (float)(rng.NextDouble() * 46.0);

                Vector3 baseCenter = new Vector3(Mathf.Cos(rad) * dist, -2f, Mathf.Sin(rad) * dist);

                GameObject mountain = new GameObject("Mountain_" + i);
                mountain.transform.SetParent(root.transform, false);

                LowPolyMesh mesh = new LowPolyMesh();
                // Peu de côtés = silhouettes franches, typiques du low-poly.
                mesh.AddCone(baseCenter, baseCenter + Vector3.up * coneHeight, coneRadius, 6 + rng.Next(0, 3), false);
                AttachMesh(mountain, mesh, "Mountain_" + i);

                MeshRenderer renderer = mountain.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = rng.NextDouble() < 0.5 ? nearMat : farMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // ============================================================
        // CIEL
        // ============================================================

        /// <summary>
        /// Dôme de ciel en dégradé vertical.
        ///
        /// Astuce technique : un seul maillage ne peut pas porter plusieurs
        /// couleurs sans shader à dégradé (ou couleurs de sommets). On crée donc
        /// UNE bande = UN GameObject + UN matériau, chaque bande ayant sa couleur
        /// interpolée entre l'horizon et le zénith. 12 draw calls : négligeable,
        /// et ça fonctionne aussi bien en Built-in qu'en URP.
        /// </summary>
        public static void CreateSkyDome(float radius, Transform parent, int bands = 14)
        {
            GameObject root = new GameObject("SkyDome (dégradé)");
            root.transform.SetParent(parent, false);

            Shader unlit = Shader.Find("Unlit/Color");
            if (unlit == null)
                unlit = Shader.Find("Standard");

            int segments = 24;

            for (int band = 0; band < bands; band++)
            {
                float t0 = (float)band / bands;
                float t1 = (float)(band + 1) / bands;

                // Distribution non linéaire : plus de bandes près de l'horizon.
                float y0 = Mathf.Lerp(-0.10f, 1f, t0 * t0);
                float y1 = Mathf.Lerp(-0.10f, 1f, t1 * t1);

                float r0 = Mathf.Sqrt(Mathf.Max(0f, 1f - y0 * y0)) * radius;
                float r1 = Mathf.Sqrt(Mathf.Max(0f, 1f - y1 * y1)) * radius;

                LowPolyMesh mesh = new LowPolyMesh();

                for (int i = 0; i < segments; i++)
                {
                    float a0 = (Mathf.PI * 2f / segments) * i;
                    float a1 = (Mathf.PI * 2f / segments) * (i + 1);

                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                    Vector3 p00 = d0 * r0 + Vector3.up * (y0 * radius);
                    Vector3 p01 = d1 * r0 + Vector3.up * (y0 * radius);
                    Vector3 p10 = d0 * r1 + Vector3.up * (y1 * radius);
                    Vector3 p11 = d1 * r1 + Vector3.up * (y1 * radius);

                    // Normale tournée vers l'intérieur : on regarde le dôme de l'intérieur.
                    Vector3 normal = -((p00 + p11) * 0.5f);
                    if (normal.sqrMagnitude > 0.0001f)
                        normal.Normalize();

                    mesh.AddQuadFacing(p01, p11, p10, p00, normal);
                }

                GameObject bandObject = new GameObject("SkyBand_" + band);
                bandObject.transform.SetParent(root.transform, false);
                AttachMesh(bandObject, mesh, "SkyBand_" + band);

                // Couleur de la bande : des teintes les plus claires (horizon)
                // aux plus sombres (zénith), avec un léger virage vers le violet.
                float t = (t0 + t1) * 0.5f;
                Color bandColor = Color.Lerp(Horizon, SkyTop, Mathf.Sqrt(t));

                Material skyMat = new Material(unlit) { name = "SkyBand_" + band };
                skyMat.color = bandColor;
                if (skyMat.HasProperty("_BaseColor"))
                    skyMat.SetColor("_BaseColor", bandColor);

                MeshRenderer renderer = bandObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = skyMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // ============================================================
        // OUTILS
        // ============================================================

        /// <summary>Attache un MeshFilter + MeshRenderer à un GameObject.</summary>
        private static void AttachMesh(GameObject target, LowPolyMesh lowPolyMesh, string meshName)
        {
            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter == null)
                filter = target.AddComponent<MeshFilter>();

            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = target.AddComponent<MeshRenderer>();

            filter.sharedMesh = lowPolyMesh.Build(meshName);
        }
    }
}
