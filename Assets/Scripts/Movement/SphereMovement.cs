using UnityEngine;

public class SphereMovement : MonoBehaviour
{
    [Header("目标设置")]
    [Tooltip("要在球面上移动的物体。如果为空，则使用挂载此脚本的物体。")]
    public Transform targetObject;

    [Header("球体设置")]
    [Tooltip("球心位置")]
    public Vector3 sphereCenter = Vector3.zero;

    [Tooltip("球半径")]
    [Range(1f, 100f)]
    public float sphereRadius = 5f;

    [Header("移动设置")]
    [Tooltip("移动速度")]
    public float moveSpeed = 5f;

    [Tooltip("是否使用平滑移动")]
    public bool useSmoothMovement = true;

    [Tooltip("平滑时间")]
    public float smoothTime = 0.1f;

    [Header("经纬线设置")]
    [Tooltip("纬度线条数")]
    [Range(2, 20)]
    public int latitudeLines = 8;

    [Tooltip("经度线条数")]
    [Range(4, 32)]
    public int longitudeLines = 16;

    [Tooltip("经纬线颜色")]
    public Color gridColor = new Color(0f, 1f, 1f, 0.5f);

    // 当前物体在球面上的位置（球坐标）
    private Vector2 _sphericalCoords;
    private Vector2 _targetSphericalCoords;
    private Vector2 _velocity;

    // 缓存Transform
    private Transform _transform;
    private Transform _targetTransform;

    // 当前物体在球面的实际位置
    public Vector3 CurrentPositionOnSphere { get; private set; }

    // 当前物体的方位角和仰角（弧度）
    public float CurrentLongitude => _sphericalCoords.x;
    public float CurrentLatitude => _sphericalCoords.y;

    // 实际移动的目标物体
    public Transform MovingObject => _targetTransform ?? _transform;

    private void Awake()
    {
        _transform = GetComponent<Transform>();
        _targetTransform = targetObject;
        InitializePosition();
    }

    private void OnValidate()
    {
        // 在编辑器中实时更新目标引用
        if (Application.isPlaying == false && targetObject != null)
        {
            _targetTransform = targetObject;
        }
    }

    private void InitializePosition()
    {
        Transform target = MovingObject;

        // 计算当前物体位置相对于球心的球坐标
        Vector3 relativePos = target.position - sphereCenter;
        CurrentPositionOnSphere = relativePos.normalized * sphereRadius + sphereCenter;

        // 转换为球坐标 (x = 经度, y = 纬度)
        _sphericalCoords = CartesianToSpherical(relativePos);
        _targetSphericalCoords = _sphericalCoords;
    }

    private void Update()
    {
        HandleInput();
        UpdatePosition();
    }

    private void HandleInput()
    {
        float horizontal = Input.GetAxis("Horizontal"); // 左右移动
        float vertical = Input.GetAxis("Vertical");     // 上下移动

        if (horizontal != 0f || vertical != 0f)
        {
            // 经度方向移动（左右）- 沿纬度圈
            float longitudeDelta = horizontal * moveSpeed * Time.deltaTime;

            // 纬度方向移动（上下）- 沿经度线
            float latitudeDelta = vertical * moveSpeed * Time.deltaTime;

            _targetSphericalCoords.x += longitudeDelta;
            _targetSphericalCoords.y += latitudeDelta;

            // 限制纬度范围 (-90度 到 90度，转换为弧度)
            float maxLat = Mathf.PI / 2f - 0.01f;
            _targetSphericalCoords.y = Mathf.Clamp(_targetSphericalCoords.y, -maxLat, maxLat);
        }
    }

    private void UpdatePosition()
    {
        Transform target = MovingObject;

        if (useSmoothMovement)
        {
            // 使用平滑移动到目标球坐标
            _sphericalCoords.x = Mathf.SmoothDamp(_sphericalCoords.x, _targetSphericalCoords.x,
                ref _velocity.x, smoothTime);
            _sphericalCoords.y = Mathf.SmoothDamp(_sphericalCoords.y, _targetSphericalCoords.y,
                ref _velocity.y, smoothTime);
        }
        else
        {
            _sphericalCoords = _targetSphericalCoords;
        }

        // 将球坐标转换回笛卡尔坐标
        Vector3 relativePos = SphericalToCartesian(_sphericalCoords);
        CurrentPositionOnSphere = sphereCenter + relativePos;

        // 更新物体位置和朝向
        target.position = CurrentPositionOnSphere;

        // 让物体面朝移动方向
        Vector3 forward = GetForwardDirection();
        if (forward.sqrMagnitude > 0.001f)
        {
            target.rotation = Quaternion.LookRotation(forward);
        }
    }

