using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;
using UnityEngine.SceneManagement;
public class CharacterMovement : MonoBehaviour
{
    public float acceleration = 10f;    // 加速度
    public float maxSpeed = 12f;         // 最大速度
    public float deceleration = 8f;     // 松开按键时的减速度
    public float rotationSpeed = 420f;   // 转向速度（用于让角色朝向移动方向）

    public CinemachineRotateWithFollowTarget cameraRotationControllor;

    public Transform camera;


    private MeshRenderer mesh;

    private Rigidbody rb;
    private Vector3 moveVelocity;
    private Vector3 camForward, camRight, camLeft, camBackward;
    private KeyCode counterDirectionKey;

    public GameObject crounchObject;
    public GameObject headIndicator;
    public enum SpeedState
    {
        Static,
        Walk,
        Run
    }

    public enum GestureState
    {
        Stand,
        Counch,
        Jump,
        Rolling
    }

    public SpeedState characterMoveState;
    public GestureState characterGestureState;

    public float playerHeight;
    public LayerMask groundLayer; // LayerMask for ground detection

    [Header("JumpSettings")]
    public bool isGround;
    public bool isJumping = false;
    public bool hasJumped = false;
    public float jumpForce = 5f;
    public float gravity = 10f;
    public float maxJumpTime = .4f;
    public float holdJumpTimer = 0f;

    [Header("SecondJumpSettings")]
    public float secondJumpForce = 5f;
    public float secondMaxJumpTime = .4f;
    public float seondHoldJumpTimer = 0f;

    [Header("ThirdJumpSettings")]
    public float thirdJumpForce = 5f;
    public float thirdMaxJumpTime = .4f;
    public float thirdHoldJumpTimer = 0f;
    public float thirdJumpWindow = 1.0f;
    public bool thirdWindowActive = false;
    private float thirdWindowTimer = 0f;

    [Header("BackJumpSettings")]
    public float backJumpForce = 12f;
    public float backJumpHorizontalForce = 5f;
    public float backForceDuration = 0.25f;
    public AnimationCurve horizontalForceCurve;   // 力曲线（0~1之间）
    public bool isBackJumping = false;

    [Header("LongJumpSettings")]
    public float longJumpForce = 7f;
    public float longJumpHorizontalForce = 300f;
    public float longJumpForceDuration = 0.25f;
    public AnimationCurve longJumpHorizontalForceCurve;   // 力曲线（0~1之间）
    public bool isLongJumping = false;
    public float longJumpTimer = 0f;
    public float longJumpWindow = .17f;
    public bool longJumpWindowActivate = false;
    private bool longJumpWindowActivateTwo = false;
    public bool isLongBoosting = false; // 新增：推进阶段


    public int currentJumpCount = 0;


    public float secondJumpWindow = 1.0f;   
    public bool secondWindowActive = false;
    private float secondWindowTimer = 0f;

    private float ignoreGroundTimer = 0;

    [Header("Air Dash (J)")]
    public float airDashForward = 12f;     // 向前突进强度
    public float airDashUp = 2f;           // 轻微抬升（需要更平就调小或设0）
    public float airDashMaxPlanarSpeed = 20f; // 可选：突进后水平速度上限
    private bool airDashUsed = false;      // 一次空中只能用一次  // 

    [Header("RollingSettings")]
    public GameObject rollingObject;
    public bool isRolling = false;
    public float maxRollingSpeed = 30f;
    public Collider rollingCollider;
    public CapsuleCollider standCollider;

    [Header("Rolling Landing Boost")]
    public float rollingLandingBoostMultiplier = 0.6f;   // 下落速度转化为前冲的倍率
    public float rollingLandingBoostMinFall = 4f;        // 触发前冲的最小下落速度阈值（绝对值）
    public float rollingLandingBoostMax = 20f;           // 前冲速度上限（可根据手感调大/调小）
    public bool allowOverMaxRollingSpeed = false;        // 是否允许瞬间超过 maxRollingSpeed（若要严格限制可设 false）

    // 运行时临时变量
    private bool wasGrounded = false;                    // 记录上一帧是否在地面
    private float lastVerticalSpeed = 0f;


    public float fallingVelocity;
    public float speed;

