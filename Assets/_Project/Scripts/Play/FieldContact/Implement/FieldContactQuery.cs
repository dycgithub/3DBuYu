using System;
using Play.FieldContact.Model;
using UnityEngine;

namespace Play.FieldContact.Implement
{
    /// <summary>用物理空间查询球面附近的 Mesh Collider，并返回最近接触点。</summary>
    internal sealed class FieldContactQuery
    {
        internal const int ContactCapacity = 4;

        private const int InitialColliderCapacity = 128;
        private const float MinimumDirectionSqrMagnitude = 0.000001f;
        private const string EnemyTag = "Enemy";

        private Collider[] _colliders = new Collider[InitialColliderCapacity];

        /// <summary>收集范围内最近的唯一 Mesh Renderer。</summary>
        /// <param name="fieldTransform">球面 Field 的 Transform。</param>
        /// <param name="fieldCollider">Field 的球形 Collider。</param>
        /// <param name="targetLayers">允许参与接触查询的物理层。</param>
        /// <param name="targetMode">接触目标过滤模式。</param>
        /// <param name="revealDistance">球面外的最大显现距离。</param>
        /// <param name="contacts">接收接触样本的预分配缓冲区。</param>
        /// <returns>缓冲区内的接触样本数。</returns>
        internal int Collect(
            Transform fieldTransform,
            SphereCollider fieldCollider,
            LayerMask targetLayers,
            FieldContactTargetMode targetMode,
            float revealDistance,
            FieldContactSample[] contacts)
        {
            Array.Clear(contacts, 0, contacts.Length);

            if (fieldTransform == null || fieldCollider == null || revealDistance <= 0f)
            {
                return 0;
            }

            Vector3 fieldCenter = fieldCollider.transform.TransformPoint(fieldCollider.center);
            float fieldRadius = GetWorldRadius(fieldCollider);
            int hitCount = QueryColliders(
                fieldCenter,
                fieldRadius + revealDistance,
                targetLayers);
            int contactCount = 0;

            for (int index = 0; index < hitCount; index++)
            {
                Collider collider = _colliders[index];
                if (collider == null || collider.transform.root == fieldTransform.root)
                {
                    continue;
                }

                Renderer sourceRenderer = FindMeshRenderer(collider);
                // ECS Flocking disables the source Renderer while its Collider remains a valid proxy.
                if (sourceRenderer == null ||
                    !sourceRenderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (targetMode == FieldContactTargetMode.EnemyOnly &&
                    !IsEnemyTarget(collider))
                {
                    continue;
                }

                Vector3 closestPoint = collider.ClosestPoint(fieldCenter);
                if ((closestPoint - fieldCenter).sqrMagnitude < MinimumDirectionSqrMagnitude)
                {
                    closestPoint = FieldContactMask.GetClosestBoundsSurfacePoint(
                        sourceRenderer.bounds,
                        fieldCenter);
                }

                float surfaceGap = FieldContactMask.GetSurfaceGap(
                    fieldCenter,
                    fieldRadius,
                    closestPoint);
                if (surfaceGap >= revealDistance)
                {
                    continue;
                }

                Vector3 direction = closestPoint - fieldCenter;
                if (direction.sqrMagnitude < MinimumDirectionSqrMagnitude)
                {
                    direction = sourceRenderer.bounds.center - fieldCenter;
                }

                if (direction.sqrMagnitude < MinimumDirectionSqrMagnitude)
                {
                    direction = Vector3.up;
                }

                Vector3 localDirection = fieldTransform.InverseTransformDirection(direction.normalized);
                FieldContactSample sample = new(
                    localDirection.normalized,
                    surfaceGap,
                    FieldContactMask.GetRevealStrength(surfaceGap, revealDistance),
                    sourceRenderer.GetInstanceID());

                contactCount = InsertNearest(sample, contacts, contactCount);
            }

            return contactCount;
        }

        /// <summary>获取球面与检测范围相交的 Collider。</summary>
        /// <param name="center">球面中心。</param>
        /// <param name="radius">球面半径加显现距离。</param>
        /// <param name="layers">候选物理层。</param>
        /// <returns>写入缓冲区的 Collider 数量。</returns>
        private int QueryColliders(Vector3 center, float radius, LayerMask layers)
        {
            while (true)
            {
                int count = Physics.OverlapSphereNonAlloc(
                    center,
                    radius,
                    _colliders,
                    layers,
                    QueryTriggerInteraction.Collide);

                if (count < _colliders.Length)
                {
                    return count;
                }

                Array.Resize(ref _colliders, _colliders.Length * 2);
            }
        }

        /// <summary>判断碰撞体自身或根物体是否带有 Enemy 标签。</summary>
        /// <param name="collider">候选目标碰撞体。</param>
        /// <returns>目标属于 Enemy 时返回 true。</returns>
        private static bool IsEnemyTarget(Collider collider)
        {
            return collider.CompareTag(EnemyTag) ||
                   collider.transform.root.CompareTag(EnemyTag);
        }

        /// <summary>从 Collider 层级中取得 MeshRenderer 或 SkinnedMeshRenderer。</summary>
        /// <param name="collider">物理查询命中的 Collider。</param>
        /// <returns>关联网格渲染器；没有渲染网格时返回 null。</returns>
        private static Renderer FindMeshRenderer(Collider collider)
        {
            Transform current = collider.transform;
            while (current != null)
            {
                MeshRenderer meshRenderer = current.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    return meshRenderer;
                }

                SkinnedMeshRenderer skinnedRenderer = current.GetComponent<SkinnedMeshRenderer>();
                if (skinnedRenderer != null)
                {
                    return skinnedRenderer;
                }

                current = current.parent;
            }

            Transform searchRoot = collider.attachedRigidbody != null
                ? collider.attachedRigidbody.transform
                : collider.transform.root;
            MeshRenderer childMeshRenderer = searchRoot.GetComponentInChildren<MeshRenderer>(true);
            return childMeshRenderer != null
                ? childMeshRenderer
                : searchRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }

