using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Kade
{
    public enum EnemyStates
    {
        idle, pursue, melee, ranged, dead
    }

    public class EnemyTest : MonoBehaviour
    {
        [SerializeField] GameObject meleeWeapon;
        [SerializeField] GameObject shield;
        [SerializeField] GameObject swordHitbox;
        [SerializeField] GameObject kickHitbox;

        [SerializeField] float speed;

        Navigator navigator;
        Transform _transform;
        Transform player;
        Rigidbody _rigidbody;
        Animator anim;

        EnemyStates state = EnemyStates.idle;
        float currentStateElapsed = 0;
        Vector3 currentTargetNodePosition;
        int pathNodeIndex = 0;
        Vector3 targetVelocity;
        bool inMeleeRange = false;
        bool canAttack = true;

        void Start()
        {
            navigator = GetComponent<Navigator>();
            player = FindObjectOfType<PlayerLogic>().transform;
            _rigidbody = GetComponent<Rigidbody>();
            _transform = transform;
            anim = GetComponent<Animator>();
            swordHitbox.SetActive(false);
            shield.SetActive(false);
            kickHitbox.SetActive(false);
        }

        void Update()
        {
            currentStateElapsed += Time.deltaTime;

            switch (state)
            {
                case EnemyStates.idle:
                    UpdateIdle();
                    break;
                case EnemyStates.melee:
                    UpdateMelee();
                    break;
                case EnemyStates.pursue:
                    UpdatePursue();
                    break;
                case EnemyStates.ranged:
                    break;
                case EnemyStates.dead:
                    UpdateDead();
                    break;
            }
        }

        private void FixedUpdate()
        {
            _rigidbody.linearVelocity = targetVelocity;
        }

        void UpdateIdle()
        {
            if (currentStateElapsed > 2.0f)
            {
                if (inMeleeRange)
                    EnterMelee();
                else
                    AttemptBeginPursue();
            }
        }

        bool AttemptBeginPursue()
        {
            if (AttemptMakePathToPlayer())
            {
                pathNodeIndex = 0;
                state = EnemyStates.pursue;
                currentStateElapsed = 0;

                return true;
            }

            Debug.Log("failed attempt to pursue");

            return false;
        }

        void UpdatePursue()
        {
            currentTargetNodePosition = navigator.PathNodes[pathNodeIndex];

            Vector3 dirToNode = (currentTargetNodePosition - _transform.position);
            dirToNode.y = 0;
            dirToNode.Normalize();

            _transform.forward = dirToNode;

            float distToNode = Vector3.Distance(currentTargetNodePosition, _transform.position);

            if (distToNode < 3f)
            {
                pathNodeIndex++;

                if (pathNodeIndex >= navigator.PathNodes.Count)
                {
                    pathNodeIndex = 0;
                    AttemptMakePathToPlayer();
                    return;
                }

            }

            if (inMeleeRange)
            {
                // do melee attack
                if (canAttack)
                    EnterMelee();
                return;
            }

            targetVelocity = _transform.forward * speed;
            targetVelocity.y = _rigidbody.linearVelocity.y;

            if (currentStateElapsed > 1) // rebuild path every half second
            {
                pathNodeIndex = 1;
                AttemptMakePathToPlayer();
            }
        }

        void EnterMelee()
        {
            var dirToPlayer = (player.transform.position - transform.position).normalized;
            dirToPlayer.y = 0;
            transform.forward = dirToPlayer;
            targetVelocity = Vector3.zero;
            state = EnemyStates.melee;
            currentStateElapsed = 0;

            StartCoroutine(HandleMelee());
        }

        IEnumerator HandleMelee()
        {
            swordHitbox.SetActive(true);
            anim.SetTrigger("swing");
            yield return new WaitForSeconds(1f);
            swordHitbox.SetActive(false);
        }

        void UpdateMelee()
        {
            if (currentStateElapsed >= 2.0f)
            {
                state = EnemyStates.idle;
            }
        }

        public void Death()
        {
            navigator.enabled = false;
            targetVelocity = Vector3.zero;
            GameManager.instance.GoToNextLevel();
            state = EnemyStates.dead;
        }

        void UpdateDead()
        {
            //Debug.Log("in dead");
        }

        bool AttemptMakePathToPlayer()
        {
            return (navigator.CalculatePathToPosition(player.position));
        }

        float DistanceToPlayer()
        {
            return Vector3.Distance(_transform.position, player.position);
        }

        public void SetInMeleeRange(bool inMeleeRange)
        {
            this.inMeleeRange = inMeleeRange;
        }

        public void StartBlocking()
        {
            canAttack = false;
            shield.SetActive(true);
            anim.SetTrigger("startBlock");
        }

        public void BlockedAttack()
        {
            kickHitbox.SetActive(true);
            anim.SetTrigger("blocked");
        }

        public void StopBlocking()
        {
            canAttack = true;
            shield.SetActive(false);
            kickHitbox.SetActive(false);
            anim.SetTrigger("endBlock");
        }
    }
}