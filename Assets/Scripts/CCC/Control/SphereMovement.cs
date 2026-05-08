using UnityEngine;

/// <summary>
/// 球面移动控制器：将对象约束在球面上，WASD 输入转换为球面经纬度变化，只改变位置不改变朝向。
/// 可控制一个子物体始终朝向球心，并保持子物体与球心的距离不变。
/// </summary>
public class SphereMovement : MonoBehaviour
{
    [Header("球体配置")]
    public Vector3 sphereCenter = Vector3.zero;
    public Transform CneterObject = null;
    public float radius = 10f;

    [Header("移动参数")]
    public float moveSpeed = 8f;
    public float latitudeLimit = 89f;
    public bool invertLatitude = false;
    public bool invertLongitude = false;

    [Header("子物体朝向")]
    public Transform targetChild;
    public float childDistance = 0f;

    private float _longitude;
    private float _latitude;
    private float _latLimitRad;

    private void Start()
    {
        _latLimitRad = Mathf.Deg2Rad * latitudeLimit;
        SnapToSurface();
        SphericalCoordinates.FromCartesian(transform.position, CneterObject?CneterObject.position:sphereCenter,
            out _longitude, out _latitude, out _);

        if (targetChild != null && childDistance <= 0f)
            childDistance = Vector3.Distance(targetChild.position, GetEffectiveCenter());
    }

    private void Update()
    {
        UpdatePosition();
        UpdateChild();
    }

    private Vector3 GetEffectiveCenter()
    {
        return CneterObject != null ? CneterObject.position : sphereCenter;
    }

    /// <summary>
    /// 控制子物体始终朝向球心，并保持其到球心的初始距离不变。
    /// </summary>
    private void UpdateChild()
    {
        if (targetChild == null) return;

        Vector3 center = GetEffectiveCenter();
        Vector3 dirFromCenter = (transform.position - center).normalized;

        targetChild.position = center + dirFromCenter * childDistance;
        targetChild.rotation = Quaternion.LookRotation(-dirFromCenter);
    }

    /// <summary>
    /// 将 WASD 输入量转换为经纬度增量，更新对象在球面上的位置。
    /// 经度方向除以 cos(纬度) 以保持各处线速度一致。
    /// </summary>
    private void UpdatePosition()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");

        float angularSpeed = moveSpeed / radius;
        float cosLat = Mathf.Cos(_latitude);
        if (cosLat < 1e-4f) cosLat = 1e-4f;

        float lonSign = invertLongitude ? -1f : 1f;
        _longitude += inputX * lonSign * angularSpeed * Time.deltaTime / cosLat;
        float latSign = invertLatitude ? -1f : 1f;
        _latitude = Mathf.Clamp(_latitude + inputY * latSign * angularSpeed * Time.deltaTime, -_latLimitRad, _latLimitRad);

        transform.position = SphericalCoordinates.ToCartesian(_longitude, _latitude, radius, sphereCenter);
    }

    /// <summary>
    /// 将对象吸附到球面上：保持当前方向，距离归一化到 radius。
    /// </summary>
    private void SnapToSurface()
    {
        Vector3 rel = transform.position - sphereCenter;
        if (rel.magnitude < 1e-4f)
            rel = Vector3.forward;
        transform.position = sphereCenter + rel.normalized * radius;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(sphereCenter, radius);
    }

    private void OnValidate()
    {
        if (radius < 0.1f) radius = 0.1f;
    }
}
