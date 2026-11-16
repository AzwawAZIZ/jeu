using System;
using System.Collections.Generic;
using UnityEngine;
using AetherDrift.Core;
using AetherDrift.Enemies;
using AetherDrift.Player;

namespace AetherDrift.World
{
    /// <summary>
    /// Gestionnaire de contrats — ADN GTA — ÉTAPE 3.
    ///
    /// BOUCLE D'UNE MISSION
    ///   1. Le joueur traverse une balise de prise de contrat (cylindre lumineux).
    ///   2. Un chronomètre démarre : livrer l'artefact au point B.
    ///   3. Deux chasseurs IA apparaissent et tentent de le percuter en route.
    ///   4. À l'arrivée : gain, temps réalisé, chasseurs désactivés.
    ///      En cas d'échec (temps écoulé) : contrat échoué, chasseurs retirés.
    ///
    /// INTERFACE (temporaire, remplacée par le HUD de l'Étape 5)
    ///   L'objectif, le chrono et la distance restante s'affichent en haut de
    ///   l'écran. C'est volontairement minimal : ce qui compte à cette étape,
    ///   c'est que la boucle de jeu soit jouable et lisible.
    ///
    /// ÉVÉNEMENTS
    ///   OnContractStarted / OnContractCompleted / OnContractFailed permettent
    ///   au futur HUD et à la mini-map de se brancher sans polling.
    /// </summary>
    public class ContractManager : MonoBehaviour
    {
        // ============================================================
        // ÉVÉNEMENTS
        // ============================================================

        public event Action<Contract> OnContractStarted;
        public event Action<Contract, float> OnContractCompleted;   // contrat + temps réalisé
        public event Action<Contract> OnContractFailed;

        // ============================================================
        // DÉFINITION D'UN CONTRAT
        // ============================================================

        /// <summary>Un contrat de livraison d'artefact.</summary>
        [Serializable]
        public class Contract
        {
            public string Name = "Contrat";
            public Vector3 PickupPosition;
            public Vector3 DeliveryPosition;
            public float TimeLimit = 40f;

            [NonSerialized] public GameObject PickupMarker;
            [NonSerialized] public GameObject DeliveryMarker;
            [NonSerialized] public bool IsTaken;
            [NonSerialized] public bool IsCompleted;

            /// <summary>
            /// Vrai après un échec : le contrat ne peut pas être repris tant que
            /// le joueur n'est pas SORTI de la balise. Sans ça, rester immobile
            /// dans la balise relancerait la mission en boucle, indéfiniment.
            /// </summary>
            [NonSerialized] public bool WaitingForExit;
        }

        // ============================================================
        // RÉGLAGES
        // ============================================================

        [Header("Contrats")]
        [Tooltip("Nombre de contrats générés dans le monde.")]
        [SerializeField] private int contractCount = 3;

        [Tooltip("Rayon de déclenchement des balises.")]
        [SerializeField] private float markerRadius = 6f;

        [Tooltip("Distance minimale entre le point de prise et de livraison.")]
        [SerializeField] private float minDeliveryDistance = 260f;

        [Tooltip("Distance maximale entre le point de prise et de livraison.")]
        [SerializeField] private float maxDeliveryDistance = 480f;

        [Header("Chasseurs")]
        [Tooltip("Nombre de chasseurs lancés à chaque contrat.")]
        [SerializeField] private int chasersPerContract = 2;

        [Tooltip("Distance d'apparition des chasseurs derrière le joueur.")]
        [SerializeField] private float chaserSpawnDistance = 55f;

        [Header("Interface de test")]
        [Tooltip("Affiche l'objectif, le chrono et la distance à l'écran.")]
        [SerializeField] private bool showContractHud = true;

        // ============================================================
        // ÉTAT
        // ============================================================

        private readonly List<Contract> contracts = new List<Contract>();
        private readonly List<ChaserAI> activeChasers = new List<ChaserAI>();

        private Transform player;
        private Transform chasersRoot;

        private Contract activeContract;
        private float remainingTime;
        private float elapsedTime;

        public IReadOnlyList<Contract> Contracts => contracts;
        public Contract ActiveContract => activeContract;
        public float RemainingTime => remainingTime;
        public bool IsContractActive => activeContract != null;

        // ============================================================
        // INITIALISATION
        // ============================================================

