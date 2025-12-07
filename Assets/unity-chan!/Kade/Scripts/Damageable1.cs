using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Kade
{
    public class Damageable : MonoBehaviour
    {
        EnemyTest chanScript;
        Transform player;

        [SerializeField] int maxHealth;
        [SerializeField] float iTime = 0.5f;
        [SerializeField] Material flashMaterial;
        [SerializeField] Renderer[] renderers;
        [SerializeField] GameObject damageEffectPrefab;

        [SerializeField] AudioClipCollection hurtSounds;
        [SerializeField] AudioClipCollection deathSounds;

        [SerializeField] public float phaseTwoStart;
        [SerializeField] public float phaseThreeStart;

        public UnityEvent<int> OnInitialize;
        public UnityEvent<Kade.Damage> OnHit;
        public UnityEvent OnDeath;
        public UnityEvent<int, int> OnHealthChanged;

        public int currentHealth;
        float timeSinceHit = 0;

        float sequenceTimer = 0f;   // Counter window for sequence hits
        int sequenceHits = 0;       // Hits in the current sequence
        [SerializeField] float startShield = 5f;  // Window length to accumulate hits
        [SerializeField] int hitThreshold = 3;    // Hits needed to trigger block

        bool isUnityChan = false;

        void Start()
        {
            chanScript = GetComponent<EnemyTest>();
            isUnityChan = (chanScript != null);

            currentHealth = maxHealth;
            OnInitialize?.Invoke(maxHealth);
            OnHealthChanged?.Invoke(maxHealth, maxHealth);

            player = FindObjectOfType<PlayerLogic>().transform;
        }

        private void Update()
        {
            timeSinceHit += Time.deltaTime;

            if (!isUnityChan) return;

            // Maintain the sequence hit window
            sequenceTimer += Time.deltaTime;
            if (sequenceTimer >= startShield)
            {
                sequenceHits = 0;
                sequenceTimer = 0f;
            }

            if (sequenceHits >= hitThreshold && !chanScript.IsBlocking)
            {
                chanScript.BeginBlockRequest();
            }
        }

        public bool Hit(Kade.Damage damage)
        {
            if (isUnityChan && chanScript != null && chanScript.IsBlocking)
            {
                chanScript.OnBlockedHit();
                StartBlockFlash(0.15f);
                return false;
            }

            if (timeSinceHit < iTime) return false;
            if (currentHealth == 0) return false;

            if (flashMaterial != null)
            {
                StartHitFlash();
            }

            timeSinceHit = 0f;

            if (isUnityChan)
            {
                sequenceHits++;
                sequenceTimer = 0f;

                if (sequenceHits >= hitThreshold && !chanScript.IsBlocking)
                {
                    chanScript.BeginBlockRequest();
                }
            }

            currentHealth -= damage.amount;

            OnHit?.Invoke(damage);
            OnHealthChanged?.Invoke(damage.amount, currentHealth);

            if (hurtSounds != null)
                SoundEffectsManager.instance.PlayRandomClip(hurtSounds.clips, true);

            if (damageEffectPrefab != null)
                Instantiate(damageEffectPrefab, transform.position, Quaternion.identity);

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Death();
            }

            return true;
        }

        void Death()
        {
            OnDeath?.Invoke();

            if (deathSounds != null)
                SoundEffectsManager.instance.PlayRandomClip(deathSounds.clips, true);
        }

        public void ResetIFrames() => timeSinceHit = 0f;

        public void StartHitFlash()
        {
            if (timeSinceHit < iTime) return;

            foreach (var renderer in renderers)
                StartCoroutine(HandleFlashMaterialSwap(renderer));
        }

        public void StartBlockFlash(float duration = 0.15f)
        {
            foreach (var renderer in renderers)
                StartCoroutine(HandleBlockFlash(renderer, duration));
        }

        public void ResetBlockSequence()
        {
            sequenceHits = 0;
            sequenceTimer = 0f;
        }

        private IEnumerator HandleBlockFlash(Renderer renderer, float duration)
        {
            Material[] originalMats = new Material[renderer.materials.Length];
            for (int i = 0; i < originalMats.Length; i++)
                originalMats[i] = renderer.materials[i];

            Material[] newMats = new Material[renderer.materials.Length];
            for (int i = 0; i < newMats.Length; i++)

            renderer.materials = newMats;

            yield return new WaitForSeconds(duration);

            renderer.materials = originalMats;
        }

        IEnumerator HandleFlashMaterialSwap(Renderer renderer)
        {
            Material[] originalMats = new Material[renderer.materials.Length];
            for (int i = 0; i < originalMats.Length; i++)
                originalMats[i] = renderer.materials[i];

            Material[] newMats = new Material[renderer.materials.Length];
            for (int i = 0; i < newMats.Length; i++)
                newMats[i] = flashMaterial;

            renderer.materials = newMats;

            yield return new WaitForSeconds(iTime * 0.9f);

            renderer.materials = originalMats;
        }

        [ContextMenu("Test Hit")]
        public void TestHit()
        {
            Kade.Damage test = new Kade.Damage
            {
                amount = 1,
                direction = Vector3.zero,
                knockbackForce = 0
            };
            Hit(test);
        }
    }
}