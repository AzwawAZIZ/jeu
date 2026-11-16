using System.Collections.Generic;
using UnityEngine;

namespace AetherDrift.Core
{
    /// <summary>
    /// Générateur de maillages low-poly 100 % code (aucun asset, aucune dépendance).
    ///
    /// Pourquoi ce fichier ?
    /// Les primitives Unity (Cube, Cylinder…) donnent un rendu "bloc" très pauvre.
    /// Un châssis de bolide runique a besoin d'une silhouette effilée : on construit
    /// donc nos propres triangles, à la main, avec une **facettisation volontaire**
    /// (une normale par face = look low-poly assumé, façon Sable / Art of Rally).
    ///
    /// Deux décisions d'architecture importantes :
    ///
    /// 1. NORMALES EXPLICITES : on ne laisse pas Unity calculer les normales
    ///    (RecalculateNormals), on les calcule et on les stocke nous-mêmes.
    ///    Ainsi l'éclairage est correct quelle que soit la convention d'ordre
    ///    des sommets (clockwise/counter-clockwise) de la version d'Unity.
    ///
    /// 2. FACES DOUBLE-FACE : chaque triangle est ajouté dans les deux sens
    ///    d'enroulement. Avec le back-face culling par défaut, une seule copie
    ///    est visible — mais laquelle que soit la convention, la copie visible
    ///    porte la bonne normale. Résultat : aucun risque de mesh "à l'envers",
    ///    sans bricolage de shader (_Cull), donc compatible Built-in ET URP.
    ///    Coût : le nombre de triangles double (négligeable ici, meshes &lt; 300 tris).
    /// </summary>
    public class LowPolyMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>(512);
        private readonly List<Vector3> normals = new List<Vector3>(512);
        private readonly List<int> triangles = new List<int>(1024);

        public int TriangleCount => triangles.Count / 3;

        // ============================================================
        // AJOUT DE GÉOMÉTRIE
        // ============================================================

        /// <summary>
        /// Ajoute un triangle simple, avec une normale imposée.
        /// (Ajouté dans les deux sens d'enroulement : voir résumé de la classe.)
        /// </summary>
        private void AddTriangleRaw(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            if (normal.sqrMagnitude < 0.000001f)
                return;

            int start = vertices.Count;

            vertices.Add(a); normals.Add(normal);
            vertices.Add(b); normals.Add(normal);
            vertices.Add(c); normals.Add(normal);

            // Sens 1
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);

