using UnityEngine;

public class CameraSystem : MonoBehaviour
{
    public enum CameraMode { Lakitu, Mario }

    [Header("Target")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0f, 1.5f, 0f); // 角色头顶/肩部附近

    [Header("Mode")]
    public CameraMode mode = CameraMode.Lakitu;

    [Header("Yaw / Pitch")]
    [Tooltip("单次分段旋转的角度")]
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
    public LayerMask collisionMask = ~0;     // 默认对所有层做碰撞
    public float sphereRadius = 0.22f;       // 防穿墙检测半径
    public float wallPadding = 0.10f;        // 离墙保留距离
    public float minDistance = 0.7f;         // 相机最小可推进距离
    public float raiseWhenBlocked = 0.35f;   // 被挡时抬升量

    [Header("Mario Mode Tuning")]
    [Tooltip("Mario 模式下，镜头会朝角色朝向自动偏转的强度（0~1）")]
    public float marioHeadingFollow = 0.6f;

    [Header("General Smooth")]
    public float positionSmooth = 18f;
    public float rotationSmooth = 18f;

    // Inputs (老版 Input；若用新 Input System，可在 UpdateInputs 中替换)
    [Header("Input (legacy)")]
    public KeyCode rotateLeftKey = KeyCode.Q;       // C-Left
    public KeyCode rotateRightKey = KeyCode.E;      // C-Right
    public KeyCode zoomInKey = KeyCode.Z;           // 近
    public KeyCode zoomOutKey = KeyCode.X;          // 远
    public KeyCode toggleModeKey = KeyCode.C;       // 切 Lakitu/Mario
    public KeyCode pitchUpKey = KeyCode.PageUp;     // 可选：微调俯仰
    public KeyCode pitchDownKey = KeyCode.PageDown;

    // State
    private float desiredYaw;      // 目标分段 yaw
    private float currentYaw;      // 平滑后的 yaw
    private float desiredDistance; // 目标距离（Near/Med/Far 之一）
    private float currentDistance; // 平滑后的距离

    private Transform cam;         // 真正的 Camera transform

    // 区域覆盖

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

        // 初始化 yaw：让镜头朝向角色朝向的后方
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

        // Mario 模式：根据目标的朝向缓慢拉近到角色后方
        if (mode == CameraMode.Mario)
        {
            Vector3 hFwd = target.forward; hFwd.y = 0f;
            if (hFwd.sqrMagnitude > 0.0001f)
            {
                float targetYawFromMario = Mathf.Atan2(hFwd.x, hFwd.z) * Mathf.Rad2Deg;
                // 插值影响到 desiredYaw，但仍保留分段旋转给玩家微调
                desiredYaw = Mathf.LerpAngle(desiredYaw, targetYawFromMario, marioHeadingFollow * Time.deltaTime * 3f);
            }
        }

        // 平滑 yaw 和距离
        currentYaw = Mathf.LerpAngle(currentYaw, desiredYaw, Time.deltaTime * yawSmooth);
        currentDistance = Mathf.Lerp(currentDistance, desiredDistance, Time.deltaTime * zoomSmooth);

        // 计算理想位置
        Quaternion yawRot = Quaternion.Euler(0f, currentYaw, 0f);
        Quaternion pitchRot = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 focus = target.position + targetOffset;
        Vector3 desiredCamPos = focus + yawRot * (pitchRot * (Vector3.back * currentDistance));

        // 碰撞修正
        Vector3 correctedPos = ResolveCollision(focus, desiredCamPos, out bool blocked);

        // 若被挡，略微抬升 + 自动加一点 pitch
        if (blocked)
        {
            correctedPos += Vector3.up * raiseWhenBlocked;
            float targetPitch = Mathf.Clamp(pitch + pitchAutoLiftWhenClose, pitchMin, pitchMax);
            pitch = Mathf.Lerp(pitch, targetPitch, Time.deltaTime * 4f);
        }

        // 平滑移动与朝向
        transform.position = Vector3.Lerp(transform.position, correctedPos, Time.deltaTime * positionSmooth);
        Vector3 lookDir = (focus - transform.position);
        if (lookDir.sqrMagnitude < 0.0001f) lookDir = transform.forward;
        Quaternion lookRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * rotationSmooth);
    }

    private void UpdateInputs()
    {
        // 分段旋转
        if (Input.GetKeyDown(rotateLeftKey))
            desiredYaw -= yawStep;
        if (Input.GetKeyDown(rotateRightKey))
            desiredYaw += yawStep;

        // 多档缩放
        if (Input.GetKeyDown(zoomInKey))
        {
            // 更近一档
            if (Mathf.Approximately(desiredDistance, farDist))
                desiredDistance = mediumDist;
            else if (Mathf.Approximately(desiredDistance, mediumDist))
                desiredDistance = nearDist;
        }
        if (Input.GetKeyDown(zoomOutKey))
        {
            // 更远一档
            if (Mathf.Approximately(desiredDistance, nearDist))
                desiredDistance = mediumDist;
            else if (Mathf.Approximately(desiredDistance, mediumDist))
                desiredDistance = farDist;
        }

        // 切换模式
        if (Input.GetKeyDown(toggleModeKey))
        {
            mode = (mode == CameraMode.Lakitu) ? CameraMode.Mario : CameraMode.Lakitu;
        }

        // 可选：微调俯仰
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
