namespace Play.FieldContact.Model
{
    /// <summary>定义球面 Field 收集接触目标时使用的过滤范围。</summary>
    internal enum FieldContactTargetMode
    {
        /// <summary>收集所有符合物理层过滤条件的目标。</summary>
        All,

        /// <summary>只收集自身或根物体带有 Enemy 标签的目标。</summary>
        EnemyOnly
    }
}
