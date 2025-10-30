using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterMovement : MonoBehaviour
{
    public float acceleration = 10f;    // 加速度
    public float maxSpeed = 6f;         // 最大速度
    public float deceleration = 8f;     // 松开按键时的减速度
    public float rotationSpeed = 10f;   // 转向速度（用于让角色朝向移动方向）

    public Transform camera;

    private Rigidbody rb;
    private Vector3 moveVelocity;
    private Vector3 camForward, camRight, camLeft, camBackward;

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
        if (camera)
        {
            camForward = camera.forward;
            camRight = camera.right;
            camLeft = -camera.right;
            camBackward = -camera.forward; ;
        }
        else
        {
            camForward = Vector3.forward;
            camRight = Vector3.right;
            
        }

        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();
    }


    private void InputDetection()
    {

        RotateCharacterWhileIdle();

        //RotateCharacterWhile
    }
    void StateDetection()
    {
        if (rb.linearVelocity.magnitude > 0f)
        {
            characterMoveState = MoveState.Walk;
        }
        else if (rb.linearVelocity.magnitude <= 0f)
        {
            characterMoveState = MoveState.Idle;

        }
    }
    private void RotateCharacterWhileIdle()
    {
        if (characterMoveState == MoveState.Idle)
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                Vector3 flatCamForward = camForward.normalized;
                Quaternion targetRot = Quaternion.LookRotation(flatCamForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1000f * Time.deltaTime);
            }
            if (Input.GetKeyDown(KeyCode.A))
            {
                Vector3 flatCamForward = camLeft.normalized;
                Quaternion targetRot = Quaternion.LookRotation(flatCamForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1000f * Time.deltaTime);
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                Vector3 flatCamForward = camBackward.normalized;
                Quaternion targetRot = Quaternion.LookRotation(flatCamForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1000f * Time.deltaTime);
            }
            if (Input.GetKeyDown(KeyCode.D))
            {
                Vector3 flatCamForward = camRight.normalized;
                Quaternion targetRot = Quaternion.LookRotation(flatCamForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1000f * Time.deltaTime);
            }
        }
    }

    void FixedUpdate()
    {
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
