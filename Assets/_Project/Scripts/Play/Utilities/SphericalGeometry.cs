using UnityEngine;
using Interfaces;

/// <summary>
/// 球面几何工具类。提供球面法线、切向、锥形检测等静态方法。
/// 端口级瞄准逻辑在 TurretPortRuntime.AimAt() 中。
/// </summary>
public class SphericalGeometry
{
    private readonly Transform turretBase;
    private readonly Transform firePoint;
    private readonly Transform sphereCenterTransform;

    public SphericalGeometry(
        Transform turretBase,
        Transform firePoint,
        Transform sphereCenterTransform)
    {
        this.turretBase = turretBase;
        this.firePoint = firePoint;
        this.sphereCenterTransform = sphereCenterTransform;
    }

    public Vector3 GetSurfaceNormal()
    {
        if (sphereCenterTransform == null) return Vector3.up;
        return (turretBase.position - sphereCenterTransform.position).normalized;
    }

    public Vector3 GetDefaultDirection() => -GetSurfaceNormal();

    public Quaternion GetDefaultRotation()
    {
        Vector3 normal = GetSurfaceNormal();
        return Quaternion.LookRotation(-normal, normal);
    }

    public bool IsTargetInCone(IDamageable target, float coneAngle = 90f)
    {
        if (target == null || !target.IsAlive) return false;
        Vector3 origin = firePoint != null ? firePoint.position : turretBase.position;
        Vector3 coneDir = GetDefaultDirection();
        Vector3 toTarget = (target.Position - origin).normalized;
        return Vector3.Angle(coneDir, toTarget) <= coneAngle;
    }

    public Vector3 GetConeDirection()
    {
        Vector3 origin = firePoint != null ? firePoint.position : turretBase.position;
        Vector3 centerPos = sphereCenterTransform != null ? sphereCenterTransform.position : Vector3.zero;
        return (centerPos - origin).normalized;
    }

    public Vector3 GetConeOrigin()
    {
        return firePoint != null ? firePoint.position : turretBase.position;
    }
}