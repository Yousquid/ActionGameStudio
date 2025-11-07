using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;
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

    [Header("RollingSettings")]
    public GameObject rollingObject;
    public bool isRolling = false;
    public float maxRollingSpeed = 30f;
    

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
        RollingDetection();
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
        if (Input.GetKey(KeyCode.K) && characterMoveState == SpeedState.Run && characterGestureState != GestureState.Rolling)
        {
            characterGestureState = GestureState.Rolling;
        }
        else if (Input.GetKeyUp(KeyCode.K))
        {
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
        }
    }
    void StandChange()
    {
        if (characterGestureState == GestureState.Stand)
        {
            mesh.enabled = true;
            headIndicator.SetActive(true);
            crounchObject.SetActive(false);
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

                rb.AddForce(jumpVector, ForceMode.Impulse);

            }
            else if (characterGestureState == GestureState.Counch && !longJumpWindowActivate)
            {
                StartCoroutine(DoBackstep());

            }
            else if (characterGestureState == GestureState.Counch && longJumpWindowActivate)
            {
                StartCoroutine(DoLongstep());
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
        v.y = 0;
        v.x = v.x/3;
        v.z = v.z/3;
        rb.linearVelocity = v;


        Vector3 jumpVector = Vector3.up * longJumpForce;
        rb.AddForce(jumpVector, ForceMode.Impulse);


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
        isGround = Physics.Raycast(
        transform.position, Vector3.down,
        playerHeight * 0.5f + 0.05f, groundLayer
    );

        if (!isGround)
        {
            characterGestureState = GestureState.Jump;
        }
        if (hasJumped)
            ignoreGroundTimer += Time.deltaTime;

        if (isGround && ignoreGroundTimer >= 0.08f)
        {
            isJumping = false;
            ignoreGroundTimer = 0f;
            hasJumped = false;
            characterGestureState = GestureState.Stand;


            if (currentJumpCount == 1)
            {
                // 刚做完第1跳，开第2段窗口
                secondWindowActive = true;
                secondWindowTimer = secondJumpWindow;
                thirdWindowActive = false; // 保险
            }
            else if (currentJumpCount == 2 && characterMoveState == SpeedState.Run)
            {
                // 刚做完第2跳，开第3段窗口
                thirdWindowActive = true;
                thirdWindowTimer = thirdJumpWindow; //
                secondWindowActive = false;            // 
            }
            else
            {
                // 其它情况落地（比如第三段完成后或超时后）不自动开窗口
            }
        }

        // 窗口倒计时与过期清理
        if (secondWindowActive)
        {
            secondWindowTimer -= Time.deltaTime;
            if (secondWindowTimer <= 0f)
            {
                secondWindowActive = false;
                // 只有当第三段窗口不在时，才把链条清零（避免刚转入 third 的瞬间被清零）
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
    }

   
    void CrounchDetection()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && characterGestureState != GestureState.Jump)
        {
            characterGestureState = GestureState.Counch;

            longJumpTimer = longJumpWindow;
        }
        else if (Input.GetKey(KeyCode.LeftShift))
        {
            characterGestureState = GestureState.Counch;
        }
        else
        {
            characterGestureState = GestureState.Stand;
            longJumpTimer = 0f;
            longJumpWindowActivate = false;
        }

        CrounchChange();
    }

    void LongJumpWindowTimer()
    {
        if (longJumpTimer > 0f)
            longJumpTimer -= Time.deltaTime;

        longJumpWindowActivate = longJumpTimer > 0f;  // 直接由计时器决定
    }

    void CrounchChange()
    {
        if (characterGestureState == GestureState.Counch)
        {
            mesh.enabled = false;
            headIndicator.SetActive(false);
            crounchObject.SetActive(true);

            if (!longJumpWindowActivateTwo)
            {
                longJumpTimer = longJumpWindow;
                longJumpWindowActivateTwo = true;
            }
            

            if (rb.linearVelocity.magnitude != 0 && isGround && longJumpTimer <= 0)
            {
                Vector3 currentVelocity = rb.linearVelocity;

                float deceleration = 8.5f;

                Vector3 newVelocity;

                newVelocity = currentVelocity;

                newVelocity.x = 0;
                newVelocity.z = 0;

                Vector3 renewVelocity = Vector3.MoveTowards(
                    currentVelocity,
                    newVelocity,
                    deceleration * Time.fixedDeltaTime
                );

                rb.linearVelocity = newVelocity;
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
            if (isBackJumping || isLongJumping)
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
            rb.AddForce(transform.forward * acceleration, ForceMode.Acceleration);
        }
        else
        {
            Vector3 v = rb.linearVelocity;
            Vector3 planar1 = new Vector3(v.x, 0f, v.z);
            planar1 = Vector3.MoveTowards(planar1, Vector3.zero, deceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(planar1.x, v.y, planar1.z);

        }

        var S = rb.linearVelocity;

        Vector3 planar = new Vector3(S.x, 0f, S.z);
        if (planar.magnitude >= maxSpeed)
        {
            planar = planar.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(planar.x, S.y, planar.z);
        }

    }
}

