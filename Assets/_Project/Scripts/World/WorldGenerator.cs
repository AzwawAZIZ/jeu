using System.Collections.Generic;
using UnityEngine;
using AetherDrift.Core;

namespace AetherDrift.World
{
    /// <summary>
    /// Générateur du monde ouvert — ÉTAPE 3.
    ///
    /// OBJECTIF : une carte VASTE (1,2 km de côté) que l'on peut traverser à
    /// pleine vitesse sans jamais être bloqué par du décor mal placé.
    ///
    /// TROIS ZONES DISTINCTES, reliées par un réseau de chemins :
    ///
    ///   1. LE CŒUR DES RUINES (centre, rayon ~180 m)
    ///      Piliers, arches, murets brisés, blocs de pierre épars.
    ///      C'est l'arène de départ, dense mais praticable.
    ///
    ///   2. LES TERRES DE LANCEMENT (nord, Z ≈ 450)
    ///      Rampes de toutes tailles orientées vers le centre, tremplins
    ///      doubles, buttes de terre. Pensées pour la vitesse et le saut.
    ///
    ///   3. LES CHEMINS ANCIENS (est/ouest/sud, anneau périphérique)
    ///      Un réseau de routes plates bordées de bornes lumineuses, qui
    ///      relie les zones et sert de repère de navigation.
    ///
    /// PERFORMANCE : chaque zone est fusionnée en UN SEUL maillage (donc un
    /// draw call par zone). Un monde de cette taille en objets séparés
    /// écroulerait le framerate.
    ///
    /// REPÈRES POUR LA CONDUITE : aucune structure n'est placée dans un rayon
    /// de <see cref="clearanceRadius"/> autour des chemins ou des points
    /// d'intérêt, pour que la voiture ne soit jamais coincée.
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        // ============================================================
        // RÉGLAGES
        // ============================================================

        [Header("Dimensions du monde")]
        [Tooltip("Demi-taille de la carte, en mètres (600 = carte de 1,2 km).")]
        [SerializeField] private float worldHalfSize = 600f;

        [Tooltip("Rayon de la zone de ruines centrale (240 m = la moitié de la carte).")]
        [SerializeField] private float ruinsRadius = 240f;

        [Tooltip("Décalage de la zone des rampes vers le nord.")]
        [SerializeField] private float rampsAreaCenterZ = 450f;

        [Tooltip("Taille de la zone des rampes.")]
        [SerializeField] private float rampsAreaRadius = 130f;

        [Header("Densité")]
        [Tooltip("Nombre de structures de ruines (piliers, arches, murets).")]
        [SerializeField] private int ruinsCount = 320;

        [Tooltip("Nombre de rampes dans les Terres de Lancement.")]
        [SerializeField] private int rampsCount = 34;

        [Tooltip("Nombre de blocs rocheux décoratifs.")]
        [SerializeField] private int bouldersCount = 140;

        [Tooltip("Distance minimale libre autour des chemins (m).")]
        [SerializeField] private float clearanceRadius = 13f;

        [Header("Apparence")]
        [Tooltip("Couleur de la pierre principale.")]
        [SerializeField] private Color stoneColor = new Color(0.42f, 0.43f, 0.46f);

        [Tooltip("Couleur de la terre / des rampes.")]
        [SerializeField] private Color earthColor = new Color(0.34f, 0.28f, 0.22f);

        [Tooltip("Couleur des bornes lumineuses de chemin.")]
        [SerializeField] private Color markerColor = new Color(0.15f, 0.85f, 1f);

        [Header("Graine")]
        [Tooltip("Graine du générateur : même valeur = même monde. 0 = aléatoire.")]
        [SerializeField] private int seed = 20261116;

        // ============================================================
        // ÉTAT
        // ============================================================

        private System.Random rng;

        /// <summary>Points d'intérêt (centres de zones), utilisés par les tours et contrats.</summary>
        public Vector3 RuinsCenter => Vector3.zero;
        public Vector3 RampsCenter => new Vector3(0f, 0f, rampsAreaCenterZ);

