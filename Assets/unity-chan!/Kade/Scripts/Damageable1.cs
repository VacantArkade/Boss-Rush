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

        [SerializeField] float phaseTwoStart;
        [SerializeField] float phaseThreeStart;

        public UnityEvent<int> OnInitialize;
        public UnityEvent<Damage> OnHit;
        public UnityEvent OnDeath;
        public UnityEvent<int, int> OnHealthChanged;

        int currentHealth;
        float timeSinceHit = 0;

        float blockTimer = 0; //How long block has happened
        int sequenceHits = 0; //How many hits in a row
        float startShield = 3; //Timer for sequenceHits to build
        int hitThreshold = 3; //How many hits till start blocking
        float blockStop = 4; //How long to block

        bool blocking = false;

        // Start is called before the first frame update
        void Start()
        {
            currentHealth = maxHealth;

            OnInitialize?.Invoke(maxHealth);
            OnHealthChanged?.Invoke(maxHealth, maxHealth);

            player = FindObjectOfType<PlayerLogic>().transform;
        }

        private void Update()
        {
            timeSinceHit += Time.deltaTime;

            if (blocking)
            {
                var dirToPlayer = (player.transform.position - transform.position).normalized;
                dirToPlayer.y = 0;
                transform.forward = dirToPlayer;

                blockTimer += Time.deltaTime;
                if (blockTimer >= blockStop)
                {
                    blocking = false;
                    blockTimer = 0;
                    sequenceHits = 0;
                    chanScript.StopBlocking();
                }
            }

            if (blockTimer >= startShield)
            {
                sequenceHits = 0;
                blockTimer = 0;
            }
        }

        public bool Hit(Damage damage)
        {
            if (blocking)
            {
                chanScript.BlockedAttack();
                return false;
            }

            if (timeSinceHit < iTime)
                return false;

            if (currentHealth == 0)
                return false;

            if (flashMaterial != null)
            {
                StartHitFlash();
            }

            timeSinceHit = 0;

            sequenceHits++;

            currentHealth -= damage.amount;

            OnHit?.Invoke(damage); // handle any additional hit functions

            OnHealthChanged?.Invoke(damage.amount, currentHealth);

            if (hurtSounds != null)
                SoundEffectsManager.instance.PlayRandomClip(hurtSounds.clips, true);

            if (damageEffectPrefab != null)
            {
                Instantiate(damageEffectPrefab, transform.position, Quaternion.identity);
            }

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Death();
            }

            if (sequenceHits >= hitThreshold)
            {
                chanScript.StartBlocking();
                blocking = true;
            }

            return true;
        }

        void Death()
        {
            OnDeath?.Invoke();

            if (deathSounds != null)
                SoundEffectsManager.instance.PlayRandomClip(deathSounds.clips, true);
        }

        public void ResetIFrames()
        {
            timeSinceHit = 0;
        }

        public void StartHitFlash()
        {
            if (timeSinceHit < iTime)
                return;

            foreach (var renderer in renderers)
            {
                StartCoroutine(HandleFlashMaterialSwap(renderer));
            }
        }

        IEnumerator HandleFlashMaterialSwap(Renderer renderer)
        {
            Material[] originalMats = new Material[renderer.materials.Length];

            for (int i = 0; i < originalMats.Length; i++)
            {
                originalMats[i] = renderer.materials[i];
            }

            Material[] newMats = new Material[renderer.materials.Length];

            for (int i = 0; i < newMats.Length; i++)
            {
                newMats[i] = flashMaterial;
            }

            renderer.materials = newMats;

            yield return new WaitForSeconds(iTime * 0.9f);

            renderer.materials = originalMats;
        }

        [ContextMenu("Test Hit")]
        public void TestHit()
        {
            Damage test = new Damage();
            test.amount = 1;
            test.direction = Vector3.zero;
            test.knockbackForce = 0;
            Hit(test);
        }
    }
}