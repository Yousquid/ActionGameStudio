using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CharacterMovement : MonoBehaviour
{
    public float acceleration = 10f;    // 加速度
    public float maxSpeed = 12f;         // 最大速度
    public float deceleration = 8f;     // 松开按键时的减速度
    public float rotationSpeed = 420f;   // 转向速度（用于让角色朝向移动方向）

    public CinemachineRotateWithFollowTarget cameraRotationControllor;

    public Transform camera;

    private Rigidbody rb;
    private Vector3 moveVelocity;
    private Vector3 camForward, camRight, camLeft, camBackward;
    private KeyCode counterDirectionKey;

    [Header("SM64-like Turning")]
    public float yawAccelDegPerSec2 = 2200f;   // 角加速度（越大越迅猛）
    public float yawFrictionDegPerSec = 500f;  // 角速度摩擦（松开/对齐时更快停下）
    public float yawSpeedCapDegPerSec = 420f;  // 最大角速度（类似你原来的 rotationSpeedDeg）

    // 轻微“向前偏置”（前/后>左右）
    [Range(0.5f, 2f)] public float forwardWeight = 1.0f;
    [Range(0.5f, 2f)] public float lateralWeight = 0.85f;

    // ====== Runtime state（运行时状态）======
    private float currentYawDeg;   // 角色当前朝向（水平），用欧拉角缓存
    private float yawVelDegPerSec; // 当前角速度

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
        currentYawDeg = transform.rotation.eulerAngles.y;

    }

    void Update()
    {
        //SetCameraRotationWhileIdle();
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
 
    }


    private void InputDetection()
    {

        StopCharacterWhileHoldingOppositeDirections();
        RotateCharacterWhileIdle();

    }

    void SetCameraRotationWhileIdle()
    {
        if (characterMoveState != MoveState.Idle) return;

        camera.rotation = Quaternion.LookRotation(transform.forward, transform.up);
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
    {if (characterMoveState != MoveState.Idle) return;

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

    private Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        if (v.sqrMagnitude > 0f) v.Normalize();
        return v;
    }
    private bool TryGetDesiredDir(out Vector3 desiredDir)
    {
        desiredDir = Vector3.zero;

        Vector3 fwd = Flat(camForward);
        Vector3 back = Flat(camBackward);
        Vector3 right = Flat(camRight);
        Vector3 left = Flat(camLeft);

        // 键输入
        int w = Input.GetKey(KeyCode.W) ? 1 : 0;
        int s = Input.GetKey(KeyCode.S) ? 1 : 0;
        int d = Input.GetKey(KeyCode.D) ? 1 : 0;
        int a = Input.GetKey(KeyCode.A) ? 1 : 0;

        if ((w | s | a | d) == 0) return false;

        // 前后稍重，左右稍轻，让“前+左/右”更偏前
        desiredDir = fwd * (w * forwardWeight)
                   + back * (s * forwardWeight)
                   + right * (d * lateralWeight)
                   + left * (a * lateralWeight);

        desiredDir = Flat(desiredDir);
        return desiredDir.sqrMagnitude > 0f;
    }

    private static float DeltaAngle(float fromDeg, float toDeg)
    {
        float d = Mathf.DeltaAngle(fromDeg, toDeg);
        return d;
    }
    void RotateCharacterWhileWalkAndRun()
    {
        if (characterMoveState != MoveState.Walk && characterMoveState != MoveState.Run)
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
    void FixedUpdate()
    {
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
        RotateCharacterWhileWalkAndRun();
    }

    private void HandleMovement()
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
        {
            cameraRotationControllor.Damping = 4.6f;
            rb.AddForce(transform.forward * acceleration, ForceMode.Acceleration);
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
            //cameraRotationControllor.Damping = .2f;

        }


        if (rb.linearVelocity.magnitude >= maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

    }
}