        /// <summary>Demi-taille de la carte (lecture pour la mini-map).</summary>
        public float WorldHalfSize => worldHalfSize;

        /// <summary>Segments de chemins générés (pour placer des repères dessus).</summary>
        public List<Vector3> PathPoints { get; } = new List<Vector3>();


        /// <summary>Emplacements praticables réservés (tours, contrats).</summary>
        private readonly List<Vector3> reservedSpots = new List<Vector3>();

        /// <summary>
        /// Segments de chemin, stockés pour que le test de dégagement porte sur
        /// TOUTE la longueur du chemin, pas seulement sur ses nœuds. Sans ça,
        /// un pilier pouvait apparaître au milieu d'une ligne droite de 60 m.
        /// </summary>
        private readonly List<Vector3> pathStart = new List<Vector3>();
        private readonly List<Vector3> pathEnd = new List<Vector3>();

        // ============================================================
        // API PUBLIQUE
        // ============================================================

        /// <summary>
        /// Génère tout le monde sous <paramref name="parent"/>.
        /// </summary>
        public void Generate(Transform parent)
        {
            rng = new System.Random(seed == 0 ? System.Environment.TickCount : seed);

            GameObject root = new GameObject("WorldGenerator");
            root.transform.SetParent(parent, false);

            BuildPathNetwork(root.transform);

            // Les zones consomment le réseau de chemins pour éviter les collisions.
            BuildRuinsZone(root.transform);
            BuildRampsZone(root.transform);
            BuildBoulders(root.transform);
            BuildBoundaries(root.transform);

            Debug.Log("[AETHER DRIFT] Monde généré : " + (worldHalfSize * 2f) + " m de côté, "
                + ruinsCount + " ruines, " + rampsCount + " rampes, " + PathPoints.Count + " nœuds de chemin.");
        }

        /// <summary>
        /// Réserve un emplacement (tour, contrat) : le décor n'y sera pas placé.
        /// </summary>
        public void ReserveSpot(Vector3 position, float radius)
        {
            reservedSpots.Add(position);

            // On mémorise aussi le rayon sous forme de petits cercles de contrôle
            // pour que le test de collision reste simple.
            const int resolution = 8;
            for (int i = 0; i < resolution; i++)
            {
                float angle = (Mathf.PI * 2f / resolution) * i;
                reservedSpots.Add(position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        /// <summary>
        /// Retourne un point praticable libre, loin des chemins et des réservations.
        /// </summary>
        public Vector3 FindFreeSpot(System.Random random, float minRadius, float maxRadius, Vector3 center)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                float distance = Mathf.Lerp(minRadius, maxRadius, (float)random.NextDouble());
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

                if (candidate.magnitude > worldHalfSize - 40f)
                    continue;

                if (IsTooCloseToPath(candidate) || IsReserved(candidate))
                    continue;

                return candidate;
            }

            // Repli : un point dégagé près du centre de la zone.
            return center + new Vector3(minRadius, 0f, 0f);
        }

        // ============================================================
        // ZONE 1 — CŒUR DES RUINES
        // ============================================================

        /// <summary>
        /// Piliers, arches, murets et dalles autour du centre.
        /// Tout est fusionné en un seul mesh (1 draw call).
        /// </summary>
        private void BuildRuinsZone(Transform parent)
        {
            GameObject zone = CreateZone("Zone 1 - Ruines", parent);

            LowPolyMesh mesh = new LowPolyMesh();

            for (int i = 0; i < ruinsCount; i++)
            {
                // Distribution annulaire : plus dense au centre, dégagée au bord.
                float t = (float)rng.NextDouble();
                float radius = Mathf.Lerp(28f, ruinsRadius, Mathf.Sqrt(t));
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);

                Vector3 basePos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                // On ne bloque jamais un chemin.
                if (IsTooCloseToPath(basePos) || IsReserved(basePos))
                    continue;

                int kind = rng.Next(0, 4);
                switch (kind)
                {
                    case 0: BuildPillar(mesh, basePos); break;
                    case 1: BuildArch(mesh, basePos, angle); break;
                    case 2: BuildWall(mesh, basePos, angle); break;
                    default: BuildRubble(mesh, basePos); break;
                }

                // Une ruine sur cinq est un ARC DE CERCLE : plusieurs blocs posés
                // le long d'une courbe. C'est ce qui donne l'impression d'une
                // ville effondrée plutôt que d'un champ de poteaux.
                if (rng.NextDouble() < 0.2)
                    BuildArcade(mesh, basePos, (float)(rng.NextDouble() * 360f));
            }

            FinishZone(zone, mesh, "RuinsMesh");
        }

