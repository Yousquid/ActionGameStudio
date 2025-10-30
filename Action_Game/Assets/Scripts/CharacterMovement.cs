using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterMovement : MonoBehaviour
{
    public float acceleration = 10f;    // 加速度
    public float maxSpeed = 12f;         // 最大速度
    public float deceleration = 8f;     // 松开按键时的减速度
    public float rotationSpeed = 10f;   // 转向速度（用于让角色朝向移动方向）

    public Transform camera;

    private Rigidbody rb;
    private Vector3 moveVelocity;
    private Vector3 camForward, camRight, camLeft, camBackward;
    private KeyCode counterDirectionKey;

    public enum MoveState
    {
        Idle,
        Walk,
        Run
    }

    public MoveState characterMoveState;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // 防止物理旋转
    }

    void Update()
    {
        ReadCameraBasis();
        InputDetection();
        StateDetection();
    }

    void ReadCameraBasis()
    {
            camForward = camera.forward;
            camRight = camera.right;
            camLeft = -camera.right;
            camBackward = -camera.forward; ;
        
       

        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();
    }


    private void InputDetection()
    {

        if (Input.GetKey(KeyCode.W))
        {
            counterDirectionKey = KeyCode.S;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            counterDirectionKey = KeyCode.W;

        }
        else if (Input.GetKey(KeyCode.A))
        {
            counterDirectionKey = KeyCode.D;

        }
        else if (Input.GetKey(KeyCode.D))
        {
            counterDirectionKey = KeyCode.A;

        }

        RotateCharacterWhileIdle();
        StopCharacterWhileHoldingOppositeDirections();
        RotateCharacterWhileWalkAndRun();
    }
    void StateDetection()
    {
        if (rb.linearVelocity.magnitude > 0f && rb.linearVelocity.magnitude <= 4f)
        {
            characterMoveState = MoveState.Walk;
        }
        else if (rb.linearVelocity.magnitude <= 0f)
        {
            characterMoveState = MoveState.Idle;

        }
        else if (rb.linearVelocity.magnitude >= 4f)
        {
            characterMoveState = MoveState.Run;

        }
    }

    void StopCharacterWhileHoldingOppositeDirections()
    {
        if (Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D))
        {
            rb.linearVelocity = Vector3.zero;
        }

        if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.S))
        {
            rb.linearVelocity = Vector3.zero;
        }
    }
    void RotateCharacterWhileWalkAndRun()
    {
        if ((Input.GetKey(KeyCode.A) && Input.GetKeyDown(KeyCode.D)) ||
           (Input.GetKey(KeyCode.D) && Input.GetKeyDown(KeyCode.A)))
        {
            rb.linearVelocity = Vector3.zero;

            
        }

        if ((Input.GetKey(KeyCode.W) && Input.GetKeyDown(KeyCode.S)) ||
            (Input.GetKey(KeyCode.S) && Input.GetKeyDown(KeyCode.W)))
        {
            rb.linearVelocity = Vector3.zero;

            
        }
    }
    private void RotateCharacterWhileIdle()
    {
        if (characterMoveState == MoveState.Idle)
        {
            if (Input.GetKeyDown(KeyCode.W)|| Input.GetKey(KeyCode.W))
            {
                Vector3 flatCamForward = camForward.normalized;
                Quaternion targetRot = Quaternion.LookRotation(flatCamForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1000f * Time.deltaTime);
            }
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKey(KeyCode.A))
            {
                Vector3 flatCamForward = camLeft.normalized;
                Quaternion targetRot = Quaternion.LookRotation(flatCamForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1000f * Time.deltaTime);
            }
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKey(KeyCode.S))
            {
                Vector3 flatCamForward = camBackward.normalized;
                Quaternion targetRot = Quaternion.LookRotation(flatCamForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1000f * Time.deltaTime);
            }
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKey(KeyCode.D))
            {
                Vector3 flatCamForward = camRight.normalized;
                Quaternion targetRot = Quaternion.LookRotation(flatCamForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1000f * Time.deltaTime);
            }
        }
    }

    void FixedUpdate()
    {
        StopCharacterWhileHoldingOppositeDirections();
        bool oppositeHeld = (Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D)) ||
                       (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.S));

        if (oppositeHeld)
        {
            rb.linearVelocity = Vector3.zero;           // 清零
                                                        // 可选：一点阻尼，防抖更稳
                                                        // rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            return;                                     // 关键：不要再加力了
        }

        HandleMovement();
    }

    private void HandleMovement()
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
        {
            rb.AddForce(transform.forward * acceleration, ForceMode.Acceleration);
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
        }


        if (rb.linearVelocity.magnitude >= maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

    }
}
