using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Kade
{
    // Base State
    public abstract class EnemyState
    {
        protected EnemyTest enemy;
        protected float elapsedTime;

        public EnemyState(EnemyTest enemy) { this.enemy = enemy; }

        public virtual void Enter() { elapsedTime = 0f; }
        public virtual void Update() { elapsedTime += Time.deltaTime; }
        public virtual void Exit() { }
    }

    // Idle State
    public class IdleState : EnemyState
    {
        public IdleState(EnemyTest enemy) : base(enemy) { }

        public override void Update()
        {
            base.Update();
            //enemy.Anim.SetTrigger("switchIdle");
            if (elapsedTime > 2.0f)
            {
                if (enemy.InMeleeRange && enemy.CanAttack)
                    enemy.StateMachine.ChangeState(new MeleeState(enemy));
                else if (!enemy.InMeleeRange)
                    enemy.StateMachine.ChangeState(new PursueState(enemy));
            }
        }
    }

    // Pursue State
    public class PursueState : EnemyState
    {
        public PursueState(EnemyTest enemy) : base(enemy) { }

        public override void Enter()
        {
            base.Enter();
            enemy.PathNodeIndex = 0;
            enemy.AttemptMakePathToPlayer();
        }

        public override void Update()
        {
            base.Update();
            var targetNode = enemy.Navigator.PathNodes[enemy.PathNodeIndex];
            Vector3 dirToNode = (targetNode - enemy.Transform.position);
            dirToNode.y = 0;
            dirToNode.Normalize();

            enemy.Anim.SetBool("isMoving", true);
            enemy.Transform.forward = dirToNode;
            float distToNode = Vector3.Distance(targetNode, enemy.Transform.position);

            if (distToNode < 3f)
            {
                enemy.PathNodeIndex++;
                if (enemy.PathNodeIndex >= enemy.Navigator.PathNodes.Count)
                {
                    enemy.PathNodeIndex = 0;
                    enemy.AttemptMakePathToPlayer();
                    return;
                }
            }

            if (enemy.InMeleeRange && enemy.CanAttack)
            {
                enemy.Anim.SetBool("isMoving", false);
                enemy.StateMachine.ChangeState(new MeleeState(enemy));
                return;
            }

            enemy.TargetVelocity = enemy.Transform.forward * enemy.Speed;

            var v = enemy.TargetVelocity;
            v.y = enemy.Rigidbody.linearVelocity.y;
            enemy.TargetVelocity = v;

            if (elapsedTime > 1f)
            {
                enemy.PathNodeIndex = 1;
                enemy.AttemptMakePathToPlayer();
                elapsedTime = 0f;
            }
        }
    }

    // Melee State
    public class MeleeState : EnemyState
    {
        public MeleeState(EnemyTest enemy) : base(enemy) { }

        public override void Enter()
        {
            base.Enter();
            var dirToPlayer = (enemy.Player.position - enemy.Transform.position).normalized;
            dirToPlayer.y = 0;
            enemy.Transform.forward = dirToPlayer;
            enemy.TargetVelocity = Vector3.zero;
            enemy.StartCoroutine(enemy.HandleMelee());
        }

        public override void Update()
        {
            base.Update();
            if (elapsedTime >= 2.0f)
                enemy.StateMachine.ChangeState(new IdleState(enemy));
        }
    }

    // Block State
    public class BlockState : EnemyState
    {
        private readonly float blockDuration = 4f;
        public BlockState(EnemyTest enemy) : base(enemy) { }

        public override void Enter()
        {
            base.Enter();
            enemy.CanAttack = false;
            enemy.Shield.SetActive(true);
            //enemy.Anim.SetTrigger("startBlock");
            enemy.Anim.SetBool("isBlocking", true);
            enemy.TargetVelocity = Vector3.zero;
            FacePlayer();
        }

        public override void Update()
        {
            base.Update();

            // Keep facing the player while blocking
            FacePlayer();

            // Exit after duration
            if (elapsedTime >= blockDuration)
            {
                enemy.Anim.SetBool("isBlocking", false);
                enemy.StateMachine.ChangeState(new IdleState(enemy));
            }
        }

        public override void Exit()
        {
            enemy.CanAttack = true;
            enemy.Shield.SetActive(false);
            enemy.KickHitbox.SetActive(false);
            enemy.Anim.SetBool("isBlocking", false);
            //enemy.Anim.SetTrigger("endBlock");
            enemy.NotifyBlockEnded();
        }

        private void FacePlayer()
        {
            var dir = (enemy.Player.position - enemy.Transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                enemy.Transform.forward = dir;
            }
        }
    }

    // Dead State
    public class DeadState : EnemyState
    {
        public DeadState(EnemyTest enemy) : base(enemy) { }

        public override void Enter()
        {
            base.Enter();
            enemy.Navigator.enabled = false;
            enemy.TargetVelocity = Vector3.zero;
            GameManager.instance.GoToNextLevel();
        }
    }

    // State Machine
    public class StateMachine
    {
        private EnemyState currentState;
        public void ChangeState(EnemyState newState)
        {
            currentState?.Exit();
            currentState = newState;
            currentState.Enter();
        }

        public void Update() => currentState?.Update();
    }

    // EnemyTest
    public class EnemyTest : MonoBehaviour
    {
        [SerializeField] GameObject meleeWeapon;
        [SerializeField] GameObject shield;
        [SerializeField] GameObject swordHitbox;
        [SerializeField] GameObject kickHitbox;
        [SerializeField] float speed;

        public Damageable Dam { get; private set; }
        public Navigator Navigator { get; private set; }
        public Transform Transform { get; private set; }
        public Transform Player { get; private set; }
        public Rigidbody Rigidbody { get; private set; }
        public Animator Anim { get; private set; }

        public StateMachine StateMachine { get; private set; }
        public Vector3 TargetVelocity { get; set; }
        public int PathNodeIndex { get; set; }
        public float Speed => speed;
        public bool isMoving { get; private set; }
        public bool InMeleeRange { get; private set; }
        public bool CanAttack { get; set; } = true;
        public bool IsBlocking { get; private set; }

        public GameObject Shield => shield;
        public GameObject SwordHitbox => swordHitbox;
        public GameObject KickHitbox => kickHitbox;

        void Start()
        {
            Navigator = GetComponent<Navigator>();
            Player = FindObjectOfType<PlayerLogic>().transform;
            Rigidbody = GetComponent<Rigidbody>();
            Transform = transform;
            Anim = GetComponent<Animator>();
            Dam = GetComponent<Damageable>();

            swordHitbox.SetActive(false);
            shield.SetActive(false);
            kickHitbox.SetActive(false);

            StateMachine = new StateMachine();
            StateMachine.ChangeState(new IdleState(this));
        }

        void Update() => StateMachine.Update();

        void FixedUpdate() => Rigidbody.linearVelocity = TargetVelocity;

        public IEnumerator HandleMelee()
        {
            swordHitbox.SetActive(true);
            if (Dam.currentHealth < Dam.phaseTwoStart)
            {
                Anim.SetTrigger("tripleSwing");
                yield return new WaitForSeconds(3.5f);
            }
            else
            {
                Anim.SetTrigger("swing");
                yield return new WaitForSeconds(0.5f);
            }
            swordHitbox.SetActive(false);
        }

        public void BeginBlockRequest()
        {
            // If already blocking, do nothing
            if (IsBlocking) return;
            StateMachine.ChangeState(new BlockState(this));
            IsBlocking = true;
        }

        public void EndBlockRequest()
        {
            // If not blocking, ignore
            if (!IsBlocking) return;
            StateMachine.ChangeState(new IdleState(this));
            IsBlocking = false;
        }

        public void OnBlockedHit()
        {
            // Play block reaction
            kickHitbox.SetActive(true);
            Anim.SetTrigger("blocked");
        }

        internal void NotifyBlockEnded()
        {
            IsBlocking = false;
        }



        public void Death() => StateMachine.ChangeState(new DeadState(this));

        public bool AttemptMakePathToPlayer() => Navigator.CalculatePathToPosition(Player.position);

        public void SetInMeleeRange(bool inMeleeRange) => InMeleeRange = inMeleeRange;

        public void StartBlocking() => StateMachine.ChangeState(new BlockState(this));

        public void BlockedAttack()
        {
            kickHitbox.SetActive(true);
            Anim.SetTrigger("blocked");
            Anim.SetBool("isBlocking", false);
        }
    }
}