        /// <summary>Pilier cylindrique à tambours, éventuellement brisé.</summary>
        private void BuildPillar(LowPolyMesh mesh, Vector3 basePos)
        {
            bool broken = rng.NextDouble() < 0.4;
            float height = broken ? 2.5f + (float)rng.NextDouble() * 4f : 7f + (float)rng.NextDouble() * 6f;
            float radius = 0.9f + (float)rng.NextDouble() * 0.5f;

            int drums = Mathf.Max(1, Mathf.RoundToInt(height / 1.5f));
            float drumHeight = height / drums;

            for (int d = 0; d < drums; d++)
            {
                float rBottom = radius * (1f - 0.04f * d);
                float rTop = radius * (1f - 0.04f * (d + 1));

                // Léger décalage : colonnes antiques qui ont bougé.
                Vector3 offset = new Vector3(
                    (float)(rng.NextDouble() - 0.5) * 0.1f,
                    drumHeight * d,
                    (float)(rng.NextDouble() - 0.5) * 0.1f);

                mesh.AddFrustum(basePos + offset, rBottom, rTop, drumHeight * 0.98f, 8, false, false);
            }

            mesh.AddFrustum(basePos, radius * 1.4f, radius * 1.15f, 0.5f, 8, true, false);

            if (!broken)
            {
                mesh.AddFrustum(basePos + Vector3.up * (height - 0.45f), radius * 1.05f, radius * 1.3f, 0.5f, 8, false, true);
                MeshUtils.AddBox(mesh, basePos + Vector3.up * (height + 0.05f), new Vector3(radius * 2.2f, 0.25f, radius * 2.2f));
            }
            else
            {
                // Débris au pied du pilier brisé.
                int debris = 1 + rng.Next(0, 3);
                for (int d = 0; d < debris; d++)
                {
                    Vector3 dPos = basePos + new Vector3(
                        (float)(rng.NextDouble() - 0.5) * 3.4f, 0.2f,
                        (float)(rng.NextDouble() - 0.5) * 3.4f);
                    MeshUtils.AddBox(mesh, dPos,
                        new Vector3(0.5f + (float)rng.NextDouble() * 0.6f, 0.4f, 0.5f + (float)rng.NextDouble() * 0.6f),
                        Quaternion.Euler(0f, (float)(rng.NextDouble() * 360f), 0f));
                }
            }
        }

        /// <summary>Arche : deux piliers et un linteau. Point de passage spectaculaire.</summary>
        private void BuildArch(LowPolyMesh mesh, Vector3 basePos, float angleDeg)
        {
            float span = 7f + (float)rng.NextDouble() * 5f;
            float height = 6f + (float)rng.NextDouble() * 4f;
            float thickness = 1.1f;

            Quaternion rotation = Quaternion.Euler(0f, angleDeg, 0f);
            Vector3 side = rotation * Vector3.right * (span * 0.5f);

            // Deux montants.
            MeshUtils.AddBox(mesh, basePos - side + Vector3.up * (height * 0.5f),
                new Vector3(thickness, height, thickness));
            MeshUtils.AddBox(mesh, basePos + side + Vector3.up * (height * 0.5f),
                new Vector3(thickness, height, thickness));

            // Linteau.
            MeshUtils.AddBox(mesh, basePos + Vector3.up * (height + 0.4f),
                new Vector3(span + thickness, 0.8f, thickness * 1.2f), rotation);
        }