    [Header("Rolling Release Hop")]
    public float rollingReleaseHopUp = 3f;        // 松开K时的竖直抬升
    public float rollingReleaseHopForward = 6f;   // 松开K时的前向爆发
    public float rollingReleaseHopCooldown = 0.2f;// 防抖：连续松开K的冷却
    private float rollingReleaseHopCDTimer = 2f;
    private bool hasHoped = false;

    // 用于检测“从Rolling -> 非Rolling”的帧级过渡
    private bool wasRollingPrevFrame = false;

    void Start()
    {
        mesh = GetComponent<MeshRenderer>();
        characterGestureState = GestureState.Stand;
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // 防止物理旋转


    }

    void Update()
    {
        //SetCameraRotationWhileIdle();
        GroundCheck();
        ReadCameraBasis();
        InputDetection();
        StateDetection();
        JumpDetection();
        LongJumpWindowTimer();
        StandChange();
        RollingChange();

        fallingVelocity = rb.linearVelocity.y;
        var A = rb.linearVelocity;
        A.y = 0;
        speed = A.magnitude;

        if (rollingReleaseHopCDTimer > 0f)
            rollingReleaseHopCDTimer -= Time.deltaTime;

        // 记录上一帧是否是Rolling（用于边沿检测/安全校验）
        wasRollingPrevFrame = (characterGestureState == GestureState.Rolling);

        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(0);
        }
    }

    private void TryRollingReleaseHop()
    {
        // 冷却中 or 刚才并不是Rolling：不触发
        if (rollingReleaseHopCDTimer > 0f || !wasRollingPrevFrame)
            return;

        if (hasHoped)
            return;
        // 仅在地面上触发（若你希望空中也能触发，可以去掉 isGround 判断）
        //if (!isGround)
        //    return;

        // 不计入正常跳跃：不改 isJumping/hasJumped/currentJumpCount

        // 提示：避免先前下压速度影响小跳（可按需保留/删除）
        var v = rb.linearVelocity;
        v.y = Mathf.Max(0f, v.y);   // 不把向上速度清零，只去掉向下分量
        rb.linearVelocity = v;

        // 前向（水平）+ 向上 的小跳：使用 VelocityChange 不受质量影响、且非常“脆”
        Vector3 forwardPlanar = transform.forward; forwardPlanar.y = 0f;
        if (forwardPlanar.sqrMagnitude > 0f) forwardPlanar.Normalize();

        Vector3 impulse = forwardPlanar * rollingReleaseHopForward + Vector3.up * rollingReleaseHopUp;
        rb.AddForce(impulse, ForceMode.VelocityChange);

        // 开冷却，避免连续触发
        rollingReleaseHopCDTimer = rollingReleaseHopCooldown;
        hasHoped = true;
    }
    void ReadCameraBasis()
    {
            camForward = camera.forward;
            camRight = camera.right;
            camLeft = -camera.right;
            camBackward = -camera.forward; ;
 
    }


    private void InputDetection()
    {

        StopCharacterWhileHoldingOppositeDirections();
        CrounchDetection();
        RotateCharacterWhileIdle();
        AirDashDetection(); 
        RollingDetection();
    }

    private void AirDashDetection() // 
    {
        // 只有在空中且本次空中还没用过才能触发
        if (!isGround && !airDashUsed && Input.GetKeyDown(KeyCode.J))
        {
            // 如果你不希望某些特殊状态能用（比如长跳/后撤/滚动），可以在这里挡掉：
            if (isLongJumping || isBackJumping)
                return;

            // 取水平前向（优先玩家朝向；若为零可用相机前向回退）
            Vector3 forwardPlanar = transform.forward;
            forwardPlanar.y = 0f;
            if (forwardPlanar.sqrMagnitude < 0.0001f)
            {
                forwardPlanar = camForward;
                forwardPlanar.y = 0f;
            }
            if (forwardPlanar.sqrMagnitude > 0f) forwardPlanar.Normalize();

           

            // 突进：水平 + 竖直 的“速度变化”，不计入正常跳跃计数
            Vector3 impulse = forwardPlanar * airDashForward + Vector3.up * airDashUp;
            rb.AddForce(impulse, ForceMode.VelocityChange);

            // 可选：突进后做一次水平速度上限裁剪，避免过大
            Vector3 planar = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (planar.magnitude > airDashMaxPlanarSpeed)
            {
                Vector3 capped = planar.normalized * airDashMaxPlanarSpeed;
                rb.linearVelocity = new Vector3(capped.x, rb.linearVelocity.y, capped.z);
            }

            // 标记本次空中已使用
            airDashUsed = true;
        }
    }
    void StateDetection()
    {
        if (rb.linearVelocity.magnitude > 0f && rb.linearVelocity.magnitude <= 5f)
        {
            characterMoveState = SpeedState.Walk;
        }
        else if (rb.linearVelocity.magnitude <= 0f)
        {
            characterMoveState = SpeedState.Static;

        }
        else if (rb.linearVelocity.magnitude >= 12f)
        {
            characterMoveState = SpeedState.Run;

        }
    }

    void RollingDetection()
    {
        // 进入 Rolling（你原有的条件）
        if (Input.GetKey(KeyCode.K) && fallingVelocity <= -12f && characterGestureState != GestureState.Rolling)
        {
            characterGestureState = GestureState.Rolling;
        }
        // 松开K：先试图触发小跳，再改姿态
        else if (Input.GetKeyUp(KeyCode.K))
        {
            // 只有从Rolling松开K才触发（Try函数内部也有 wasRollingPrevFrame 保护）
            TryRollingReleaseHop();

            // 然后退出 Rolling
            characterGestureState = GestureState.Stand;
        }
    }

    void RollingChange()
    {
        if (characterGestureState == GestureState.Rolling)
        {
            mesh.enabled = false;
            headIndicator.SetActive(false);
            rollingObject.SetActive(true);
            standCollider.isTrigger = true;
            rollingCollider.isTrigger = false;
        }
    }
    void StandChange()
    {
        if (characterGestureState == GestureState.Stand)
        {
            mesh.enabled = true;
            headIndicator.SetActive(true);
            crounchObject.SetActive(false);
            rollingObject.SetActive(false);
            standCollider.isTrigger = false;
            rollingCollider.isTrigger = true;
        }
    }

    void JumpDetection()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGround)
        {
            isJumping = true;
            holdJumpTimer = 0f;
            hasJumped = true;

            // 清竖直速度
            Vector3 v = rb.linearVelocity; v.y = 0f; rb.linearVelocity = v;

            Vector3 jumpVector = Vector3.zero;

            if (characterGestureState != GestureState.Counch)
            {
                if (thirdWindowActive)
                {
                    jumpVector = Vector3.up * thirdJumpForce;
                    currentJumpCount = 0;           // 触发 third 后把计数清零（按你原方案）
                    thirdWindowActive = false;      // 消耗第三段窗口
                    secondWindowActive = false;     // 保险：也关掉第二段窗口
                }
                else if (secondWindowActive)
                {
                    jumpVector = Vector3.up * secondJumpForce;
                    currentJumpCount += 1;          // 从 1 → 2
                    secondWindowActive = false;     // 消耗第二段窗口
                }
                else
                {
                    jumpVector = Vector3.up * jumpForce;
                    currentJumpCount = 1;
                }

                if (characterGestureState != GestureState.Rolling)
                {
                    rb.AddForce(jumpVector, ForceMode.Impulse);

                }
                else if (characterGestureState == GestureState.Rolling)
                {
                    rb.AddForce(jumpVector/2, ForceMode.Impulse);

                }

            }
            
            if (longJumpWindowActivate)
            {
                StartCoroutine(DoLongstep());
            }
            else if (characterGestureState == GestureState.Counch && !longJumpWindowActivate)
            {
                StartCoroutine(DoBackstep());

            }

        }



        if (isJumping && Input.GetKey(KeyCode.Space))
        {
            holdJumpTimer += Time.deltaTime;
        }
        if (isJumping && (Input.GetKeyUp(KeyCode.Space) || holdJumpTimer >= maxJumpTime))
        {
            isJumping = false;
        }

        
    }

    IEnumerator DoBackstep()
    {
        isBackJumping = true;

        Vector3 v = rb.linearVelocity;
        v.y = 0;
        rb.linearVelocity = v;

       
        Vector3 jumpVector = Vector3.up * backJumpForce;
        rb.AddForce(jumpVector, ForceMode.Impulse);

       
        float timer = 0f;
        while (timer < backForceDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / backForceDuration);

            // 力曲线（默认线性，如果没设置则直接线性增长）
            float forceFactor = horizontalForceCurve != null ? horizontalForceCurve.Evaluate(t) : t;

            Vector3 backDir = -transform.forward;
            Vector3 horizontalForce = backDir * backJumpHorizontalForce * forceFactor;

            rb.AddForce(horizontalForce * Time.deltaTime, ForceMode.VelocityChange);

            yield return null;
        }

        isBackJumping = false;

    }

    IEnumerator DoLongstep()
    {
        isLongJumping = true;

        Vector3 v = rb.linearVelocity;
        v.x = v.x/3*2;
        v.z = v.z/3*2;
        rb.linearVelocity = v;


        Vector3 jumpVector = Vector3.up * longJumpForce;
        if (characterGestureState != GestureState.Rolling)
        {
            rb.AddForce(jumpVector/2, ForceMode.Impulse);

        }
        else if (characterGestureState == GestureState.Rolling)
        {
            rb.AddForce(jumpVector*2.4F, ForceMode.Impulse);

        }


        float timer = 0f;
        while (timer < longJumpForceDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / longJumpForceDuration);

            // 力曲线（默认线性，如果没设置则直接线性增长）
            float forceFactor = longJumpHorizontalForceCurve  != null ? longJumpHorizontalForceCurve.Evaluate(t) : t;

            Vector3 backDir = transform.forward;
            Vector3 horizontalForce = backDir * longJumpHorizontalForce * forceFactor;

            rb.AddForce(horizontalForce * Time.deltaTime, ForceMode.VelocityChange);

            yield return null;
        }

        isLongJumping = false;

    }
    void GroundCheck()
    {
        // 地面检测
        bool nowGround = Physics.Raycast(
            transform.position, Vector3.down,
            playerHeight * 0.5f + 0.05f, groundLayer
        );
        isGround = nowGround;

        // 记录空中时的竖直速度（用于落地瞬间计算）
        if (!isGround)
        {
            // 记录最近一帧的竖直速度（通常为负数，表示下落）
            lastVerticalSpeed = rb.linearVelocity.y;
        }

        if (hasJumped) ignoreGroundTimer += Time.deltaTime;

        // —— 你的原落地处理（保持不变）——
        if (isGround && ignoreGroundTimer >= 0.08f)
        {
            isJumping = false;
            ignoreGroundTimer = 0f;
            hasJumped = false;
            hasHoped = false;
            airDashUsed = false;

            if (currentJumpCount == 1)
            {
                secondWindowActive = true;
                secondWindowTimer = secondJumpWindow;
                thirdWindowActive = false;
            }
            else if (currentJumpCount == 2 && characterMoveState == SpeedState.Run)
            {
                thirdWindowActive = true;
                thirdWindowTimer = thirdJumpWindow;
                secondWindowActive = false;
            }
            else
            {
                // 其它情况……
            }
        }

        // 窗口倒计时（保持不变）
        if (secondWindowActive)
        {
            secondWindowTimer -= Time.deltaTime;
            if (secondWindowTimer <= 0f)
            {
                secondWindowActive = false;
                if (!thirdWindowActive)
                    currentJumpCount = 0;
            }
        }

        if (thirdWindowActive)
        {
            thirdWindowTimer -= Time.deltaTime;
            if (thirdWindowTimer <= 0f)
            {
                thirdWindowActive = false;
                currentJumpCount = 0;
            }
        }

        // 关键：检测“从空中 → 落地”的边沿，并在 Rolling 时给前冲 ★★★
        if (!wasGrounded && isGround) // 空中上一帧、这一帧落地
        {
            if (characterGestureState == GestureState.Rolling)
            {
                // 以落地前的下落速度大小来决定前冲强度
                float fallSpeed = Mathf.Abs(lastVerticalSpeed); // 取绝对值
                if (fallSpeed >= rollingLandingBoostMinFall)
                {
                    float boost = fallSpeed * rollingLandingBoostMultiplier;
                    boost = Mathf.Min(boost, rollingLandingBoostMax);

                    // 给一个沿角色 forward 的爆发速度变化（VelocityChange 不受质量影响）
                    Vector3 forwardPlanar = transform.forward; forwardPlanar.y = 0f;
                    if (forwardPlanar.sqrMagnitude > 0f) forwardPlanar.Normalize();

                    // 如果不允许超过 maxRollingSpeed，就在施加前做一次预测并裁剪
                    if (!allowOverMaxRollingSpeed)
                    {
                        Vector3 v = rb.linearVelocity;
                        Vector3 planar = new Vector3(v.x, 0f, v.z);
                        Vector3 candidate = planar + forwardPlanar * boost; // 施加后的平面速度
                        float maxPlanar = (characterGestureState == GestureState.Rolling) ? maxRollingSpeed : maxSpeed;
                        if (candidate.magnitude > maxPlanar)
                        {
                            candidate = candidate.normalized * maxPlanar;
                            // 用“差值”作为真正可施加的 boost
                            boost = Mathf.Max(0f, (candidate - planar).magnitude);
                        }
                    }

                    rb.AddForce(forwardPlanar * boost, ForceMode.VelocityChange);
                }
            }
        }

        // 更新上一帧是否在地面
        wasGrounded = isGround;
    }

   
    void CrounchDetection()
    {
        // 只在按下瞬间开窗；不立即进入 crounch
        if (Input.GetKeyDown(KeyCode.LeftShift) && isGround)
        {
            longJumpTimer = longJumpWindow;
            longJumpWindowActivate = true;   // 显式置位
                                             // 不改 characterGestureState（保持原姿态）
        }
        // 松开时关窗并回到 Stand
        else if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            longJumpTimer = 0f;
            longJumpWindowActivate = false;
            characterGestureState = GestureState.Stand;
        }

        // 保留你的外观切换，仅当真的处于 crounch 时才生效
        CrounchChange();
    }

    void LongJumpWindowTimer()
    {
        if (longJumpTimer > 0f)
            longJumpTimer -= Time.deltaTime;

        bool wasActive = longJumpWindowActivate;
        bool nowActive = longJumpTimer > 0f;

        // 更新当前状态
        longJumpWindowActivate = nowActive;

        // —— 关键逻辑：窗口从“有”变“无”的瞬间，切换到 crounch ——
        if (wasActive && !nowActive)
        {
            // 仍按着 Shift 且在地面，才进入蹲姿
            if (Input.GetKey(KeyCode.LeftShift) && isGround)
            {
                characterGestureState = GestureState.Counch;
            }
            else
            {
                // 否则（没按或不在地面）按你的设计，回 Stand（也可不处理）
                //characterGestureState = GestureState.Stand;
            }
        }
    }

    void CrounchChange()
    {
        if (characterGestureState == GestureState.Counch)
        {
            mesh.enabled = false;
            headIndicator.SetActive(false);
            crounchObject.SetActive(true);

            if (rb.linearVelocity.magnitude != 0 && isGround && !longJumpWindowActivate)
            {
                Vector3 currentVelocity = rb.linearVelocity;
                float deceleration = 8.5f;

                Vector3 planar = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
                planar = Vector3.MoveTowards(planar, Vector3.zero, deceleration * Time.fixedDeltaTime);
                rb.linearVelocity = new Vector3(planar.x, currentVelocity.y, planar.z);
            }
        }
    }

    void StopCharacterWhileHoldingOppositeDirections()
    {
        if ((Input.GetKey(KeyCode.A) && Input.GetKeyDown(KeyCode.D)) ||
          (Input.GetKey(KeyCode.D) && Input.GetKeyDown(KeyCode.A)))
        {
            Vector3 v = rb.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            rb.linearVelocity = v;




        }

        if ((Input.GetKey(KeyCode.W) && Input.GetKeyDown(KeyCode.S)) ||
            (Input.GetKey(KeyCode.S) && Input.GetKeyDown(KeyCode.W)))
        {
            Vector3 v = rb.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            rb.linearVelocity = v;


        }
    }
  
    private void RotateCharacterWhileIdle()
    {if (characterMoveState != SpeedState.Static) return;

    if      (Input.GetKeyDown(KeyCode.W)) Face(camForward);
    else if (Input.GetKeyDown(KeyCode.S)) Face(camBackward);
    else if (Input.GetKeyDown(KeyCode.D)) Face(camRight);
    else if (Input.GetKeyDown(KeyCode.A)) Face(camLeft);
    else
    {
      
        if      (Input.GetKey(KeyCode.D) && !Input.GetKey(KeyCode.A)) Face(camRight);
        else if (Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.D)) Face(camLeft);
        else if (Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.S)) Face(camForward);
        else if (Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.W)) Face(camBackward);
    }

    void Face(Vector3 dir)
    {
        var desired = dir.normalized;
        var target = Quaternion.LookRotation(desired, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1000f * Time.deltaTime);
    }
    }

  

    
    void RotateCharacterWhileWalkAndRun()
    {
        if (characterMoveState != SpeedState.Walk && characterMoveState != SpeedState.Run)
            return;

        // 基于相机的四向向量（保持水平分量）
        Vector3 fwd = Flat(camForward);
        Vector3 back = Flat(camBackward);
        Vector3 right = Flat(camRight);
        Vector3 left = Flat(camLeft);

        // 键权重：按住即给该方向持续“转向增量”
        int w = Input.GetKey(KeyCode.W) ? 1 : 0;
        int s = Input.GetKey(KeyCode.S) ? 1 : 0;
        int d = Input.GetKey(KeyCode.D) ? 1 : 0;
        int a = Input.GetKey(KeyCode.A) ? 1 : 0;

        // 合成“期望朝向”向量（可同时按键，自动取对角）
        Vector3 desired = fwd * w + back * s + right * d + left * a;
        desired = Flat(desired);
        if (desired.sqrMagnitude == 0f) return; // 没有方向需求就不旋转

        // 按角速度持续朝“期望朝向”旋转（持续增加已转角度）
        Vector3 current = Flat(transform.forward);
        float maxRadiansThisFrame = rotationSpeed * Mathf.Deg2Rad * Time.deltaTime;
        Vector3 newDir = Vector3.RotateTowards(current, desired, maxRadiansThisFrame, 0f);
        transform.rotation = Quaternion.LookRotation(newDir, Vector3.up);

        Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0f ? v.normalized : Vector3.zero;
        }
    }

    private void ApplyGravity()
    {
        if (!isJumping && !isGround && !isLongJumping)
        {
            Vector3 GravityVector = Vector3.down * gravity;
            rb.AddForce(GravityVector, ForceMode.Acceleration);
        }
    }
    void FixedUpdate()
    {
        ApplyGravity();

        bool oppositeHeld = (Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D)) ||
                       (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.S));

        if (isBackJumping || isLongJumping)
        {
            return;
        }

        if (oppositeHeld)
        {
            Vector3 v = rb.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            rb.linearVelocity = v;          // 清零
                                            // 可选：一点阻尼，防抖更稳
                                            // rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            return;                                     // 关键：不要再加力了
        }

        HandleMovement();
        RotateCharacterWhileWalkAndRun();
    }

    private void HandleMovement()
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
        {
            if (isBackJumping || isLongJumping )
            { 
                return;
            }
            cameraRotationControllor.Damping = 4.6f;

            if (characterGestureState == GestureState.Counch)
            {
                rotationSpeed = 30f;
            }
            else
            {
                rotationSpeed = 90f;

            }
            if (characterGestureState != GestureState.Rolling)
            {
                rb.AddForce(transform.forward * acceleration, ForceMode.Acceleration);

            }
            else if (characterGestureState == GestureState.Rolling)
            {
                //rb.AddForce(transform.forward * acceleration/5, ForceMode.Acceleration);
            }
        }
        else
        {
            Vector3 v = rb.linearVelocity;
            Vector3 planar1 = new Vector3(v.x, 0f, v.z);
            planar1 = Vector3.MoveTowards(planar1, Vector3.zero, deceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(planar1.x, v.y, planar1.z);

        }
        if (characterGestureState != GestureState.Rolling)
        {
            var S = rb.linearVelocity;

            Vector3 planar = new Vector3(S.x, 0f, S.z);
            if (planar.magnitude >= maxSpeed)
            {
                planar = planar.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(planar.x, S.y, planar.z);
            }

        }
        else if (characterGestureState == GestureState.Rolling)
        {
            var S = rb.linearVelocity;

            Vector3 planar = new Vector3(S.x, 0f, S.z);
            if (planar.magnitude >= maxRollingSpeed)
            {
                planar = planar.normalized * maxRollingSpeed;
                rb.linearVelocity = new Vector3(planar.x, S.y, planar.z);
            }

          
        }

    }
}

