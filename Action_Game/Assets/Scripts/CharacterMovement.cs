using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CharacterMovement : MonoBehaviour
{
    public float acceleration = 10f;    // ���ٶ�
    public float maxSpeed = 12f;         // ����ٶ�
    public float deceleration = 8f;     // �ɿ�����ʱ�ļ��ٶ�
    public float rotationSpeed = 420f;   // ת���ٶȣ������ý�ɫ�����ƶ�����

    public CinemachineRotateWithFollowTarget cameraRotationControllor;

    public Transform camera;

    public bool isGround;

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
        CrounchDetection();
        StandChange();
        RotateCharacterWhileIdle();

    }

    void SetCameraRotationWhileIdle()
    {
        if (characterMoveState != SpeedState.Static) return;

        camera.rotation = Quaternion.LookRotation(transform.forward, transform.up);
    }
    void StateDetection()
    {
        if (rb.linearVelocity.magnitude > 0f && rb.linearVelocity.magnitude <= 4f)
        {
            characterMoveState = SpeedState.Walk;
        }
        else if (rb.linearVelocity.magnitude <= 0f)
        {
            characterMoveState = SpeedState.Static;

        }
        else if (rb.linearVelocity.magnitude >= 4f)
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

    void CrounchChange()
    {
        if (characterGestureState == GestureState.Counch)
        {
            mesh.enabled = false;
            headIndicator.SetActive(false);
            crounchObject.SetActive(true);

            if (rb.linearVelocity.magnitude != 0)
            {
                Vector3 currentVelocity = rb.linearVelocity;

                float deceleration = 8.5f;

                Vector3 newVelocity = Vector3.MoveTowards(
                    currentVelocity,
                    Vector3.zero,
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
            rb.linearVelocity = Vector3.zero;




        }

        if ((Input.GetKey(KeyCode.W) && Input.GetKeyDown(KeyCode.S)) ||
            (Input.GetKey(KeyCode.S) && Input.GetKeyDown(KeyCode.W)))
        {
            rb.linearVelocity = Vector3.zero;


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

    private Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        if (v.sqrMagnitude > 0f) v.Normalize();
        return v;
    }
    

    private static float DeltaAngle(float fromDeg, float toDeg)
    {
        float d = Mathf.DeltaAngle(fromDeg, toDeg);
        return d;
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
            rb.linearVelocity = Vector3.zero;           // ����
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
            rb.linearVelocity = Vector3.zero;
            //cameraRotationControllor.Damping = .2f;

        }


        if (rb.linearVelocity.magnitude >= maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

    }
}