        /// <summary>Muret bas, franchissable en le sautant.</summary>
        private void BuildWall(LowPolyMesh mesh, Vector3 basePos, float angleDeg)
        {
            float length = 6f + (float)rng.NextDouble() * 10f;
            float height = 0.9f + (float)rng.NextDouble() * 1.4f;
            float thickness = 0.7f;

            Quaternion rotation = Quaternion.Euler(0f, angleDeg, 0f);

            // Muret en deux tronçons décalés : évite l'effet "barrière nette".
            MeshUtils.AddBox(mesh, basePos + Vector3.up * (height * 0.5f),
                new Vector3(length * 0.6f, height, thickness), rotation);

            Vector3 offset = rotation * new Vector3(length * 0.35f, 0f, 0f);
            MeshUtils.AddBox(mesh, basePos + offset + Vector3.up * (height * 0.35f),
                new Vector3(length * 0.45f, height * 0.7f, thickness * 0.8f), rotation);
        }

        /// <summary>
        /// Arcade effondrée : une file de blocs décrivant un arc de cercle,
        /// dont la hauteur décroît vers une extrémité — souvenir d'une galerie
        /// qui s'est affaissée. Collision traitée par le maillage fusionné.
        /// </summary>
        private void BuildArcade(LowPolyMesh mesh, Vector3 basePos, float startAngle)
        {
            int segments = 5 + rng.Next(0, 4);
            float radius = 9f + (float)rng.NextDouble() * 9f;
            float angularSpan = 55f + (float)rng.NextDouble() * 55f;

            float collapse = (float)rng.NextDouble();   // 0 = intacte, 1 = effondrée

            for (int i = 0; i < segments; i++)
            {
                float t = segments > 1 ? (float)i / (segments - 1) : 0f;
                float angle = (startAngle + angularSpan * t) * Mathf.Deg2Rad;

                Vector3 pos = basePos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                // La hauteur s'écrase progressivement vers la fin de l'arc.
                float height = Mathf.Lerp(6f, 1.2f, collapse * t) * (0.85f + (float)rng.NextDouble() * 0.3f);
                float width = 1.1f + (float)rng.NextDouble() * 0.5f;

                MeshUtils.AddBox(mesh, pos + Vector3.up * (height * 0.5f),
                    new Vector3(width, height, width),
                    Quaternion.Euler(
                        (float)(rng.NextDouble() - 0.5) * collapse * 14f,
                        angle * Mathf.Rad2Deg,
                        (float)(rng.NextDouble() - 0.5) * collapse * 14f));

                // Linteau entre deux blocs, s'il en reste debout.
                if (i < segments - 1 && collapse < 0.5f)
                {
                    float nextAngle = (startAngle + angularSpan * ((i + 1f) / (segments - 1))) * Mathf.Deg2Rad;
                    Vector3 nextPos = basePos + new Vector3(Mathf.Cos(nextAngle), 0f, Mathf.Sin(nextAngle)) * radius;
                    Vector3 mid = (pos + nextPos) * 0.5f + Vector3.up * (height + 0.35f);
                    // La longueur du linteau suit la corde entre les deux blocs :
                    // avec un arc, la distance latérale seule laisserait un trou.
                    float span = Vector3.Distance(pos, nextPos) + width;

                    MeshUtils.AddBox(mesh, mid, new Vector3(width * 1.1f, 0.7f, span),
                        Quaternion.LookRotation((nextPos - pos).normalized, Vector3.up));
                }
            }
        }