        /// <summary>
        /// Choisit les points de prise et de livraison, SANS créer la géométrie.
        ///
        /// À appeler AVANT <see cref="WorldGenerator.Generate"/> : le générateur
        /// doit connaître ces emplacements pour ne pas y planter un pilier.
        /// </summary>
        /// <param name="count">Nombre de contrats à créer (0 = valeur de l'Inspector).</param>
        public void PlanContracts(System.Random random, WorldGenerator world, int count = 0)
        {
            contracts.Clear();

            int target = count > 0 ? count : contractCount;

            for (int i = 0; i < target; i++)
            {
                Vector3 pickup = world.FindFreeSpot(random, 60f, world.WorldHalfSize * 0.75f, Vector3.zero);

                // Point de livraison : assez loin pour que la course ait du sens.
                Vector3 delivery = pickup;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    Vector3 candidate = world.FindFreeSpot(random, 80f, world.WorldHalfSize * 0.85f, Vector3.zero);
                    float distance = Vector3.Distance(pickup, candidate);

                    if (distance >= minDeliveryDistance && distance <= maxDeliveryDistance)
                    {
                        delivery = candidate;
                        break;
                    }
                }

                Contract contract = new Contract
                {
                    Name = "Contrat " + (i + 1) + " — Artefact runique",
                    PickupPosition = pickup,
                    DeliveryPosition = delivery,
                    // 15 s de marge + le trajet à 43 km/h de moyenne.
                    // (Vérifié au calcul : 260 m -> 37 s, 480 m -> 55 s.)
                    TimeLimit = 15f + Vector3.Distance(pickup, delivery) / 12f,
                };

                contracts.Add(contract);
            }
        }

        /// <summary>
        /// Crée la géométrie des contrats déjà planifiés et branche le joueur.
        /// À appeler APRÈS <see cref="WorldGenerator.Generate"/>.
        /// </summary>
        public void Initialize(Transform playerTransform)
        {
            player = playerTransform;

            GameObject root = new GameObject("Contracts");
            root.transform.SetParent(transform, false);

            GameObject chaserRoot = new GameObject("Chasers");
            chaserRoot.transform.SetParent(transform, false);
            chasersRoot = chaserRoot.transform;

            for (int i = 0; i < contracts.Count; i++)
            {
                Contract contract = contracts[i];
                contract.PickupMarker = CreatePickupMarker(root.transform, contract);
                contract.DeliveryMarker = CreateDeliveryMarker(root.transform, contract);
            }

            Debug.Log("[AETHER DRIFT] " + contracts.Count + " contrats générés. Traverse une balise cyan pour démarrer.");
        }

        // ============================================================
        // BOUCLE
        // ============================================================

        private void Update()
        {
            if (player == null)
                return;

            // Les balises tournent en permanence : celle de livraison doit rester
            // repérable pendant TOUTE la course, pas seulement avant.
            AnimateMarkers();

            if (activeContract == null)
            {
                CheckPickups();
                return;
            }

            UpdateActiveContract();
        }

        /// <summary>Détecte l'entrée du joueur dans une balise de prise de contrat.</summary>
        private void CheckPickups()
        {
            for (int i = 0; i < contracts.Count; i++)
            {
                Contract contract = contracts[i];
                if (contract.IsTaken || contract.IsCompleted)
                    continue;

                Vector3 delta = player.position - contract.PickupPosition;
                delta.y = 0f;

                bool inside = delta.sqrMagnitude <= markerRadius * markerRadius;

                // Après un échec, il faut d'abord sortir de la balise.
                if (contract.WaitingForExit)
                {
                    if (!inside)
                        contract.WaitingForExit = false;
                    continue;
                }

                if (inside)
                {
                    StartContract(contract);
                    return;
                }
            }
        }

        /// <summary>Fait tourner les balises pour qu'elles se repèrent de loin.</summary>
        private void AnimateMarkers()
        {
            float spin = Time.time * 45f;
            for (int i = 0; i < contracts.Count; i++)
            {
                Contract contract = contracts[i];

                if (contract.PickupMarker != null)
                    contract.PickupMarker.transform.rotation = Quaternion.Euler(0f, spin, 0f);

                if (contract.DeliveryMarker != null)
                {
                    contract.DeliveryMarker.transform.rotation = Quaternion.Euler(0f, -spin, 0f);

                    // La balise de livraison n'est visible QUE pendant la mission
                    // en cours : avant, on ne divulgue pas la destination ;
                    // après, la course est terminée. (Bug évité : se fier au seul
                    // drapeau IsTaken la rallumait après la livraison.)
                    contract.DeliveryMarker.SetActive(contract == activeContract && contract.IsTaken);
                }
            }
        }

