using UnityEngine;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;     // 访问 TrackerSettings / BindingMode


public class CameraSystem : MonoBehaviour
{
    [Header("Targets")]
    public Transform followTarget;

    [Header("References")]
    public CinemachineCamera cmCamera;
    private CinemachineFollow follow;

    [Header("Offsets")]
    public Vector3 normalOffset = new Vector3(0f, 6f, -8f);   // 平时：世界空间偏移
    public Vector3 behindOffset = new Vector3(0f, 3.5f, -5f); // 触发后：目标局部空间“身后”

    [Header("Blend & Smoothing")]
    public float moveLerpTime = 0.35f;
    public float rotSnapLerpTime = 0.25f;

    [Header("Behavior")]
    public bool continuousFollowRotation = false;
    public bool startInNormalMode = true;

    // state
    private bool inBehindMode = false;
    private bool isBlending = false;

    void Reset()
    {
        cmCamera = GetComponent<CinemachineCamera>();
    }

    void Awake()
    {
        // 1) 在当前对象上找 CinemachineCamera
        if (!cmCamera)
            TryGetComponent(out cmCamera);

        // 2) 如果还没找到，尝试在子物体里找（有些人把脚本挂在父物体上）
        if (!cmCamera)
            cmCamera = GetComponentInChildren<CinemachineCamera>(true);

        if (!cmCamera)
        {
            Debug.LogError("[CameraSystem] 没找到 CinemachineCamera。请把脚本挂在含有 CinemachineCamera 组件的对象上（不是 Main Camera）。");
            enabled = false;
            return;
        }

        // 3) 拿到/添加 CinemachineFollow
        if (!cmCamera.TryGetComponent(out follow))
            follow = cmCamera.gameObject.AddComponent<CinemachineFollow>();
    }

    void Start()
    {
        if (!followTarget)
        {
            Debug.LogError("请设置 Follow Target");
            enabled = false;
            return;
        }

        cmCamera.Follow = followTarget;   // CM3: 直接用 Follow/LookAt
        cmCamera.LookAt = null;

        if (startInNormalMode) EnterNormalMode(true);
        else EnterBehindMode(true);
    }

    void LateUpdate()
    {
        // 示例：按 Q 切换
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (!inBehindMode) EnterBehindMode(false);
            else EnterNormalMode(false);
        }

        // 连续跟随朝向（可选）
        if (inBehindMode && continuousFollowRotation && !isBlending)
        {
            Quaternion cur = cmCamera.transform.rotation;
            Quaternion trg = followTarget.rotation;
            float t = 1f - Mathf.Exp(-8f * Time.deltaTime);
            cmCamera.transform.rotation = Quaternion.Slerp(cur, trg, t);
        }
    }

    public void EnterNormalMode(bool immediate)
    {
        inBehindMode = false;
        StopAllCoroutines();

        // CM3：通过 TrackerSettings 设置绑定模式（struct，需要回写）
        var ts = follow.TrackerSettings;
        ts.BindingMode = BindingMode.WorldSpace;            // 世界空间：不随目标旋转
        follow.TrackerSettings = ts;

        if (immediate)
        {
            follow.FollowOffset = normalOffset;
        }
        else
        {
            StartCoroutine(LerpOffset(follow.FollowOffset, normalOffset, moveLerpTime));
        }
    }

    public void EnterBehindMode(bool immediate)
    {
        inBehindMode = true;
        StopAllCoroutines();

        // 目标空间（随目标旋转），保持“身后”关系
        var ts = follow.TrackerSettings;
        ts.BindingMode = BindingMode.LockToTargetWithWorldUp;  // 忽略俯仰和滚转，只跟随 yaw
        follow.TrackerSettings = ts;

        if (immediate)
        {
            follow.FollowOffset = behindOffset;
            cmCamera.transform.rotation = followTarget.rotation;
        }
        else
        {
            StartCoroutine(LerpOffset(follow.FollowOffset, behindOffset, moveLerpTime));
            StartCoroutine(LerpRotation(cmCamera.transform.rotation, followTarget.rotation, rotSnapLerpTime));
        }
    }

    private System.Collections.IEnumerator LerpOffset(Vector3 from, Vector3 to, float time)
    {
        isBlending = true;
        float t = 0f;
        time = Mathf.Max(0.0001f, time);
        while (t < 1f)
        {
            t += Time.deltaTime / time;
            follow.FollowOffset = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        isBlending = false;
    }

    private System.Collections.IEnumerator LerpRotation(Quaternion from, Quaternion to, float time)
    {
        isBlending = true;
        float t = 0f;
        time = Mathf.Max(0.0001f, time);
        while (t < 1f)
        {
            t += Time.deltaTime / time;
            cmCamera.transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        isBlending = false;
    }
}