        /// <summary>Amas de blocs éboulés.</summary>
        private void BuildRubble(LowPolyMesh mesh, Vector3 basePos)
        {
            int blocks = 2 + rng.Next(0, 4);
            for (int i = 0; i < blocks; i++)
            {
                Vector3 offset = new Vector3(
                    (float)(rng.NextDouble() - 0.5) * 3.5f,
                    0f,
                    (float)(rng.NextDouble() - 0.5) * 3.5f);

                Vector3 size = new Vector3(
                    0.6f + (float)rng.NextDouble() * 1.2f,
                    0.4f + (float)rng.NextDouble() * 1.0f,
                    0.6f + (float)rng.NextDouble() * 1.2f);

                Quaternion rot = Quaternion.Euler(
                    (float)(rng.NextDouble() - 0.5) * 18f,
                    (float)(rng.NextDouble() * 360f),
                    (float)(rng.NextDouble() - 0.5) * 18f);

                MeshUtils.AddBox(mesh, basePos + offset + Vector3.up * (size.y * 0.5f), size, rot);
            }
        }

        // ============================================================
        // ZONE 2 — TERRES DE LANCEMENT (rampes)
        // ============================================================

        /// <summary>
        /// Rampes de saut orientées vers le centre du monde, plus tremplins
        /// doubles et buttes de terre.
        /// </summary>
        private void BuildRampsZone(Transform parent)
        {
            GameObject zone = CreateZone("Zone 2 - Rampes", parent);

            LowPolyMesh mesh = new LowPolyMesh();
            Vector3 center = RampsCenter;

            for (int i = 0; i < rampsCount; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float distance = (float)Mathf.Lerp(28f, rampsAreaRadius, (float)rng.NextDouble());

                Vector3 basePos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

                if (IsTooCloseToPath(basePos) || IsReserved(basePos))
                    continue;

                // Orientation : vers le centre de la carte, avec un écart aléatoire.
                Vector3 toCenter = (Vector3.zero - basePos).normalized;
                Vector3 flat = new Vector3(toCenter.x, 0f, toCenter.z).normalized;

                bool bigJump = rng.NextDouble() < 0.35;
                float rampLength = bigJump ? 26f : 13f + (float)rng.NextDouble() * 10f;
                float rampHeight = bigJump ? 7.5f : 2.5f + (float)rng.NextDouble() * 3.5f;
                float width = bigJump ? 11f : 7f + (float)rng.NextDouble() * 4f;

                Vector3 start = basePos - flat * (rampLength * 0.5f);
                Vector3 end = basePos + flat * (rampLength * 0.5f) + Vector3.up * rampHeight;

                // La surface de roulement doit partir DU SOL, sinon la voiture
                // bute sur une marche de la demi-épaisseur du plateau (0,3 m).
                MeshUtils.AddRamp(mesh, start - Vector3.up * 0.3f, end, width);

                // Rampes d'approche : prolongent la pente vers l'arrière, pour
                // une entrée progressive même à basse vitesse.
                MeshUtils.AddRamp(mesh, start - flat * 6f - Vector3.up * 0.3f,
                    start - Vector3.up * 0.3f + Vector3.up * 0.02f, width * 0.9f);

                // Butte de terre sur le côté : sert de tremplin latéral.
                if (rng.NextDouble() < 0.5)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, flat) * (width + 8f);
                    float bump = 0.8f + (float)rng.NextDouble() * 1.2f;
                    MeshUtils.AddPrism(mesh, basePos + side,
                        width * 0.8f, 14f, bump,
                        Quaternion.LookRotation(Vector3.Cross(Vector3.up, side.normalized), Vector3.up));
                }
            }

            FinishZone(zone, mesh, "RampsMesh", true, earthColor);
        }

        // ============================================================
        // ZONE 3 — CHEMINS ANCIENS
        // ============================================================

        /// <summary>
        /// Réseau de chemins : anneau périphérique + rayons vers le centre et
        /// vers les Terres de Lancement. Les chemins sont PLATS et sans obstacle,
        /// et leurs bornes lumineuses servent de repère à grande vitesse.
        /// </summary>
        private void BuildPathNetwork(Transform parent)
        {
            GameObject zone = CreateZone("Zone 3 - Chemins", parent);
            LowPolyMesh mesh = new LowPolyMesh();
            LowPolyMesh markerMesh = new LowPolyMesh();

            float ringRadius = ruinsRadius + 60f;
            float pathWidth = 16f;

            // --- Anneau périphérique ---
            //
            // Les nœuds de l'anneau sont créés d'abord, puis les segments :
            //   i = 0 -> +X (est)   i = 6 -> +Z (nord)
            //   i = 12 -> -X (ouest)  i = 18 -> -Z (sud)
            // On réutilise ces nœuds pour les rayons, plutôt que d'en créer
            // des doublons à la même position.
            const int segments = 24;
            int firstRingIndex = PathPoints.Count;

            for (int i = 0; i < segments; i++)
            {
                float angle = (Mathf.PI * 2f / segments) * i;
                PathPoints.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius);
            }

            for (int i = 0; i < segments; i++)
            {
                Vector3 a = PathPoints[firstRingIndex + i];
                Vector3 b = PathPoints[firstRingIndex + (i + 1) % segments];

                AddPathSegment(mesh, a, b, pathWidth);

                // Borne lumineuse tous les deux segments, côté extérieur.
                if (i % 2 == 0)
                {
                    Vector3 outward = new Vector3(b.x, 0f, b.z).normalized;
                    AddPathMarker(markerMesh, b + outward * (pathWidth * 0.6f));
                }
            }

            int eastGate = firstRingIndex + 0;
            int northGate = firstRingIndex + segments / 4;
            int westGate = firstRingIndex + segments / 2;
            int southGate = firstRingIndex + (segments * 3) / 4;

            // --- Rayons depuis le centre ---
            int[] gates = { northGate, southGate, eastGate, westGate };
            Vector3 centerNode = Vector3.zero;
            PathPoints.Add(centerNode);

            for (int i = 0; i < gates.Length; i++)
                AddPathSegment(mesh, centerNode, PathPoints[gates[i]], pathWidth * 0.8f);

            // --- Chemin principal vers les Terres de Lancement ---
            Vector3 rampsEntrance = new Vector3(0f, 0f, rampsAreaCenterZ - rampsAreaRadius * 0.6f);
            PathPoints.Add(rampsEntrance);

            AddPathSegment(mesh, PathPoints[northGate], rampsEntrance, pathWidth * 0.8f);

            // --- Axe nord : du centre au portail nord (240 m), puis du portail
            //     aux Terres de Lancement (environ 130 m) : c'est la plus longue
            //     ligne droite du jeu, la piste d'élan idéale. ---
            // On la borde tous les 30 m de piliers lumineux, pour donner un
            // repère de vitesse : sans références latérales, on ne "sent" pas
            // la vitesse, même à 150 km/h.
            Vector3 northGatePosition = PathPoints[northGate];
            Vector3 axis = rampsEntrance - northGatePosition;
            float axisLength = new Vector3(axis.x, 0f, axis.z).magnitude;
            Vector3 axisFlat = new Vector3(axis.x, 0f, axis.z) / axisLength;
            Vector3 lateral = Vector3.Cross(Vector3.up, axisFlat) * (pathWidth * 0.62f);

            int pillarCount = Mathf.FloorToInt(axisLength / 30f);
            for (int i = 1; i < pillarCount; i++)
            {
                Vector3 point = northGatePosition + axisFlat * (axisLength * i / pillarCount);
                AddPathMarker(markerMesh, point + lateral);
                AddPathMarker(markerMesh, point - lateral);
            }

            // Aucun collider : le sol est plat sous les chemins. Un collider
            // ici créerait une marche de 8 cm à chaque bord de route — le genre
            // de détail qui rend la conduite désagréable sans qu'on sache pourquoi.
            FinishZone(zone, mesh, "PathsMesh", false);
            FinishZone(zone, markerMesh, "PathMarkersMesh", false, markerColor, true);
        }

        /// <summary>Dalle plate d'un segment de chemin.</summary>
        private void AddPathSegment(LowPolyMesh mesh, Vector3 from, Vector3 to, float width)
        {
            Vector3 direction = to - from;
            float length = new Vector3(direction.x, 0f, direction.z).magnitude;
            if (length < 0.5f)
                return;

            Vector3 flat = new Vector3(direction.x, 0f, direction.z) / length;
            Quaternion rotation = Quaternion.LookRotation(flat, Vector3.up);

            // Mémorisé pour le test de dégagement (voir IsTooCloseToPath).
            pathStart.Add(from);
            pathEnd.Add(to);

            // Très légèrement au-dessus du sol pour éviter le z-fighting.
            Vector3 center = (from + to) * 0.5f + Vector3.up * 0.04f;
            MeshUtils.AddBox(mesh, center, new Vector3(width, 0.08f, length + 1f), rotation);
        }

        /// <summary>Borne lumineuse : petit bloc émissif au bord du chemin.</summary>
        private void AddPathMarker(LowPolyMesh mesh, Vector3 position)
        {
            MeshUtils.AddBox(mesh, position + Vector3.up * 0.45f, new Vector3(0.45f, 1.1f, 0.45f));
        }

        // ============================================================
        // BLOCS ROCHEUX
        // ============================================================

        /// <summary>Rochers décoratifs dispersés, jamais sur un chemin.</summary>
        private void BuildBoulders(Transform parent)
        {
            GameObject zone = CreateZone("Rochers", parent);
            LowPolyMesh mesh = new LowPolyMesh();

            for (int i = 0; i < bouldersCount; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float radius = Mathf.Lerp(ruinsRadius * 0.6f, worldHalfSize * 0.9f, (float)rng.NextDouble());

                Vector3 pos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                if (IsTooCloseToPath(pos) || IsReserved(pos) || IsInRampsArea(pos))
                    continue;

                float size = 1.5f + (float)rng.NextDouble() * 4.5f;

                // Amas de 2-4 rochers anguleux.
                int count = 1 + rng.Next(0, 3);
                for (int b = 0; b < count; b++)
                {
                    Vector3 offset = new Vector3(
                        (float)(rng.NextDouble() - 0.5) * size * 1.4f,
                        0f,
                        (float)(rng.NextDouble() - 0.5) * size * 1.4f);

                    float s = size * (0.5f + (float)rng.NextDouble() * 0.6f);

                    mesh.AddFrustum(pos + offset,
                        s * 0.5f, s * 0.15f, s * 0.9f, 5 + rng.Next(0, 2),
                        true, true, (float)(rng.NextDouble() * 360f));
                }
            }

            FinishZone(zone, mesh, "BouldersMesh", true);
        }

        // ============================================================
        // LIMITES DU MONDE
        // ============================================================

        /// <summary>
        /// Quatre murs invisibles au bord de la carte.
        ///
        /// Sans eux, à 42 m/s on sort du sol en quelques secondes et on tombe
        /// dans le vide : la pire expérience possible en monde ouvert.
        /// Ils sont placés un peu à l'INTÉRIEUR du bord du sol, et assez hauts
        /// pour qu'on ne puisse pas les sauter depuis une rampe.
        /// </summary>
        private void BuildBoundaries(Transform parent)
        {
            GameObject root = new GameObject("Limites (invisibles)");
            root.transform.SetParent(parent, false);

            float limit = worldHalfSize - 10f;
            const float wallHeight = 120f;
            const float wallThickness = 4f;

            for (int i = 0; i < 4; i++)
            {
                // 0 = nord, 1 = sud, 2 = est, 3 = ouest
                bool horizontal = i < 2;
                float sign = (i % 2 == 0) ? 1f : -1f;

                Vector3 center = horizontal
                    ? new Vector3(0f, wallHeight * 0.5f, limit * sign)
                    : new Vector3(limit * sign, wallHeight * 0.5f, 0f);

                Vector3 size = horizontal
                    ? new Vector3(limit * 2f, wallHeight, wallThickness)
                    : new Vector3(wallThickness, wallHeight, limit * 2f);

                GameObject wall = new GameObject("Limite " + i);
                wall.transform.SetParent(root.transform, false);
                wall.transform.position = center;

                BoxCollider collider = wall.AddComponent<BoxCollider>();
                collider.size = size;
                // Aucun renderer : le mur est invisible, mais solide.
            }
        }

        // ============================================================
        // OUTILS INTERNES
        // ============================================================

        /// <summary>Crée le GameObject d'une zone et son matériau.</summary>
        private GameObject CreateZone(string name, Transform parent)
        {
            GameObject zone = new GameObject(name);
            zone.transform.SetParent(parent, false);
            return zone;
        }

        /// <summary>
        /// Attache le mesh fusionné au GameObject de zone, avec matériau et collider.
        /// </summary>
        private void FinishZone(GameObject zone, LowPolyMesh mesh, string meshName,
            bool addCollider = true, Color? overrideColor = null, bool emissive = false)
        {
            if (mesh.TriangleCount == 0)
                return;

            GameObject target = new GameObject(meshName);
            target.transform.SetParent(zone.transform, false);

            Mesh built = mesh.Build(meshName);
            target.AddComponent<MeshFilter>().sharedMesh = built;

            MeshRenderer renderer = target.AddComponent<MeshRenderer>();

            Color color = overrideColor.HasValue ? overrideColor.Value : stoneColor;
            Material material = emissive
                ? ProceduralMaterials.Create(meshName + "Mat", color, color, 2.2f, 0.5f, 0f)
                : ProceduralMaterials.Create(meshName + "Mat", color, Color.black, 0f, 0.12f, 0f);

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            // Un MeshCollider sur le mesh fusionné : la voiture roule sur le
            // relief exact (rampes incluses) sans des centaines de colliders.
            if (addCollider)
                target.AddComponent<MeshCollider>().sharedMesh = built;
        }

        /// <summary>
        /// Teste si un point est trop près d'un chemin — distance au SEGMENT,
        /// pas seulement au nœud : c'est ce qui garantit qu'aucun décor ne
        /// pousse au milieu d'une ligne droite de 60 m.
        /// </summary>
        private bool IsTooCloseToPath(Vector3 position)
        {
            float limit = clearanceRadius * clearanceRadius;

            for (int i = 0; i < pathStart.Count; i++)
            {
                Vector3 a = pathStart[i];
                Vector3 b = pathEnd[i];

                Vector3 ab = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float lengthSq = ab.sqrMagnitude;

                Vector3 delta;
                if (lengthSq < 0.001f)
                {
                    delta = new Vector3(position.x - a.x, 0f, position.z - a.z);
                }
                else
                {
                    Vector3 ap = new Vector3(position.x - a.x, 0f, position.z - a.z);
                    float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / lengthSq);
                    Vector3 closest = a + ab * t;
                    delta = new Vector3(position.x - closest.x, 0f, position.z - closest.z);
                }

                if (delta.sqrMagnitude < limit)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Teste si un point tombe dans les Terres de Lancement.
        /// Les rochers n'y sont pas admis : ils bloqueraient l'élan avant une
        /// rampe, et une rampe bloquée ne sert à rien.
        /// </summary>
        private bool IsInRampsArea(Vector3 position)
        {
            Vector3 delta = new Vector3(position.x, 0f, position.z - rampsAreaCenterZ);
            float radius = rampsAreaRadius + 30f;
            return delta.sqrMagnitude < radius * radius;
        }

        /// <summary>Teste si un point est réservé (tour, contrat).</summary>
        private bool IsReserved(Vector3 position)
        {
            for (int i = 0; i < reservedSpots.Count; i++)
            {
                if ((reservedSpots[i] - position).sqrMagnitude < 400f) // ~20 m
                    return true;
            }
            return false;
        }
    }
}
