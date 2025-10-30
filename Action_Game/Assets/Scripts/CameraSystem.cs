using UnityEngine;

public class CameraSystem : MonoBehaviour
{
    public enum CameraMode { Lakitu, Mario }

    [Header("Target")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0f, 1.5f, 0f); // ��ɫͷ��/�粿����

    [Header("Mode")]
    public CameraMode mode = CameraMode.Lakitu;

    [Header("Yaw / Pitch")]
    [Tooltip("���ηֶ���ת�ĽǶ�")]
    public float yawStep = 45f;
    public float yawSmooth = 12f;
    public float pitch = 20f;
    public float pitchMin = -10f;
    public float pitchMax = 50f;
    public float pitchAutoLiftWhenClose = 10f;

    [Header("Zoom (presets)")]
    public float nearDist = 2.8f;
    public float mediumDist = 4.2f;
    public float farDist = 6.5f;
    public float zoomSmooth = 8f;
    public int startZoomIndex = 1; // 0/1/2 => near/med/far

    [Header("Collision")]
    public LayerMask collisionMask = ~0;     // Ĭ�϶����в�����ײ
    public float sphereRadius = 0.22f;       // ����ǽ���뾶
    public float wallPadding = 0.10f;        // ��ǽ��������
    public float minDistance = 0.7f;         // �����С���ƽ�����
    public float raiseWhenBlocked = 0.35f;   // ����ʱ̧����

    [Header("Mario Mode Tuning")]
    [Tooltip("Mario ģʽ�£���ͷ�ᳯ��ɫ�����Զ�ƫת��ǿ�ȣ�0~1��")]
    public float marioHeadingFollow = 0.6f;

    [Header("General Smooth")]
    public float positionSmooth = 18f;
    public float rotationSmooth = 18f;

    // Inputs (�ϰ� Input�������� Input System������ UpdateInputs ���滻)
    [Header("Input (legacy)")]
    public KeyCode rotateLeftKey = KeyCode.Q;       // C-Left
    public KeyCode rotateRightKey = KeyCode.E;      // C-Right
    public KeyCode zoomInKey = KeyCode.Z;           // ��
    public KeyCode zoomOutKey = KeyCode.X;          // Զ
    public KeyCode toggleModeKey = KeyCode.C;       // �� Lakitu/Mario
    public KeyCode pitchUpKey = KeyCode.PageUp;     // ��ѡ��΢������
    public KeyCode pitchDownKey = KeyCode.PageDown;

    // State
    private float desiredYaw;      // Ŀ��ֶ� yaw
    private float currentYaw;      // ƽ����� yaw
    private float desiredDistance; // Ŀ����루Near/Med/Far ֮һ��
    private float currentDistance; // ƽ����ľ���

    private Transform cam;         // ������ Camera transform

    // ���򸲸�

