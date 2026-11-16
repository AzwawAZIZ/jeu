using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using AetherDrift.Core;

namespace AetherDrift.Player
{
    /// <summary>
    /// Charge le bolide depuis un modèle 3D importé (OBJ) plutôt que de le
    /// générer en primitives.
    ///
    /// POURQUOI L'OBJ ET PAS LE FBX ?
    /// Le FBX exige le SDK d'import de Unity (disponible dans l'éditeur, mais
    /// pas garanti à l'exécution selon la plateforme et la version). L'OBJ, lui,
    /// est un format texte dont on maîtrise entièrement la lecture : aucune
    /// dépendance, aucun réglage d'import à faire, résultat identique en éditeur
    /// et en build. Les fichiers livrés ont été convertis depuis un modèle CC0.
    ///
    /// ARBORESCENCE ATTENDUE
    ///   Resources/Vehicles/muscle_blue/muscle_body.obj   (carrosserie)
    ///   Resources/Vehicles/muscle_blue/wheel_FL.obj      (roues séparées)
    ///   Resources/Vehicles/muscle_blue/wheel_FR.obj
    ///   Resources/Vehicles/muscle_blue/wheel_RL.obj
    ///   Resources/Vehicles/muscle_blue/wheel_RR.obj
    ///   Resources/Vehicles/muscle_blue/muscle_palette.png (texture de palette)
    ///
    /// REPLI : si un fichier manque, on revient automatiquement au châssis
    /// procédural (PlayerVehicle), avec un avertissement clair. Le jeu reste
    /// jouable en toutes circonstances.
    /// </summary>
    public static class VehicleModelLoader
    {
        public const string ModelFolder = "Vehicles/muscle_blue";

        /// <summary>Résultat du chargement : racine visuelle + roues.</summary>
        public class LoadedModel
        {
            public GameObject Root;
            public Transform[] Wheels = new Transform[0];
            public Transform[] SteeringWheels = new Transform[0];
            public Material BodyMaterial;
        }

        /// <summary>
        /// Construit le véhicule visuel complet sous <paramref name="parent"/>.
        /// Retourne null si les fichiers sont introuvables.
        /// </summary>
        /// <param name="scale">Échelle appliquée au modèle (1 = mètres du modèle).</param>
        /// <param name="bodyColor">Teinte multipliée sur la carrosserie (Color.white = couleurs d'origine).</param>
        public static LoadedModel Load(Transform parent, float scale, Color bodyColor)
        {
            // --- 1. Carrosserie ---
            Mesh bodyMesh = LoadObj(ModelFolder + "/muscle_body");
            if (bodyMesh == null)
            {
                Debug.LogWarning("[AETHER DRIFT] muscle_body.obj introuvable dans Resources/" + ModelFolder
                    + " — repli sur le châssis procédural.");
                return null;
            }

            LoadedModel result = new LoadedModel();

            GameObject root = new GameObject("ImportedVehicle");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;
            result.Root = root.transform;

            // --- 2. Matériau unique du pack (texture de palette plate) ---
            Material bodyMaterial = CreatePaletteMaterial(bodyColor);
            result.BodyMaterial = bodyMaterial;

            GameObject bodyObject = CreateMeshObject("Body", root.transform, bodyMesh, bodyMaterial);
            bodyObject.transform.localPosition = Vector3.zero;

            // --- 3. Roues (meshes centrés : on place le pivot à leur position) ---
            string[] wheelNames = { "wheel_FL", "wheel_FR", "wheel_RL", "wheel_RR" };
            Vector3[] wheelPositions =
            {
                new Vector3( 0.727f, 0.384f,  1.080f), // avant gauche
                new Vector3(-0.728f, 0.384f,  1.080f), // avant droit
                new Vector3( 0.685f, 0.434f, -1.000f), // arrière gauche
                new Vector3(-0.685f, 0.434f, -1.000f), // arrière droit
            };

            List<Transform> wheels = new List<Transform>();
            List<Transform> steering = new List<Transform>();

            for (int i = 0; i < wheelNames.Length; i++)
            {
                Mesh wheelMesh = LoadObj(ModelFolder + "/" + wheelNames[i]);
                if (wheelMesh == null)
                {
                    Debug.LogWarning("[AETHER DRIFT] " + wheelNames[i] + ".obj manquant : roue ignorée.");
                    continue;
                }

                // Pivot vide + mesh enfant : la rotation du pivot fait tourner la roue
                // proprement, sans décaler le mesh (il est déjà centré sur 0).
                GameObject pivot = new GameObject(wheelNames[i] + "_pivot");
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = wheelPositions[i];

                CreateMeshObject(wheelNames[i], pivot.transform, wheelMesh, bodyMaterial);

                WheelSpinner spinner = pivot.AddComponent<WheelSpinner>();
                // Les deux premières roues sont directrices.
                ConfigureSpinner(spinner, i < 2);

                wheels.Add(pivot.transform);
                if (i < 2)
                    steering.Add(pivot.transform);
            }

            result.Wheels = wheels.ToArray();
            result.SteeringWheels = steering.ToArray();

            return result;
        }

        /// <summary>
        /// Renseigne les champs privés du WheelSpinner via l'API de sérialisation
        /// n'est pas possible à l'exécution : on expose donc une méthode dédiée.
        /// </summary>
        private static void ConfigureSpinner(WheelSpinner spinner, bool isSteering)
        {
            spinner.Configure(isSteering);
        }