            // Sens 2 (double-face)
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
        }

        /// <summary>
        /// Ajoute un triangle dont la normale est orientée vers l'EXTÉRIEUR de la forme.
        /// </summary>
        /// <param name="insidePoint">Un point situé à l'intérieur de la forme (centre approximatif).</param>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 insidePoint)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 0.000001f)
                return;

            Vector3 centroid = (a + b + c) / 3f;
            if (Vector3.Dot(normal, centroid - insidePoint) < 0f)
                normal = -normal;

            AddTriangleRaw(a, b, c, normal.normalized);
        }

        /// <summary>
        /// Ajoute un quad (deux triangles) orienté vers l'extérieur.
        /// Convention : a → b → c → d forment un cycle autour de la face.
        /// </summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 insidePoint)
        {
            AddTriangle(a, b, c, insidePoint);
            AddTriangle(a, c, d, insidePoint);
        }

        /// <summary>
        /// Ajoute un quad avec une normale imposée (ex. sol : Vector3.up).
        /// Idéal pour les surfaces planes (anneau d'arène, lignes lumineuses).
        /// </summary>
        public void AddQuadFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            AddTriangleRaw(a, b, c, normal);
            AddTriangleRaw(a, c, d, normal);
        }

        // ============================================================
        // FORMES DE BASE
        // ============================================================

        /// <summary>
        /// Tronc de cône / cylindre basse définition, orienté vers +Y.
        /// Utilisé pour : cockpit, patins anti-gravité, tambours de colonnes.
        /// </summary>
        /// <param name="topRadius">0 = cône pointu.</param>
        public void AddFrustum(Vector3 baseCenter, float bottomRadius, float topRadius,
            float height, int sides, bool capBottom = true, bool capTop = true, float rotationDeg = 0f)
        {
            sides = Mathf.Max(3, sides);

            Vector3 topCenter = baseCenter + Vector3.up * height;
            Vector3 inside = baseCenter + Vector3.up * (height * 0.5f);

            float startAngle = rotationDeg * Mathf.Deg2Rad;
            float step = (Mathf.PI * 2f) / sides;

            Vector3[] bottom = new Vector3[sides];
            Vector3[] top = new Vector3[sides];

            for (int i = 0; i < sides; i++)
            {
                float angle = startAngle + step * i;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bottom[i] = baseCenter + dir * bottomRadius;
                top[i] = topCenter + dir * topRadius;
            }

            bool pointed = topRadius <= 0.0001f;

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;

                if (pointed)
                    AddTriangle(bottom[i], bottom[next], topCenter, inside);
                else
                    AddQuad(bottom[i], bottom[next], top[next], top[i], inside);
            }

            // Bouchons : le centre étant à l'intérieur, les normales partent bien vers l'extérieur.
            if (capBottom)
            {
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    AddTriangle(baseCenter, bottom[next], bottom[i], inside);
                }
            }

            if (capTop && !pointed)
            {
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    AddTriangle(topCenter, top[i], top[next], inside);
                }
            }
        }

        /// <summary>
        /// Cône orienté selon un axe quelconque (base → sommet).
        /// Utilisé pour : propulseurs (axe -Z) et montagnes (axe +Y).
        /// </summary>
        public void AddCone(Vector3 baseCenter, Vector3 apex, float radius, int sides, bool capBase = true)
        {
            sides = Mathf.Max(3, sides);

            Vector3 axis = apex - baseCenter;
            float height = axis.magnitude;
            if (height < 0.0001f)
                return;

            Vector3 up = axis / height;

            // Base orthonormée perpendiculaire à l'axe.
            Vector3 reference = Mathf.Abs(Vector3.Dot(up, Vector3.up)) > 0.98f ? Vector3.right : Vector3.up;
            Vector3 right = Vector3.Cross(reference, up).normalized;
            Vector3 forward = Vector3.Cross(up, right).normalized;

            Vector3 inside = baseCenter + up * (height * 0.5f);

            float step = (Mathf.PI * 2f) / sides;
            Vector3[] ring = new Vector3[sides];

            for (int i = 0; i < sides; i++)
            {
                float angle = step * i;
                ring[i] = baseCenter + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                AddTriangle(ring[i], ring[next], apex, inside);
            }

            if (capBase)
            {
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    AddTriangle(baseCenter, ring[next], ring[i], inside);
                }
            }
        }

        /// <summary>
        /// Anneau plat (disque évidé), normal vers le haut.
        /// Utilisé pour : le cercle d'arène au sol.
        /// </summary>
        public void AddFlatAnnulus(Vector3 center, float innerRadius, float outerRadius, int segments)
        {
            segments = Mathf.Max(3, segments);
            float step = (Mathf.PI * 2f) / segments;

            for (int i = 0; i < segments; i++)
            {
                float a0 = step * i;
                float a1 = step * (i + 1);

                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                Vector3 inner0 = center + d0 * innerRadius;
                Vector3 inner1 = center + d1 * innerRadius;
                Vector3 outer0 = center + d0 * outerRadius;
                Vector3 outer1 = center + d1 * outerRadius;

                AddQuadFacing(inner0, outer0, outer1, inner1, Vector3.up);
            }
        }

        // ============================================================
        // SORTIE
        // ============================================================

        /// <summary>
        /// Construit le Mesh final (normales déjà fournies, pas de recalcul).
        /// </summary>
        public Mesh Build(string meshName)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;

            // Sécurité : au-delà de 65535 sommets il faudrait un index 32 bits.
            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