    /// <summary>
    /// 获取物体前进方向（切线方向）
    /// </summary>
    public Vector3 GetForwardDirection()
    {
        Vector3 rightDir = GetLongitudeTangent();
        Vector3 upDir = GetLatitudeTangent();

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        return (rightDir * horizontal + upDir * vertical).normalized;
    }

    /// <summary>
    /// 获取经线方向（南北方向）
    /// </summary>
    public Vector3 GetLongitudeTangent()
    {
        Vector3 pos = MovingObject.position - sphereCenter;
        Vector3 north = Vector3.up;
        return Vector3.Cross(pos, north).normalized;
    }

    /// <summary>
    /// 获取纬线方向（东西方向）
    /// </summary>
    public Vector3 GetLatitudeTangent()
    {
        Vector3 pos = MovingObject.position - sphereCenter;
        Vector3 north = Vector3.up;
        Vector3 eastDir = Vector3.Cross(north, pos).normalized;
        return eastDir;
    }

    /// <summary>
    /// 笛卡尔坐标转球坐标
    /// </summary>
    public static Vector2 CartesianToSpherical(Vector3 cartesian)
    {
        float radius = cartesian.magnitude;
        float longitude = Mathf.Atan2(cartesian.x, cartesian.z); // 经度
        float latitude = Mathf.Asin(cartesian.y / radius);      // 纬度
        return new Vector2(longitude, latitude);
    }

    /// <summary>
    /// 球坐标转笛卡尔坐标
    /// </summary>
    public static Vector3 SphericalToCartesian(Vector2 spherical)
    {
        float longitude = spherical.x;
        float latitude = spherical.y;

        float x = Mathf.Cos(latitude) * Mathf.Sin(longitude);
        float y = Mathf.Sin(latitude);
        float z = Mathf.Cos(latitude) * Mathf.Cos(longitude);

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// 直接设置物体到球面上的指定位置
    /// </summary>
    public void SetPositionOnSphere(float longitude, float latitude)
    {
        _targetSphericalCoords.x = longitude;
        _targetSphericalCoords.y = Mathf.Clamp(latitude, -Mathf.PI / 2f + 0.01f, Mathf.PI / 2f - 0.01f);
    }

    private void OnDrawGizmos()
    {
        DrawLatitudeLines();
        DrawLongitudeLines();
        DrawSphereCenter();
    }

    private void DrawSphereCenter()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(sphereCenter, 0.2f);
    }

    private void DrawLatitudeLines()
    {
        Gizmos.color = gridColor;
        for (int i = 0; i <= latitudeLines; i++)
        {
            float lat = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, (float)i / latitudeLines);
            DrawLatitudeLine(lat);
        }
    }

    private void DrawLatitudeLine(float latitude)
    {
        Vector3 prevPoint = Vector3.zero;
        bool firstPoint = true;

        int segments = longitudeLines * 4;

        for (int i = 0; i <= segments; i++)
        {
            float lon = Mathf.Lerp(-Mathf.PI, Mathf.PI, (float)i / segments);
            Vector3 localPos = SphericalToCartesian(new Vector2(lon, latitude));
            Vector3 worldPos = sphereCenter + localPos * sphereRadius;

            if (!firstPoint)
            {
                Gizmos.DrawLine(prevPoint, worldPos);
            }

            prevPoint = worldPos;
            firstPoint = false;
        }
    }

    private void DrawLongitudeLines()
    {
        Gizmos.color = gridColor;
        for (int i = 0; i <= longitudeLines; i++)
        {
            float lon = Mathf.Lerp(-Mathf.PI, Mathf.PI, (float)i / longitudeLines);
            DrawLongitudeLine(lon);
        }
    }

    private void DrawLongitudeLine(float longitude)
    {
        Vector3 prevPoint = Vector3.zero;
        bool firstPoint = true;

        int segments = latitudeLines * 4;

        for (int i = 0; i <= segments; i++)
        {
            float lat = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, (float)i / segments);
            Vector3 localPos = SphericalToCartesian(new Vector2(longitude, lat));
            Vector3 worldPos = sphereCenter + localPos * sphereRadius;

            if (!firstPoint)
            {
                Gizmos.DrawLine(prevPoint, worldPos);
            }

            prevPoint = worldPos;
            firstPoint = false;
        }
    }

    /// <summary>
    /// 在编辑器中绘制经纬线（支持OnDrawGizmosSelected）
    /// </summary>
    public void DrawDebugLines()
    {
        OnDrawGizmos();
    }
}