        // ============================================================
        // MATÉRIAU
        // ============================================================

        /// <summary>
        /// Matériau avec la texture de palette (16 couleurs plates).
        /// Le modèle n'a qu'un seul matériau : toutes les couleurs viennent de l'UV.
        /// </summary>
        private static Material CreatePaletteMaterial(Color tint)
        {
            Texture2D palette = Resources.Load<Texture2D>(ModelFolder + "/muscle_palette");

            Material material = ProceduralMaterials.Create(
                "VehiclePaint",
                tint,
                Color.black,
                0f,
                0.55f,   // un peu brillant : carrosserie de jouet
                0.15f);  // légèrement métallique

            if (palette != null)
            {
                // Point filtering : indispensable pour des couleurs parfaitement
                // plates (sinon les bords de la palette bavent).
                palette.filterMode = FilterMode.Point;
                palette.wrapMode = TextureWrapMode.Clamp;

                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", palette);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", palette);
            }
            else
            {
                Debug.LogWarning("[AETHER DRIFT] muscle_palette.png introuvable : le véhicule sera monochrome.");
            }

            return material;
        }

        // ============================================================
        // CHARGEMENT OBJ
        // ============================================================

        /// <summary>
        /// Crée un GameObject avec MeshFilter + MeshRenderer (ombres activées).
        /// </summary>
        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            return go;
        }

        /// <summary>
        /// Lit un OBJ depuis Resources et construit le Mesh correspondant.
        /// Retourne null si le fichier est absent.
        ///
        /// Note : pour que Resources.Load fonctionne, l'extension .obj est
        /// acceptée comme TextAsset par Unity (les fichiers .obj sont importés
        /// en tant que modèle 3D par défaut). On lit donc le fichier via
        /// Resources.Load&lt;TextAsset&gt; ; si l'importeur l'a transformé en
        /// Mesh, on récupère directement ce Mesh.
        /// </summary>
        private static Mesh LoadObj(string resourcePath)
        {
            // Cas A : Unity a importé l'OBJ comme modèle → on récupère son Mesh.
            Mesh[] imported = Resources.LoadAll<Mesh>(resourcePath);
            if (imported != null && imported.Length > 0)
                return imported[0];

            // Cas B : le fichier est disponible comme texte → on le parse nous-mêmes.
            TextAsset text = Resources.Load<TextAsset>(resourcePath);
            if (text != null)
                return ParseObj(text.text, resourcePath);

            return null;
        }

        /// <summary>
        /// Parseur OBJ minimaliste mais robuste : positions, normales, UV et faces
        /// (triangles et quads, avec ou sans index de texture/normale).
        /// </summary>
        private static Mesh ParseObj(string content, string meshName)
        {
            List<Vector3> positions = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            List<Vector3> outVertices = new List<Vector3>();
            List<Vector3> outNormals = new List<Vector3>();
            List<Vector2> outUVs = new List<Vector2>();
            List<int> outTriangles = new List<int>();

            string[] lines = content.Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li].Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                string[] parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                switch (parts[0])
                {
                    case "v":
                        positions.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
                        break;

                    case "vn":
                        normals.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
                        break;

                    case "vt":
                        outUVs.Add(new Vector2(ParseFloat(parts[1]), ParseFloat(parts[2])));
                        break;

                    case "f":
                        // Une face peut avoir 3, 4… sommets : on triangule en éventail.
                        int startVertex = outVertices.Count;
                        for (int i = 1; i < parts.Length; i++)
                        {
                            string[] indices = parts[i].Split('/');

                            // --- position ---
                            int positionIndex = ParseIndex(indices[0], positions.Count);
                            outVertices.Add(positions[positionIndex]);

                            // --- texture (facultative) ---
                            if (indices.Length > 1 && indices[1].Length > 0 && uvs.Count > 0)
                            {
                                int uvIndex = ParseIndex(indices[1], uvs.Count);
                                outUVs.Add(uvs[uvIndex]);
                            }
                            else
                            {
                                outUVs.Add(Vector2.zero);
                            }

                            // --- normale (facultative) ---
                            if (indices.Length > 2 && indices[2].Length > 0 && normals.Count > 0)
                            {
                                int normalIndex = ParseIndex(indices[2], normals.Count);
                                outNormals.Add(normals[normalIndex]);
                            }
                            else
                            {
                                outNormals.Add(Vector3.up);
                            }
                        }

                        for (int i = 1; i < parts.Length - 1; i++)
                        {
                            outTriangles.Add(startVertex);
                            outTriangles.Add(startVertex + i);
                            outTriangles.Add(startVertex + i + 1);
                        }
                        break;
                }
            }

            if (outVertices.Count == 0)
                return null;

            Mesh mesh = new Mesh { name = meshName };

            if (outVertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(outVertices);
            mesh.SetNormals(outNormals);
            mesh.SetUVs(0, outUVs);
            mesh.SetTriangles(outTriangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>Parse un float en culture invariante (le « . » décimal est standard en OBJ).</summary>
        private static float ParseFloat(string value)
        {
            return float.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Convertit un index OBJ (1-based, négatif = relatif à la fin) en index 0-based.
        /// </summary>
        private static int ParseIndex(string token, int count)
        {
            int raw = int.Parse(token, CultureInfo.InvariantCulture);
            int index = raw < 0 ? count + raw : raw - 1;

            if (index < 0) index = 0;
            if (index >= count) index = count - 1;

            return index;
        }
    }
}
