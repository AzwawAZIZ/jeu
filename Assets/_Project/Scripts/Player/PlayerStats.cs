using System;
using UnityEngine;

namespace AetherDrift.Player
{
    /// <summary>
    /// Ressources du pilote : ÉTHER (charge de drift) et ENDURANCE (dash).
    /// Sert aussi de référentiel d'invulnérabilité (i-frames) pour tout le véhicule.
    ///
    /// ARCHITECTURE — pourquoi un script séparé ?
    /// Le CarController ne fait que *lire* la physique ; les ressources et les
    /// règles de jeu (invulnérabilité, régénération) vivent ici. Résultat :
    ///   - l'IA, le HUD et les futurs boss peuvent lire l'état sans toucher au contrôleur,
    ///   - les règles se testent et s'équilibrent dans l'Inspector,
    ///   - CarController reste court et lisible.
    ///
    /// ÉTHER (0 → 100)
    ///   Se charge en drift (Espace maintenu + vitesse suffisante).
    ///   Le relâchement de l'Espace convertit la charge en impulsion de vitesse
    ///   (consommée par CarController via ConsumeEther()).
    ///
    /// ENDURANCE (0 → 100)
    ///   Consommée par le dash d'esquive. Régénère avec un délai après usage,
    ///   ce qui crée la tension "Souls" : on ne peut pas spammer les esquives.
    ///
    /// ÉVÉNEMENTS : exposés en C# pour que l'UI (Étape 5) s'y abonne sans
    /// interrogation par frame (polling). Aucun impact sur le build.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        // ============================================================
        // ÉTHER
        // ============================================================

        [Header("Éther (drift)")]
        [Tooltip("Charge maximale d'éther.")]
        [SerializeField] private float maxEther = 100f;

        [Tooltip("Vitesse de charge en drift (unités/seconde).")]
        [SerializeField] private float etherGainRate = 42f;

        [Tooltip("Vitesse de charge en drift serré (virage fort), en plus.")]
        [SerializeField] private float sharpDriftBonus = 22f;

        [Tooltip("Décharge passive quand on ne drifte pas (unités/seconde).")]
        [SerializeField] private float etherDecayRate = 14f;

        [Tooltip("Délai avant que la décharge passive commence (secondes).")]
        [SerializeField] private float etherDecayDelay = 1.4f;

        // ============================================================
        // ENDURANCE
        // ============================================================

        [Header("Endurance (esquive)")]
        [Tooltip("Endurance maximale.")]
        [SerializeField] private float maxStamina = 100f;

        [Tooltip("Coût d'un dash d'esquive.")]
        [SerializeField] private float dashStaminaCost = 34f;

        [Tooltip("Vitesse de régénération (unités/seconde).")]
        [SerializeField] private float staminaRegenRate = 26f;

        [Tooltip("Délai avant régénération après une esquive (secondes).")]
        [SerializeField] private float staminaRegenDelay = 0.65f;

        // ============================================================
        // INVULNÉRABILITÉ
        // ============================================================

        [Header("Invulnérabilité (i-frames)")]
        [Tooltip("Durée d'invulnérabilité d'un dash (secondes).")]
        [SerializeField] private float invulnerabilityDuration = 0.4f;

        // ============================================================
        // ÉTAT PUBLIC (lecture seule pour les autres systèmes)
        // ============================================================

        public float Ether { get; private set; }
        public float MaxEther => maxEther;
        public float EtherRatio => maxEther > 0f ? Ether / maxEther : 0f;

        public float Stamina { get; private set; }
        public float MaxStamina => maxStamina;
        public float StaminaRatio => maxStamina > 0f ? Stamina / maxStamina : 0f;

        public float DashStaminaCost => dashStaminaCost;

        /// <summary>Vrai pendant les i-frames : les dégâts sont ignorés.</summary>
        public bool IsInvulnerable => invulnerabilityTimer > 0f;

        /// <summary>Temps restant d'invulnérabilité (secondes).</summary>
        public float InvulnerabilityRemaining => Mathf.Max(0f, invulnerabilityTimer);

        /// <summary>Vrai si l'endurance suffit pour déclencher une esquive.</summary>
        public bool CanDash => Stamina >= dashStaminaCost && !IsInvulnerable;

        // ============================================================
        // ÉVÉNEMENTS
        // ============================================================

        /// <summary>Déclenché au début d'un dash (pour le VFX / le son).</summary>
        public event Action OnDashStarted;

        /// <summary>Déclenché quand un coup est absorbé par les i-frames.</summary>
        public event Action OnDamageDodged;

        /// <summary>Déclenché quand l'éther vient d'atteindre son maximum.</summary>
        public event Action OnEtherFull;

        // ============================================================
        // INTERNES
        // ============================================================