        /// <summary>将更近的接触样本插入有界排序数组。</summary>
        /// <param name="sample">候选接触样本。</param>
        /// <param name="contacts">按表面间距从小到大排序的缓冲区。</param>
        /// <param name="count">当前样本数量。</param>
        /// <returns>更新后的样本数量。</returns>
        private static int InsertNearest(
            FieldContactSample sample,
            FieldContactSample[] contacts,
            int count)
        {
            for (int index = 0; index < count; index++)
            {
                if (contacts[index].SourceId != sample.SourceId)
                {
                    continue;
                }

                if (contacts[index].SurfaceGap <= sample.SurfaceGap)
                {
                    return count;
                }

                for (int move = index; move < count - 1; move++)
                {
                    contacts[move] = contacts[move + 1];
                }

                count--;
                break;
            }

            int insertIndex = 0;
            while (insertIndex < count && contacts[insertIndex].SurfaceGap <= sample.SurfaceGap)
            {
                insertIndex++;
            }

            if (insertIndex >= contacts.Length)
            {
                return count;
            }

            int newCount = Mathf.Min(count + 1, contacts.Length);
            for (int move = newCount - 1; move > insertIndex; move--)
            {
                contacts[move] = contacts[move - 1];
            }

            contacts[insertIndex] = sample;
            return newCount;
        }

        /// <summary>计算经过 Transform 缩放后的球形 Collider 半径。</summary>
        /// <param name="collider">Field 的球形 Collider。</param>
        /// <returns>世界空间的保守球体半径。</returns>
        private static float GetWorldRadius(SphereCollider collider)
        {
            Vector3 scale = collider.transform.lossyScale;
            return collider.radius * Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z));
        }
    }
}