        /// <summary>Chrono, détection de l'arrivée et de l'échec.</summary>
        private void UpdateActiveContract()
        {
            remainingTime -= Time.deltaTime;
            elapsedTime += Time.deltaTime;

            // Un chasseur peut se retirer de lui-même (distancé) : on nettoie
            // la liste pour que le compteur affiché reste juste.
            for (int i = activeChasers.Count - 1; i >= 0; i--)
            {
                if (activeChasers[i] == null)
                    activeChasers.RemoveAt(i);
            }

            // --- Arrivée ? ---
            Vector3 delta = player.position - activeContract.DeliveryPosition;
            delta.y = 0f;

            if (delta.sqrMagnitude <= markerRadius * markerRadius)
            {
                CompleteContract();
                return;
            }

            // --- Temps écoulé ? ---
            if (remainingTime <= 0f)
            {
                FailContract();
            }
        }

        // ============================================================
        // DÉMARRAGE / FIN
        // ============================================================

        /// <summary>Démarre un contrat : chrono, balise de livraison, chasseurs.</summary>
        public void StartContract(Contract contract)
        {
            // Sécurité : une seule mission à la fois.
            if (contract == null || activeContract != null)
                return;

            activeContract = contract;
            contract.IsTaken = true;
            remainingTime = contract.TimeLimit;
            elapsedTime = 0f;

            if (contract.DeliveryMarker != null)
                contract.DeliveryMarker.SetActive(true);

            // La balise de prise s'éteint : le joueur doit aller au point B.
            if (contract.PickupMarker != null)
                contract.PickupMarker.SetActive(false);

            SpawnChasers();

            Debug.Log("[AETHER DRIFT] Contrat accepté : " + contract.Name
                + " — livrer en " + contract.TimeLimit.ToString("0") + " s. "
                + chasersPerContract + " chasseurs lancés.");

            OnContractStarted?.Invoke(contract);
        }

        /// <summary>Le joueur a livré l'artefact.</summary>
        private void CompleteContract()
        {
            Contract contract = activeContract;
            contract.IsCompleted = true;
            activeContract = null;

            if (contract.DeliveryMarker != null)
                contract.DeliveryMarker.SetActive(false);

            DespawnChasers();

            Debug.Log("[AETHER DRIFT] Contrat rempli : " + contract.Name
                + " en " + elapsedTime.ToString("0.0") + " s.");

            OnContractCompleted?.Invoke(contract, elapsedTime);
        }

        /// <summary>Le temps est écoulé.</summary>
        private void FailContract()
        {
            Contract contract = activeContract;
            activeContract = null;

            if (contract.DeliveryMarker != null)
                contract.DeliveryMarker.SetActive(false);

            // Le contrat redevient disponible : on peut réessayer — mais
            // seulement après avoir quitté la balise (voir WaitingForExit).
            contract.IsTaken = false;
            contract.WaitingForExit = true;
            if (contract.PickupMarker != null)
                contract.PickupMarker.SetActive(true);

            DespawnChasers();

            Debug.Log("[AETHER DRIFT] Contrat échoué (temps écoulé) : " + contract.Name + ". Il est de nouveau disponible.");

            OnContractFailed?.Invoke(contract);
        }

        // ============================================================
        // CHASSEURS
        // ============================================================

        /// <summary>
        /// Fait apparaître les chasseurs derrière le joueur, hors de sa vue.
        /// </summary>
        private void SpawnChasers()
        {
            DespawnChasers();

            if (player == null || chasersRoot == null)
                return;

            Vector3 behind = -player.forward;

            for (int i = 0; i < chasersPerContract; i++)
            {
                // Deux positions décalées latéralement, pour ne pas apparaître empilés.
                float side = (i % 2 == 0) ? -1f : 1f;
                Vector3 offset = behind * chaserSpawnDistance + player.right * (side * 18f);

                Vector3 spawn = player.position + offset;

                // On pose le chasseur SUR le sol, pas à une hauteur arbitraire :
                // sinon, si le joueur est sur une rampe à 8 m, le chasseur
                // apparaît sous le terrain (ou tombe de 40 m).
                if (Physics.Raycast(spawn + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f,
                        ~0, QueryTriggerInteraction.Ignore))
                {
                    spawn = hit.point + Vector3.up * 1.5f;
                }
                else
                {
                    spawn = new Vector3(spawn.x, 2f, spawn.z);
                }

                ChaserAI chaser = ChaserAI.Create(chasersRoot, spawn, player);
                activeChasers.Add(chaser);
            }
        }

        /// <summary>Retire tous les chasseurs actifs.</summary>
        private void DespawnChasers()
        {
            for (int i = 0; i < activeChasers.Count; i++)
            {
                if (activeChasers[i] != null)
                    activeChasers[i].Despawn();
            }
            activeChasers.Clear();
        }

        // ============================================================
        // BALISES
        // ============================================================

