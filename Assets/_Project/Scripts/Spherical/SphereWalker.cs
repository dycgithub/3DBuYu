using UnityEngine;
using UnityEngine.Serialization;

public class SphereWalker : MonoBehaviour
{
    [Header("Sphere 设置")]
    public Vector3 sphereCenter = Vector3.zero;
    public Transform sphereCenterPosObj;
    public float radius = 10f;
    public float latitudeLimit = 89f;

    [Header("Camera 跟随")]
    public Transform cameraFollowPos;
    public float pivotDistance = 0f;

    public bool showGizmos;

    public float longitude { get; private set; }//经度
    public float latitude { get; private set; }//纬度

    private float _latitudeLimitRad;//最大纬度限制

    private void Start()
    {
        _latitudeLimitRad = Mathf.Deg2Rad * latitudeLimit;
        SnapToSurface();
        SphericalCoordinates.FromCartesian(transform.position, GetEffectiveCenter(),
            out float lon, out float lat, out _);
        longitude = lon;
        latitude = lat;

        if (cameraFollowPos != null && pivotDistance <= 0f)
            pivotDistance = Vector3.Distance(cameraFollowPos.position, GetEffectiveCenter());
    }

    private void Update()
    {
        UpdateCameraPivot();
    }

    public Vector3 GetEffectiveCenter()
    {
        return sphereCenterPosObj != null ? sphereCenterPosObj.position : sphereCenter;
    }

    public void Move(float deltaLongitude, float deltaLatitude)
    {
        longitude += deltaLongitude;
        latitude = Mathf.Clamp(latitude + deltaLatitude, -_latitudeLimitRad, _latitudeLimitRad);
        transform.position = SphericalCoordinates.ToCartesian(longitude, latitude, radius, GetEffectiveCenter());
    }

    public void SnapToSurface()
    {
        Vector3 offsetFromCenter = transform.position - GetEffectiveCenter();
        if (offsetFromCenter.magnitude < 1e-4f)
            offsetFromCenter = Vector3.forward;
        transform.position = GetEffectiveCenter() + offsetFromCenter.normalized * radius;
    }

    private void UpdateCameraPivot()
    {
        if (cameraFollowPos == null) return;

        Vector3 center = GetEffectiveCenter();
        Vector3 dirFromCenter = (transform.position - center).normalized;

        cameraFollowPos.position = center + dirFromCenter * pivotDistance;
        cameraFollowPos.rotation = Quaternion.LookRotation(-dirFromCenter);
    }

    private void OnDrawGizmosSelected()
    {
        if (showGizmos)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(sphereCenter, radius);
        }
    }

    private void OnValidate()
    {
        if (radius < 0.1f) radius = 0.1f;
    }
}
