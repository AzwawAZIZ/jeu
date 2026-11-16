using System.Collections.Generic;
using UnityEngine;
using AetherDrift.Player;

namespace AetherDrift.World
{
    /// <summary>
    /// Aide à la descente — ÉTAPE 3.
    ///
    /// PROBLÈME RÉSOLU
    ///   Le sommet d'une tour de synchronisation est à 42-50 m du sol. Après
    ///   l'impulsion, le joueur se retrouve sur une plateforme étroite avec une
    ///   seule issue : redescendre 2,5 tours de spirale. Amusant une fois,
    ///   pénible la troisième.
    ///
    /// SOLUTION (ADN « saut de foi » d'Assassin's Creed)
    ///   La touche R lance une descente guidée : la voiture suit la spirale vers
    ///   le bas à vitesse constante, puis se pose au pied de la tour. Ce n'est
    ///   pas une téléportation : on voit le véhicule descendre.
    ///
    /// FONCTIONNEMENT
    ///   - La conduite (<see cref="CarController"/>) est désactivée le temps de
    ///     la descente : aucune force parasite ne vient contrarier la trajectoire.
    ///   - Le Rigidbody passe en cinématique et suit l'itinéraire par
    ///     MovePosition : le résultat est identique quel que soit le framerate.
    ///   - À l'arrivée, tout est rendu au joueur (gravité, conduite, vitesse nulle).
    /// </summary>
    public class DescentHelper : MonoBehaviour
    {
        // ============================================================
        // RÉGLAGES
        // ============================================================

        [Tooltip("Vitesse de descente le long de l'itinéraire (m/s).")]
        [SerializeField] private float descentSpeed = 26f;

        [Tooltip("Durée maximale d'une descente automatique (sécurité).")]
        [SerializeField] private float maxDescentDuration = 14f;

        // ============================================================
        // ÉTAT
        // ============================================================

        private Rigidbody rb;
        private CarController controller;
        private Transform player;

        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<float> cumulativeLength = new List<float>();

        private float travelled;
        private float totalLength;
        private float descentTimer;
        private bool isDescending;

        /// <summary>Une descente automatique est-elle en cours ?</summary>
        public bool IsDescending => isDescending;

        // ============================================================
        // API
        // ============================================================

        /// <summary>Prépare l'aide à la descente sur le véhicule du joueur.</summary>
        public static DescentHelper Create(Transform playerTransform)
        {
            if (playerTransform == null)
                return null;

            DescentHelper helper = playerTransform.gameObject.AddComponent<DescentHelper>();
            helper.player = playerTransform;
            return helper;
        }

        /// <summary>
        /// Lance la descente guidée le long d'un itinéraire (2 points minimum).
        /// </summary>
        public void BeginDescent(List<Vector3> route)
        {
            if (player == null || isDescending || route == null || route.Count < 2)
                return;

            if (rb == null)
                rb = player.GetComponent<Rigidbody>();

            if (controller == null)
                controller = player.GetComponent<CarController>();

            if (rb == null)
                return;

            // Le premier point est TOUJOURS la position réelle du véhicule :
            // sinon, une descente lancée avec un point de départ théorique
            // ferait un saut visible dès la première image.
            route[0] = rb.position;

            // --- Préparation de l'itinéraire et des longueurs cumulées ---
            points.Clear();
            cumulativeLength.Clear();

            for (int i = 0; i < route.Count; i++)
            {
                points.Add(route[i]);

                if (i == 0)
                {
                    cumulativeLength.Add(0f);
                }
                else
                {
                    cumulativeLength.Add(cumulativeLength[i - 1] + Vector3.Distance(points[i - 1], points[i]));
                }
            }

            totalLength = cumulativeLength[cumulativeLength.Count - 1];
            if (totalLength < 1f)
                return;

            travelled = 0f;
            descentTimer = maxDescentDuration;
            isDescending = true;

            // La conduite est coupée : on ne veut pas que les forces du moteur
            // luttent contre la trajectoire.
            if (controller != null)
                controller.enabled = false;

            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Debug.Log("[AETHER DRIFT] Descente guidée : " + totalLength.ToString("0") + " m d'itinéraire.");
        }

        private void OnDisable()
        {
            // Si le script est désactivé en pleine descente (rechargement de
            // scène, par exemple), on rend le contrôle au joueur : sinon il
            // resterait en apesanteur, sans commandes.
            if (isDescending)
                EndDescent();
        }

        private void FixedUpdate()
        {
            if (!isDescending || rb == null)
                return;

            descentTimer -= Time.fixedDeltaTime;

            // Sécurité : on ne laisse jamais le joueur coincé en descente.
            if (descentTimer <= 0f)
            {
                EndDescent();
                return;
            }

            travelled += descentSpeed * Time.fixedDeltaTime;

            if (travelled >= totalLength)
            {
                rb.MovePosition(points[points.Count - 1]);
                EndDescent();
                return;
            }

            Vector3 position = EvaluatePosition(travelled);
            rb.MovePosition(position);

            // Orientation : vers le point suivant, en ignorant la pente pour que
            // le châssis reste stable (la voiture ne "pique pas du nez").
            Vector3 ahead = EvaluatePosition(Mathf.Min(travelled + 4f, totalLength));
            Vector3 direction = new Vector3(ahead.x - position.x, 0f, ahead.z - position.z);

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, 10f * Time.fixedDeltaTime));
            }
        }

        /// <summary>Position sur la polyligne à une distance donnée depuis le départ.</summary>
        private Vector3 EvaluatePosition(float distance)
        {
            // Recherche du segment courant. L'itinéraire fait quelques dizaines
            // de points : une boucle simple est plus rapide qu'une recherche
            // dichotomique, et sans allocation.
            for (int i = 1; i < points.Count; i++)
            {
                if (distance <= cumulativeLength[i])
                {
                    float segmentLength = cumulativeLength[i] - cumulativeLength[i - 1];
                    if (segmentLength < 0.0001f)
                        return points[i];

                    float t = (distance - cumulativeLength[i - 1]) / segmentLength;
                    return Vector3.Lerp(points[i - 1], points[i], t);
                }
            }

            return points[points.Count - 1];
        }

        /// <summary>Rend le contrôle au joueur, au sol.</summary>
        private void EndDescent()
        {
            if (!isDescending)
                return;

            isDescending = false;
            points.Clear();
            cumulativeLength.Clear();

            if (rb == null)
                return;

            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (controller != null)
                controller.enabled = true;

            Debug.Log("[AETHER DRIFT] Descente terminée : contrôle rendu au joueur. "
                + "Les contrats t'attendent sur la carte (balises cyan).");
        }
    }
}
