using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;
public class CharacterMovement : MonoBehaviour
{
    public float acceleration = 10f;    // ���ٶ�
    public float maxSpeed = 12f;         // ����ٶ�
    public float deceleration = 8f;     // �ɿ�����ʱ�ļ��ٶ�
    public float rotationSpeed = 420f;   // ת���ٶȣ������ý�ɫ�����ƶ�����

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
        Jump
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
    public AnimationCurve horizontalForceCurve;   // �����ߣ�0~1֮�䣩
    public bool isBackJumping = false;

    [Header("LongJumpSettings")]
    public float longJumpForce = 7f;
    public float longJumpHorizontalForce = 300f;
    public float longJumpForceDuration = 0.25f;
    public AnimationCurve longJumpHorizontalForceCurve;   // �����ߣ�0~1֮�䣩
    public bool isLongJumping = false;
    public float longJumpTimer = 0f;
    public float longJumpWindow = .17f;
    private bool longJumpWindowActivate = false;
    private bool longJumpWindowActivateTwo = false;


    public int currentJumpCount = 0;


    public float secondJumpWindow = 1.0f;   
    public bool secondWindowActive = false;
    private float secondWindowTimer = 0f;

    private float ignoreGroundTimer = 0;

    void Start()
    {
        mesh = GetComponent<MeshRenderer>();
        characterGestureState = GestureState.Stand;
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // ��ֹ������ת


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
        StandChange();
        RotateCharacterWhileIdle();

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
        else if (rb.linearVelocity.magnitude >= 5f)
        {
            characterMoveState = SpeedState.Run;

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

            // ����ֱ�ٶ�
            Vector3 v = rb.linearVelocity; v.y = 0f; rb.linearVelocity = v;

            Vector3 jumpVector = Vector3.zero;

            if (characterGestureState != GestureState.Counch)
            {
                if (thirdWindowActive)
                {
                    jumpVector = Vector3.up * thirdJumpForce;
                    currentJumpCount = 0;           // ���� third ��Ѽ������㣨����ԭ������
                    thirdWindowActive = false;      // ���ĵ����δ���
                    secondWindowActive = false;     // ���գ�Ҳ�ص��ڶ��δ���
                }
                else if (secondWindowActive)
                {
                    jumpVector = Vector3.up * secondJumpForce;
                    currentJumpCount += 1;          // �� 1 �� 2
                    secondWindowActive = false;     // ���ĵڶ��δ���
                }
                else
                {
                    jumpVector = Vector3.up * jumpForce;
                    currentJumpCount = 1;           // ��ͨ������ʼ������0 �� 1
                }

                rb.AddForce(jumpVector, ForceMode.Impulse);

            }
            else if (characterGestureState == GestureState.Counch && !longJumpWindowActivate)
            {
                StartCoroutine(DoBackstep());
                //jumpVector = Vector3.up * backJumpForce;
                //Vector3 backDistance = -Vector3.forward * backJumpHorizontalForce;
                //rb.AddForce(jumpVector, ForceMode.Impulse);

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

        if (!isJumping && !isGround)
        {
            Vector3 GravityVector = Vector3.down * gravity;
            rb.AddForce(GravityVector, ForceMode.Acceleration);
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

            // �����ߣ�Ĭ�����ԣ����û������ֱ������������
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
        rb.linearVelocity = v;


        Vector3 jumpVector = Vector3.up * longJumpForce;
        rb.AddForce(jumpVector, ForceMode.Impulse);


        float timer = 0f;
        while (timer < longJumpForceDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / longJumpForceDuration);

            // �����ߣ�Ĭ�����ԣ����û������ֱ������������
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
                // �������1��������2�δ���
                secondWindowActive = true;
                secondWindowTimer = secondJumpWindow;
                thirdWindowActive = false; // ����
            }
            else if (currentJumpCount == 2 && characterMoveState == SpeedState.Run)
            {
                // �������2��������3�δ���
                thirdWindowActive = true;
                thirdWindowTimer = thirdJumpWindow; //
                secondWindowActive = false;            // 
            }
            else
            {
                // ���������أ������������ɺ��ʱ�󣩲��Զ�������
            }
        }

        // ���ڵ���ʱ���������
        if (secondWindowActive)
        {
            secondWindowTimer -= Time.deltaTime;
            if (secondWindowTimer <= 0f)
            {
                secondWindowActive = false;
                // ֻ�е������δ��ڲ���ʱ���Ű��������㣨�����ת�� third ��˲�䱻���㣩
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
        if (Input.GetKey(KeyCode.LeftShift))
        {
            if (characterGestureState == GestureState.Jump) return;

            characterGestureState = GestureState.Counch;

        }
        else { characterGestureState = GestureState.Stand; }

        CrounchChange();
    }

    void LongJumpWindowTimer()
    {
        
            longJumpTimer -= Time.deltaTime;


        if (longJumpTimer > 0)
        {
            longJumpWindowActivate = true;
        }
        else
        {
            longJumpWindowActivate = false;
            longJumpWindowActivateTwo = false;
        }
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
            

            if (rb.linearVelocity.magnitude != 0 && isGround)
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

        // �����������������������ˮƽ������
        Vector3 fwd = Flat(camForward);
        Vector3 back = Flat(camBackward);
        Vector3 right = Flat(camRight);
        Vector3 left = Flat(camLeft);

        // ��Ȩ�أ���ס�����÷��������ת��������
        int w = Input.GetKey(KeyCode.W) ? 1 : 0;
        int s = Input.GetKey(KeyCode.S) ? 1 : 0;
        int d = Input.GetKey(KeyCode.D) ? 1 : 0;
        int a = Input.GetKey(KeyCode.A) ? 1 : 0;

        // �ϳɡ�����������������ͬʱ�������Զ�ȡ�Խǣ�
        Vector3 desired = fwd * w + back * s + right * d + left * a;
        desired = Flat(desired);
        if (desired.sqrMagnitude == 0f) return; // û�з�������Ͳ���ת

        // �����ٶȳ�����������������ת������������ת�Ƕȣ�
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
    void FixedUpdate()
    {
        bool oppositeHeld = (Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D)) ||
                       (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.S));

        if (oppositeHeld)
        {
            Vector3 v = rb.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            rb.linearVelocity = v;          // ����
                                            // ��ѡ��һ�����ᣬ��������
                                            // rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            return;                                     // �ؼ�����Ҫ�ټ�����
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
            v.x = 0f;
            v.z = 0f;
            rb.linearVelocity = v;

        }


        if (rb.linearVelocity.magnitude >= maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

    }
}

