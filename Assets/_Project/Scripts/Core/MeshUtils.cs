using UnityEngine;

namespace AetherDrift.Core
{
    /// <summary>
    /// Utilitaires de maillage pour le générateur de monde.
    ///
    /// Complète LowPolyMesh.cs (qui fournit les formes de base : frustum, cône,
    /// anneau, quads) avec les volumes dont l'Étape 3 a besoin :
    /// boîtes orientables, rampes, prismes, et fusion de maillages.
    ///
    /// Toutes les méthodes écrivent dans un LowPolyMesh existant : on peut donc
    /// construire une zone entière (dizaines d'éléments) en UN SEUL mesh, donc
    /// un seul draw call. C'est ce qui rend un monde vaste tenable.
    /// </summary>
    public static class MeshUtils
    {
        /// <summary>
        /// Boîte orientable. <paramref name="rotation"/> permet de l'incliner
        /// (rampes, blocs basculés, débris).
        /// </summary>
        public static void AddBox(LowPolyMesh mesh, Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;

            // 8 sommets locaux, puis rotation + translation.
            Vector3[] p = new Vector3[8];
            int index = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 local = new Vector3(h.x * x, h.y * y, h.z * z);
                        p[index++] = center + rotation * local;
                    }
                }
            }

            // Ordre de remplissage ci-dessus : x (couche 4), puis y, puis z.
            // Index = (xi << 2) | (yi << 1) | zi, avec xi/yi/zi dans {0,1}.
            Vector3 V(int xi, int yi, int zi) => p[(xi << 2) | (yi << 1) | zi];

            mesh.AddQuad(V(0, 0, 0), V(1, 0, 0), V(1, 1, 0), V(0, 1, 0), center); // -Z
            mesh.AddQuad(V(0, 0, 1), V(0, 1, 1), V(1, 1, 1), V(1, 0, 1), center); // +Z
            mesh.AddQuad(V(0, 0, 0), V(0, 1, 0), V(0, 1, 1), V(0, 0, 1), center); // -X
            mesh.AddQuad(V(1, 0, 0), V(1, 0, 1), V(1, 1, 1), V(1, 1, 0), center); // +X
            mesh.AddQuad(V(0, 0, 0), V(0, 0, 1), V(1, 0, 1), V(1, 0, 0), center); // -Y
            mesh.AddQuad(V(0, 1, 0), V(1, 1, 0), V(1, 1, 1), V(0, 1, 1), center); // +Y
        }

        /// <summary>Boîte axis-aligned (raccourci).</summary>
        public static void AddBox(LowPolyMesh mesh, Vector3 center, Vector3 size)
        {
            AddBox(mesh, center, size, Quaternion.identity);
        }

        /// <summary>
        /// Rampes : prisme incliné d'une extrémité basse à une extrémité haute.
        /// La surface de roulement est parfaitement plane — indispensable pour
        /// que la voiture monte sans à-coup.
        /// </summary>
        /// <param name="start">Centre de la base, au sol.</param>
        /// <param name="end">Centre du sommet.</param>
        /// <param name="width">Largeur de la rampe.</param>
        public static void AddRamp(LowPolyMesh mesh, Vector3 start, Vector3 end, float width)
        {
            Vector3 direction = end - start;
            float length = new Vector3(direction.x, 0f, direction.z).magnitude;
            if (length < 0.01f)
                return;

            // Repère : X = largeur, Y = vertical, Z = longueur.
            Vector3 flat = new Vector3(direction.x, 0f, direction.z) / length;

            Vector3 mid = (start + end) * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(flat, Vector3.up);
            float height = Mathf.Abs(end.y - start.y);
            float slope = Mathf.Atan2(height, length) * Mathf.Rad2Deg;

            // Boîte très plate, inclinée : suffit et reste simple.
            Quaternion tilt = rotation * Quaternion.Euler(-slope, 0f, 0f);

            // La longueur réelle du plateau incliné est l'hypoténuse.
            float plateLength = length / Mathf.Cos(slope * Mathf.Deg2Rad);
            AddBox(mesh, mid, new Vector3(width, 0.6f, plateLength), tilt);
        }

        /// <summary>
        /// Prisme triangulaire (toit, dos-d'âne, éclat de ruine).
        /// </summary>
        public static void AddPrism(LowPolyMesh mesh, Vector3 baseCenter, float width, float length,
            float height, Quaternion rotation)
        {
            float hw = width * 0.5f;
            float hl = length * 0.5f;

            Vector3 bl = baseCenter + rotation * new Vector3(-hw, 0f, -hl);
            Vector3 br = baseCenter + rotation * new Vector3(hw, 0f, -hl);
            Vector3 fl = baseCenter + rotation * new Vector3(-hw, 0f, hl);
            Vector3 fr = baseCenter + rotation * new Vector3(hw, 0f, hl);
            Vector3 top = baseCenter + rotation * new Vector3(0f, height, 0f);

            Vector3 inside = baseCenter + rotation * new Vector3(0f, height * 0.25f, 0f);

            mesh.AddQuad(bl, br, fr, fl, inside);        // base
            mesh.AddTriangle(bl, fl, top, inside);       // pente gauche
            mesh.AddTriangle(br, top, fr, inside);       // pente droite
            mesh.AddTriangle(bl, top, br, inside);       // pignon arrière
            mesh.AddTriangle(fl, fr, top, inside);       // pignon avant
        }

        /// <summary>
        /// Rampe en spirale : succession de dalles inclinées qui montent autour
        /// d'un axe vertical. Utilisée pour les tours de synchronisation.
        ///
        /// Chaque dalle chevauche la suivante (sur-longueur de 35 %) : c'est ce
        /// qui donne une surface CONTINUE, sans arête où la voiture rebondit.
        /// La pente est calculée depuis la hauteur réelle à franchir, donc le
        /// résultat reste doux même avec beaucoup de tours.
        /// </summary>
        /// <param name="center">Axe de la spirale, au sol.</param>
        /// <param name="radius">Distance horizontale à l'axe.</param>
        /// <param name="startAngleDeg">Angle de départ (0 = +X).</param>
        /// <param name="totalAngleDeg">Angle total parcouru (900 = 2,5 tours).</param>
        /// <param name="startHeight">Hauteur du début de la rampe.</param>
        /// <param name="endHeight">Hauteur de la fin de la rampe.</param>
        /// <param name="width">Largeur du plateau.</param>
        /// <param name="steps">Nombre de dalles (plus = plus lisse).</param>
        public static void AddSpiralRamp(LowPolyMesh mesh, Vector3 center, float radius,
            float startAngleDeg, float totalAngleDeg, float startHeight, float endHeight,
            float width, int steps, float thickness = 0.6f)
        {
            steps = Mathf.Max(2, steps);

            Vector3 PointAt(float t)
            {
                float angle = (startAngleDeg + totalAngleDeg * t) * Mathf.Deg2Rad;
                float y = Mathf.Lerp(startHeight, endHeight, t);
                return center + new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
            }

            for (int i = 0; i < steps; i++)
            {
                Vector3 p0 = PointAt((float)i / steps);
                Vector3 p1 = PointAt((float)(i + 1) / steps);

                Vector3 delta = p1 - p0;
                float horizontal = new Vector3(delta.x, 0f, delta.z).magnitude;
                if (horizontal < 0.0001f)
                    continue;

                float slope = Mathf.Atan2(delta.y, horizontal) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.LookRotation(
                    new Vector3(delta.x, 0f, delta.z) / horizontal, Vector3.up);
                Quaternion tilt = rotation * Quaternion.Euler(-slope, 0f, 0f);

                float slopeLength = Mathf.Sqrt(horizontal * horizontal + delta.y * delta.y);

                // Le plateau est centré sous le segment, d'une demi-épaisseur :
                // sa face supérieure passe exactement par p0 et p1.
                Vector3 mid = (p0 + p1) * 0.5f - Vector3.up * (thickness * 0.5f);

                AddBox(mesh, mid, new Vector3(width, thickness, slopeLength * 1.35f), tilt);
            }
        }

        /// <summary>
        /// Cylindre plat (socle, plateforme de tour, muret circulaire).
        /// </summary>
        public static void AddDisc(LowPolyMesh mesh, Vector3 center, float radius, float thickness, int segments = 12)
        {
            mesh.AddFrustum(center - Vector3.up * (thickness * 0.5f), radius, radius, thickness, segments, true, true);
        }
    }
}
