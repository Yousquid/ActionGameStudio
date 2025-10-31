using UnityEngine;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;     // ���� TrackerSettings / BindingMode


public class CameraSystem : MonoBehaviour
{
    [Header("Targets")]
    public Transform followTarget;

    [Header("References")]
    public CinemachineCamera cmCamera;
    private CinemachineFollow follow;

    [Header("Offsets")]
    public Vector3 normalOffset = new Vector3(0f, 6f, -8f);   // ƽʱ������ռ�ƫ��
    public Vector3 behindOffset = new Vector3(0f, 3.5f, -5f); // ������Ŀ��ֲ��ռ䡰���

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
        // 1) �ڵ�ǰ�������� CinemachineCamera
        if (!cmCamera)
            TryGetComponent(out cmCamera);

        // 2) �����û�ҵ������������������ң���Щ�˰ѽű����ڸ������ϣ�
        if (!cmCamera)
            cmCamera = GetComponentInChildren<CinemachineCamera>(true);

        if (!cmCamera)
        {
            Debug.LogError("[CameraSystem] û�ҵ� CinemachineCamera����ѽű����ں��� CinemachineCamera ����Ķ����ϣ����� Main Camera����");
            enabled = false;
            return;
        }

        // 3) �õ�/��� CinemachineFollow
        if (!cmCamera.TryGetComponent(out follow))
            follow = cmCamera.gameObject.AddComponent<CinemachineFollow>();
    }

    void Start()
    {
        if (!followTarget)
        {
            Debug.LogError("������ Follow Target");
            enabled = false;
            return;
        }

        cmCamera.Follow = followTarget;   // CM3: ֱ���� Follow/LookAt
        cmCamera.LookAt = null;

        if (startInNormalMode) EnterNormalMode(true);
        else EnterBehindMode(true);
    }

    void LateUpdate()
    {
        // ʾ������ Q �л�
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (!inBehindMode) EnterBehindMode(false);
            else EnterNormalMode(false);
        }

        // �������泯�򣨿�ѡ��
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

        // CM3��ͨ�� TrackerSettings ���ð�ģʽ��struct����Ҫ��д��
        var ts = follow.TrackerSettings;
        ts.BindingMode = BindingMode.WorldSpace;            // ����ռ䣺����Ŀ����ת
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

        // Ŀ��ռ䣨��Ŀ����ת�������֡���󡱹�ϵ
        var ts = follow.TrackerSettings;
        ts.BindingMode = BindingMode.LockToTargetWithWorldUp;  // ���Ը����͹�ת��ֻ���� yaw
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