        private float invulnerabilityTimer;
        private float timeSinceEtherUse;
        private float timeSinceStaminaUse;
        private bool etherWasFull;

        private void Awake()
        {
            // On démarre avec une réserve d'éther vide mais une endurance pleine :
            // le joueur doit mériter son premier boost en driftant.
            Ether = 0f;
            Stamina = maxStamina;
            etherWasFull = false;
        }

        private void Update()
        {
            UpdateInvulnerability();
            UpdateEtherDecay();
            UpdateStaminaRegen();
        }

        // ============================================================
        // ÉTHER
        // ============================================================

        /// <summary>
        /// Charge l'éther pendant le drift.
        /// </summary>
        /// <param name="deltaTime">Delta time courant.</param>
        /// <param name="driftIntensity">0 = ligne droite, 1 = drift maximal (virage serré).</param>
        public void ChargeEther(float deltaTime, float driftIntensity)
        {
            float rate = etherGainRate + sharpDriftBonus * Mathf.Clamp01(driftIntensity);
            Ether = Mathf.Clamp(Ether + rate * deltaTime, 0f, maxEther);
            timeSinceEtherUse = 0f;

            if (!etherWasFull && Ether >= maxEther)
            {
                etherWasFull = true;
                OnEtherFull?.Invoke();
            }
        }

        /// <summary>
        /// Consomme toute la charge d'éther et retourne la quantité réellement utilisée.
        /// Appelé par CarController au relâchement de la touche de drift.
        /// </summary>
        public float ConsumeEther()
        {
            float consumed = Ether;
            Ether = 0f;
            etherWasFull = false;
            timeSinceEtherUse = 0f;
            return consumed;
        }

        private void UpdateEtherDecay()
        {
            timeSinceEtherUse += Time.deltaTime;

            if (timeSinceEtherUse < etherDecayDelay)
                return;

            if (Ether <= 0f)
                return;

            // Décharge passive : la charge ne se garde pas indéfiniment,
            // ce qui pousse le joueur à relâcher au bon moment.
            Ether = Mathf.Max(0f, Ether - etherDecayRate * Time.deltaTime);
            etherWasFull = false;
        }

        // ============================================================
        // ENDURANCE
        // ============================================================

        /// <summary>
        /// Tente de payer une esquive. Retourne false si l'endurance manque.
        /// </summary>
        public bool TrySpendStaminaForDash()
        {
            if (Stamina < dashStaminaCost)
                return false;

            Stamina -= dashStaminaCost;
            timeSinceStaminaUse = 0f;
            return true;
        }

        private void UpdateStaminaRegen()
        {
            timeSinceStaminaUse += Time.deltaTime;

            if (timeSinceStaminaUse < staminaRegenDelay)
                return;

            if (Stamina >= maxStamina)
                return;

            Stamina = Mathf.Min(maxStamina, Stamina + staminaRegenRate * Time.deltaTime);
        }

        // ============================================================
        // INVULNÉRABILITÉ
        // ============================================================

        /// <summary>
        /// Octroie les i-frames (appelé par CarController au début du dash).
        /// </summary>
        public void TriggerInvulnerability()
        {
            invulnerabilityTimer = invulnerabilityDuration;
            OnDashStarted?.Invoke();
        }

        /// <summary>
        /// Octroie une invulnérabilité d'une durée donnée (réutilisable par
        /// les futurs boss / scripts de dégâts).
        /// </summary>
        public void TriggerInvulnerability(float duration)
        {
            invulnerabilityTimer = Mathf.Max(invulnerabilityTimer, duration);
        }

        private void UpdateInvulnerability()
        {
            if (invulnerabilityTimer > 0f)
                invulnerabilityTimer -= Time.deltaTime;
        }

        /// <summary>
        /// Point d'entrée unique des dégâts : retourne false si le coup est
        /// absorbé par les i-frames. À utiliser par tous les futurs ennemis.
        /// </summary>
        /// <param name="amount">Montant de dégâts (0 aux étapes 1-2 : pas encore de PV).</param>
        /// <returns>true si les dégâts sont appliqués, false s'ils sont esquivés.</returns>
        public bool TryTakeDamage(float amount)
        {
            if (IsInvulnerable)
            {
                OnDamageDodged?.Invoke();
                return false;
            }

            // Aucun système de points de vie à cette étape : le hook est en place
            // pour les Seigneurs Mécaniques (Étape 4).
            if (amount > 0f)
                Debug.Log("[AETHER DRIFT] Dégâts reçus : " + amount);

            return true;
        }

        // ============================================================
        // OUTILS DE TEST
        // ============================================================

        /// <summary>Remplit instantanément l'éther (utile pour tester le boost).</summary>
        public void DebugFillEther()
        {
            Ether = maxEther;
            timeSinceEtherUse = 0f;
        }
    }
}