        /// <summary>
        /// Balise de prise de contrat : cylindre creux lumineux + flèche.
        /// Purement visuelle (aucun collider : on détecte par distance, ce qui
        /// fonctionne même à très grande vitesse).
        /// </summary>
        private GameObject CreatePickupMarker(Transform parent, Contract contract)
        {
            // Cyan : point de prise, visible en permanence.
            return CreateMarker(parent, contract.PickupPosition, "PickupMarker",
                new Color(0.15f, 0.9f, 1f));
        }

        private GameObject CreateDeliveryMarker(Transform parent, Contract contract)
        {
            // Or : destination, révélée seulement une fois le contrat accepté.
            GameObject marker = CreateMarker(parent, contract.DeliveryPosition, "DeliveryMarker",
                new Color(1f, 0.72f, 0.2f));
            marker.SetActive(false);
            return marker;
        }

        private GameObject CreateMarker(Transform parent, Vector3 position, string name, Color color)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;

            LowPolyMesh mesh = new LowPolyMesh();
            const int segments = 12;
            const float radius = 5f;
            float step = (Mathf.PI * 2f) / segments;

            // Trois piliers lumineux en trépied : lisible de très loin.
            for (int i = 0; i < 3; i++)
            {
                float angle = step * (segments / 3f) * i;
                Vector3 pos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                MeshUtils.AddBox(mesh, pos + Vector3.up * 7f, new Vector3(0.5f, 14f, 0.5f));
            }

            // Anneau au sol.
            LowPolyMesh ringMesh = new LowPolyMesh();
            ringMesh.AddFlatAnnulus(Vector3.up * 0.1f, radius - 0.6f, radius + 0.4f, 32);

            GameObject pillars = new GameObject("Pillars");
            pillars.transform.SetParent(marker.transform, false);
            pillars.AddComponent<MeshFilter>().sharedMesh = mesh.Build(name + "Pillars");

            GameObject ring = new GameObject("Ring");
            ring.transform.SetParent(marker.transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = ringMesh.Build(name + "Ring");

            Material material = ProceduralMaterials.Create(name + "Mat", color, color, 3f, 0.6f, 0f);

            MeshRenderer pillarsRenderer = pillars.AddComponent<MeshRenderer>();
            pillarsRenderer.sharedMaterial = material;
            pillarsRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            MeshRenderer ringRenderer = ring.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = material;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return marker;
        }

        // ============================================================
        // INTERFACE DE TEST (remplacée par le HUD de l'Étape 5)
        // ============================================================

        private void OnGUI()
        {
            if (!showContractHud || player == null)
                return;

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.86f, 0.5f) }
            };

            GUIStyle body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };

            if (activeContract == null)
            {
                // Compte les contrats restants.
                int remaining = 0;
                for (int i = 0; i < contracts.Count; i++)
                    if (!contracts[i].IsCompleted) remaining++;

                GUI.Label(new Rect(Screen.width - 340f, 24f, 320f, 24f),
                    "CONTRATS DISPONIBLES : " + remaining, title);

                if (remaining > 0)
                {
                    GUI.Label(new Rect(Screen.width - 340f, 48f, 320f, 22f),
                        "Traverse une balise cyan", body);
                }
                return;
            }

            float distance = Vector3.Distance(player.position, activeContract.DeliveryPosition);
            float ratio = Mathf.Clamp01(remainingTime / Mathf.Max(1f, activeContract.TimeLimit));

            GUI.Label(new Rect(Screen.width - 380f, 20f, 360f, 24f), activeContract.Name, title);

            // Chrono, qui vire au rouge sous 10 s.
            GUIStyle timer = new GUIStyle(body);
            timer.fontStyle = FontStyle.Bold;
            timer.normal.textColor = remainingTime < 10f
                ? new Color(1f, 0.35f, 0.3f)
                : new Color(0.85f, 0.95f, 1f);

            GUI.Label(new Rect(Screen.width - 380f, 44f, 360f, 22f),
                "TEMPS : " + remainingTime.ToString("0.0") + " s", timer);

            GUI.Label(new Rect(Screen.width - 380f, 66f, 360f, 22f),
                "DISTANCE : " + distance.ToString("0") + " m", body);

            GUI.Label(new Rect(Screen.width - 380f, 88f, 360f, 22f),
                "CHASSEURS : " + activeChasers.Count, body);

            // Barre de temps restant.
            Rect barBg = new Rect(Screen.width - 380f, 112f, 340f, 10f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(barBg, Texture2D.whiteTexture);

            GUI.color = Color.Lerp(new Color(1f, 0.3f, 0.25f), new Color(0.3f, 0.9f, 1f), ratio);
            GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * ratio, barBg.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
