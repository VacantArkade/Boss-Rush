using UnityEngine;
using System.Collections;
using UnityEngine.AI;

namespace Kade
{
    //Temp use of enum to speed testing
    public enum ChanStates
    {
        idle, pursue, melee, ranged
    }
    public class UnityChanBoss : MonoBehaviour
    {
        [Header("Chan Stats")]
        [SerializeField] int maxHealth = 100;
        private int currentHealth;
        [SerializeField] float speed;

        [Header("Chan Damage")]
        [SerializeField] int basicAtkDamage;

        [Header("Chan Controls")]
        [SerializeField] GameObject sword;
        [SerializeField] NavMeshAgent agent;
        NavMeshPath path;
        bool inMeleeRange = false;
        Transform _transform;

        Transform player;
        void Start()
        {
            player = FindAnyObjectByType<PlayerLogic>().transform;
            _transform = transform;
        }

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        void Update()
        {
            agent.SetDestination(player.transform.position);
        }
    }
}
