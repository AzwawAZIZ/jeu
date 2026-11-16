using UnityEngine;

namespace AetherDrift.Core
{
    /// <summary>
    /// Fabrique de matériaux créés par code — compatible Built-in ET URP.
    ///
    /// Le piège classique : `new Material(shader)` puis `mat.color = ...` échoue
    /// silencieusement en HDRP/URP moderne (la couleur est sur `_BaseColor`).
    /// On renseigne donc les DEUX propriétés quand elles existent.
    /// </summary>
    public static class ProceduralMaterials
    {
        private static Shader cachedLitShader;
        private static Shader cachedUnlitShader;

        /// <summary>
        /// Shader "lit" : URP/Lit si dispo, sinon Standard (Built-in), sinon Unlit.
        /// </summary>
        public static Shader LitShader
        {
            get
            {
                if (cachedLitShader != null)
                    return cachedLitShader;

                cachedLitShader = Shader.Find("Universal Render Pipeline/Lit");
                if (cachedLitShader == null)
                    cachedLitShader = Shader.Find("Standard");
                if (cachedLitShader == null)
                    cachedLitShader = Shader.Find("Legacy Shaders/Diffuse");
                if (cachedLitShader == null)
                    cachedLitShader = Shader.Find("Unlit/Color");

                return cachedLitShader;
            }
        }

        /// <summary>
        /// Crée un matériau opaque.
        /// </summary>
        /// <param name="color">Couleur de base (albédo).</param>
        /// <param name="emission">Couleur d'émission, ou Color.black pour aucune.</param>
        /// <param name="emissionIntensity">Multiplicateur d'émission (1 = léger, 3+ = néon).</param>
        /// <param name="smoothness">0 = mat (low-poly), 1 = miroir.</param>
        /// <param name="metallic">0 = plastique/pierre, 1 = métal.</param>
        public static Material Create(string materialName, Color color,
            Color emission = default, float emissionIntensity = 0f,
            float smoothness = 0.15f, float metallic = 0f)
        {
            Material mat = new Material(LitShader) { name = materialName };

            // Couleur : on couvre Built-in (_Color) et URP (_BaseColor).
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);

            // Propriétés PBR.
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);

            // Émission.
            if (emissionIntensity > 0f)
            {
                Color emissive = emission * emissionIntensity; // * éventuel HDR
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", emissive);
                if (mat.HasProperty("_Emission"))
                    mat.SetColor("_Emission", emissive);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            return mat;
        }
    }
}
