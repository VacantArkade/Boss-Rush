using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Damager : MonoBehaviour
{
    [SerializeField] int damageAmount;
    [SerializeField] float knockbackForce = 1;
    [SerializeField] GameObject hitEffectPrefab;
    [SerializeField] AudioClipCollection hitSounds;

    public UnityEvent OnContact;
    public UnityEvent OnSuccessfulHit;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void SetDamageAmount(int amount)
    {
        damageAmount = amount;
    }

    private void OnTriggerEnter(Collider other)
    {
        OnContact?.Invoke();

        if (other.GetComponent<Damageable>())
        {
            Vector3 dir = other.transform.position - transform.position;
            dir.Normalize();

            Damage damage = new Damage();
            damage.amount = damageAmount;
            damage.direction = dir;
            damage.knockbackForce = knockbackForce;

            if (other.GetComponent<Damageable>().Hit(damage))
            {
                OnSuccessfulHit?.Invoke();

                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, other.transform.position, Quaternion.identity);
                }

                if(hitSounds != null)
                    SoundEffectsManager.instance.PlayRandomClip(hitSounds.clips, true);
            }
        }
    }
}
public class Damage
{
    public int amount = 0;
    public Vector3 direction = Vector3.zero;
    public float knockbackForce = 1;
}
