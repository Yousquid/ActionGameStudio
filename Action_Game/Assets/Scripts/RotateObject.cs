using UnityEngine;

public class RotateObject : MonoBehaviour
{
    public Rigidbody rb;
    public float rollMultiplier = 1.0f;  // 控制滚动速度（越大滚得越快）

    void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // 获取球的速度方向
        Vector3 velocity = rb.linearVelocity;

        // 没有速度就不滚动
        if (velocity.sqrMagnitude < 0.001f)
            return;

        // 根据速度方向计算旋转轴：方向 × 上向量 = 横向旋转轴
        Vector3 rollAxis = Vector3.Cross(velocity.normalized, Vector3.up);

        // 旋转角速度 = 移动速度 / 半径（假设半径 0.5，可调 rollMultiplier）
        float rollSpeed = velocity.magnitude * rollMultiplier;

        // 应用旋转
        transform.Rotate(rollAxis, rollSpeed * Time.fixedDeltaTime, Space.World);
    }
}