    void Awake()
    {
        cam = GetComponentInChildren<Camera>() ? GetComponentInChildren<Camera>().transform : Camera.main.transform;
        if (!cam)
        {
            Debug.LogWarning("[SM64Camera] No Camera found as child or Main Camera in scene.");
        }
    }

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("[SM64Camera] Please assign a target.");
            enabled = false;
            return;
        }

        // ��ʼ�� yaw���þ�ͷ�����ɫ����ĺ�
        Vector3 fwd = target.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        desiredYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        currentYaw = desiredYaw;

        desiredDistance = Mathf.Clamp(startZoomIndex, 0, 2) switch
        {
            0 => nearDist,
            2 => farDist,
            _ => mediumDist
        };
        currentDistance = desiredDistance;
    }

    void Update()
    {
        UpdateInputs();

        
    }

    void LateUpdate()
    {
        

        if (!target) return;

        // Mario ģʽ������Ŀ��ĳ�������������ɫ��
        if (mode == CameraMode.Mario)
        {
            Vector3 hFwd = target.forward; hFwd.y = 0f;
            if (hFwd.sqrMagnitude > 0.0001f)
            {
                float targetYawFromMario = Mathf.Atan2(hFwd.x, hFwd.z) * Mathf.Rad2Deg;
                // ��ֵӰ�쵽 desiredYaw�����Ա����ֶ���ת�����΢��
                desiredYaw = Mathf.LerpAngle(desiredYaw, targetYawFromMario, marioHeadingFollow * Time.deltaTime * 3f);
            }
        }

        // ƽ�� yaw �;���
        currentYaw = Mathf.LerpAngle(currentYaw, desiredYaw, Time.deltaTime * yawSmooth);
        currentDistance = Mathf.Lerp(currentDistance, desiredDistance, Time.deltaTime * zoomSmooth);

        // ��������λ��
        Quaternion yawRot = Quaternion.Euler(0f, currentYaw, 0f);
        Quaternion pitchRot = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 focus = target.position + targetOffset;
        Vector3 desiredCamPos = focus + yawRot * (pitchRot * (Vector3.back * currentDistance));

        // ��ײ����
        Vector3 correctedPos = ResolveCollision(focus, desiredCamPos, out bool blocked);

        // ����������΢̧�� + �Զ���һ�� pitch
        if (blocked)
        {
            correctedPos += Vector3.up * raiseWhenBlocked;
            float targetPitch = Mathf.Clamp(pitch + pitchAutoLiftWhenClose, pitchMin, pitchMax);
            pitch = Mathf.Lerp(pitch, targetPitch, Time.deltaTime * 4f);
        }

        // ƽ���ƶ��볯��
        transform.position = Vector3.Lerp(transform.position, correctedPos, Time.deltaTime * positionSmooth);
        Vector3 lookDir = (focus - transform.position);
        if (lookDir.sqrMagnitude < 0.0001f) lookDir = transform.forward;
        Quaternion lookRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * rotationSmooth);
    }

    private void UpdateInputs()
    {
        // �ֶ���ת
        if (Input.GetKeyDown(rotateLeftKey))
            desiredYaw -= yawStep;
        if (Input.GetKeyDown(rotateRightKey))
            desiredYaw += yawStep;

        // �൵����
        if (Input.GetKeyDown(zoomInKey))
        {
            // ����һ��
            if (Mathf.Approximately(desiredDistance, farDist))
                desiredDistance = mediumDist;
            else if (Mathf.Approximately(desiredDistance, mediumDist))
                desiredDistance = nearDist;
        }
        if (Input.GetKeyDown(zoomOutKey))
        {
            // ��Զһ��
            if (Mathf.Approximately(desiredDistance, nearDist))
                desiredDistance = mediumDist;
            else if (Mathf.Approximately(desiredDistance, mediumDist))
                desiredDistance = farDist;
        }

        // �л�ģʽ
        if (Input.GetKeyDown(toggleModeKey))
        {
            mode = (mode == CameraMode.Lakitu) ? CameraMode.Mario : CameraMode.Lakitu;
        }

        // ��ѡ��΢������
        if (Input.GetKey(pitchUpKey)) pitch = Mathf.Clamp(pitch + 45f * Time.deltaTime, pitchMin, pitchMax);
        if (Input.GetKey(pitchDownKey)) pitch = Mathf.Clamp(pitch - 45f * Time.deltaTime, pitchMin, pitchMax);
    }

    private Vector3 ResolveCollision(Vector3 focus, Vector3 desiredPos, out bool blocked)
    {
        blocked = false;
        Vector3 dir = desiredPos - focus;
        float dist = dir.magnitude;
        if (dist <= 0.0001f) return desiredPos;

        dir /= dist;

        // SphereCast from focus towards desiredPos
        if (Physics.SphereCast(focus, sphereRadius, dir, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
        {
            float safeDist = Mathf.Max(hit.distance - wallPadding, minDistance);
            Vector3 pos = focus + dir * safeDist;
            blocked = true;
            return pos;
        }

        return desiredPos;
    }

  
}
