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
        public virtual void Update()
        {
            if (enemy.IsFrozen) return;
            elapsedTime += Time.deltaTime;
        }
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

                else if (!enemy.InMeleeRange && enemy.CanAttack && enemy.Dam.currentHealth < enemy.Dam.phaseTwoStart && Time.time - enemy.LastTeleportTime >= enemy.TeleportCooldown)
                    enemy.StateMachine.ChangeState(new TeleportRangedState(enemy));

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

            if (!enemy.InMeleeRange && enemy.CanAttack)
            {
                if (enemy.Dam.currentHealth < enemy.Dam.phaseTwoStart && Time.time - enemy.LastTeleportTime >= enemy.TeleportCooldown)
                {
                    enemy.Anim.SetBool("isMoving", false);
                    enemy.StateMachine.ChangeState(new TeleportRangedState(enemy));
                }
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

            FacePlayer();

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

    // Teleport Ranged State
    public class TeleportRangedState : EnemyState
    {
        public TeleportRangedState(EnemyTest enemy) : base(enemy) { }

        public override void Enter()
        {
            base.Enter();
            enemy.TargetVelocity = Vector3.zero;

            enemy.StartCoroutine(TeleportSequence());
        }

        private IEnumerator TeleportSequence()
        {
            enemy.SetFrozen(true);

            if (enemy.TeleportBubblePrefab != null)
                GameObject.Instantiate(enemy.TeleportBubblePrefab, enemy.Transform.position, Quaternion.identity);

            yield return new WaitForSeconds(0.5f);

            Vector3 dirToPlayer = (enemy.Player.position - enemy.Transform.position).normalized;
            dirToPlayer.y = 0;
            Vector3 teleportPos = enemy.Player.position - dirToPlayer * 1.5f;

            enemy.Transform.position = teleportPos;
            enemy.Transform.forward = dirToPlayer;

            if (enemy.TeleportBubblePrefab != null)
                GameObject.Instantiate(enemy.TeleportBubblePrefab, teleportPos, Quaternion.identity);

            enemy.Anim.SetTrigger("teleport");
            enemy.LastTeleportTime = Time.time;

            yield return new WaitForSeconds(0.5f);

            enemy.SetFrozen(false);
            enemy.StateMachine.ChangeState(new MeleeState(enemy));
        }
    }

    public class UltimateMoveState : EnemyState
    {
        public UltimateMoveState(EnemyTest enemy) : base(enemy) { }

        public override void Enter()
        {
            base.Enter();

            enemy.TargetVelocity = Vector3.zero;
            enemy.Rigidbody.linearVelocity = Vector3.zero;

            Transform handAnchor = enemy.Transform.Find("HandAnchor");
            if (handAnchor != null)
            {
                enemy.Player.SetParent(handAnchor);
                enemy.Player.localPosition = Vector3.zero;
                enemy.Player.localRotation = Quaternion.identity;
            }

            enemy.Anim.SetTrigger("grab");

            enemy.StartCoroutine(UltimateSequence());
        }

        private IEnumerator UltimateSequence()
        {
            yield return new WaitForSeconds(0.8f);

            PlayerLogic playerLogic = enemy.Player.GetComponent<PlayerLogic>();
            if (playerLogic != null)
                playerLogic.canControl = false;

            TimeStopManager.Instance.EnableTimeStopEffect(true, 1f);

            for (int i = 0; i < 3; i++)
            {
                yield return TeleportAttack();
                yield return new WaitForSeconds(1f);
            }

            enemy.Anim.SetTrigger("kick");
            yield return new WaitForSeconds(0.5f);

            TimeStopManager.Instance.EnableTimeStopEffect(false, 1f);

            if (playerLogic != null)
                playerLogic.canControl = true;

            enemy.Player.SetParent(null);

            enemy.StateMachine.ChangeState(new IdleState(enemy));
        }

        private IEnumerator TeleportAttack()
        {
            GameObject bubble = null;
            if (enemy.TeleportBubblePrefab != null)
                bubble = GameObject.Instantiate(enemy.TeleportBubblePrefab, enemy.Transform.position, Quaternion.identity);

            float growTime = 0.3f;
            float elapsed = 0f;
            Vector3 startScale = Vector3.zero;
            Vector3 maxScale = Vector3.one * 1f;

            while (elapsed < growTime)
            {
                elapsed += Time.deltaTime;
                bubble.transform.localScale = Vector3.Lerp(startScale, maxScale, elapsed / growTime);
                yield return null;
            }

            float radius = Random.Range(1.5f, 2.5f);
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;

            Vector3 teleportPos = enemy.Player.position + offset;
            enemy.Transform.position = teleportPos;
            enemy.Transform.LookAt(enemy.Player);

            if (enemy.TeleportBubblePrefab != null)
            {
                GameObject arrivalBubble = GameObject.Instantiate(enemy.TeleportBubblePrefab, teleportPos, Quaternion.identity);

                float shrinkTime = 1f;
                float elapsedShrink = 0f;
                Vector3 startArrival = arrivalBubble.transform.localScale;

                while (elapsedShrink < shrinkTime)
                {
                    elapsedShrink += Time.deltaTime;
                    arrivalBubble.transform.localScale = Vector3.Lerp(startArrival, Vector3.zero, elapsedShrink / shrinkTime);
                    yield return null;
                }
            }

            enemy.Anim.SetTrigger("swing");

            yield return new WaitForSeconds(1f);
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
        public EnemyState CurrentState => currentState;
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
        [SerializeField] GameObject teleportBubblePrefab;

        public GameObject TeleportBubblePrefab => teleportBubblePrefab;
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
        public bool IsFrozen { get; private set; } = false;
        public float LastTeleportTime { get; set; } = -999f;
        public float TeleportCooldown = 10f;

        public GameObject Shield => shield;
        public GameObject SwordHitbox => swordHitbox;
        public GameObject KickHitbox => kickHitbox;

        private float ultimateCooldown = 0f;

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

        void Update()
        {
            StateMachine.Update();

            if (ultimateCooldown > 0f)
                ultimateCooldown -= Time.deltaTime;

            if (Dam.currentHealth <= Dam.phaseThreeStart &&
                ultimateCooldown <= 0f)
            {
                if (!(StateMachine.CurrentState is UltimateMoveState))
                {
                    StateMachine.ChangeState(new UltimateMoveState(this));
                    ultimateCooldown = 60f;
                }
            }
        }

        void FixedUpdate()
        {
            if (IsFrozen)
            {
                Rigidbody.linearVelocity = Vector3.zero;
                return;
            }
            Rigidbody.linearVelocity = TargetVelocity;
            isMoving = TargetVelocity.magnitude > 0.1f;
            Anim.SetBool("isMoving", isMoving);
        }

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
            if (IsBlocking) return;
            StateMachine.ChangeState(new BlockState(this));
            IsBlocking = true;
        }

        public void EndBlockRequest()
        {
            if (!IsBlocking) return;
            StateMachine.ChangeState(new IdleState(this));
            IsBlocking = false;
        }

        public void OnBlockedHit()
        {
            kickHitbox.SetActive(true);
            Anim.SetTrigger("blocked");
        }

        internal void NotifyBlockEnded()
        {
            IsBlocking = false;
        }

        public void SetFrozen(bool frozen)
        {
            IsFrozen = frozen;
            if (frozen)
            {
                TargetVelocity = Vector3.zero;
                Rigidbody.linearVelocity = Vector3.zero;
                Navigator.enabled = false;
                Anim.SetBool("isMoving", false);
            }
            else
            {
                Navigator.enabled = true;
            }
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
