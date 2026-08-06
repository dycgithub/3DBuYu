namespace SpatialSystem.Data
{
    /// <summary>
    /// 网格中的一个单元格。
    /// 每个单元格容量上限为 32，防止无限循环和栈溢出。
    /// 未来迁移到 ECS 时可替换为 NativeParallelMultiHashMap。
    /// </summary>
    public struct SpatialCell
    {
        /// <summary>当前桶中的条目数量。</summary>
        public int Count;

        /// <summary>
        /// 固定大小的条目索引数组（指向全局 Entries 数组）。
        /// 硬上限 32 —— 如果超过，后续条目会被丢弃并记录警告。
        /// </summary>
        public const int MaxEntries = 32;

        /// <summary>条目索引的固定数组。</summary>
        public int Entry0, Entry1, Entry2, Entry3, Entry4, Entry5, Entry6, Entry7;
        public int Entry8, Entry9, Entry10, Entry11, Entry12, Entry13, Entry14, Entry15;
        public int Entry16, Entry17, Entry18, Entry19, Entry20, Entry21, Entry22, Entry23;
        public int Entry24, Entry25, Entry26, Entry27, Entry28, Entry29, Entry30, Entry31;

        /// <summary>获取指定索引处的条目。</summary>
        public int this[int index]
        {
            get
            {
                return index switch
                {
                    0 => Entry0, 1 => Entry1, 2 => Entry2, 3 => Entry3,
                    4 => Entry4, 5 => Entry5, 6 => Entry6, 7 => Entry7,
                    8 => Entry8, 9 => Entry9, 10 => Entry10, 11 => Entry11,
                    12 => Entry12, 13 => Entry13, 14 => Entry14, 15 => Entry15,
                    16 => Entry16, 17 => Entry17, 18 => Entry18, 19 => Entry19,
                    20 => Entry20, 21 => Entry21, 22 => Entry22, 23 => Entry23,
                    24 => Entry24, 25 => Entry25, 26 => Entry26, 27 => Entry27,
                    28 => Entry28, 29 => Entry29, 30 => Entry30, 31 => Entry31,
                    _ => -1
                };
            }
            set
            {
                switch (index)
                {
                    case 0: Entry0 = value; break; case 1: Entry1 = value; break;
                    case 2: Entry2 = value; break; case 3: Entry3 = value; break;
                    case 4: Entry4 = value; break; case 5: Entry5 = value; break;
                    case 6: Entry6 = value; break; case 7: Entry7 = value; break;
                    case 8: Entry8 = value; break; case 9: Entry9 = value; break;
                    case 10: Entry10 = value; break; case 11: Entry11 = value; break;
                    case 12: Entry12 = value; break; case 13: Entry13 = value; break;
                    case 14: Entry14 = value; break; case 15: Entry15 = value; break;
                    case 16: Entry16 = value; break; case 17: Entry17 = value; break;
                    case 18: Entry18 = value; break; case 19: Entry19 = value; break;
                    case 20: Entry20 = value; break; case 21: Entry21 = value; break;
                    case 22: Entry22 = value; break; case 23: Entry23 = value; break;
                    case 24: Entry24 = value; break; case 25: Entry25 = value; break;
                    case 26: Entry26 = value; break; case 27: Entry27 = value; break;
                    case 28: Entry28 = value; break; case 29: Entry29 = value; break;
                    case 30: Entry30 = value; break; case 31: Entry31 = value; break;
                }
            }
        }

        /// <summary>添加一个条目索引。如果桶已满则返回 false。</summary>
        public bool Add(int entryIndex)
        {
            if (Count >= MaxEntries) return false;
            this[Count] = entryIndex;
            Count++;
            return true;
        }

        /// <summary>移除指定索引处的条目（通过将最后一个条目移入来填充空隙）。</summary>
        public bool Remove(int entryIndex)
        {
            for (int i = 0; i < Count; i++)
            {
                if (this[i] == entryIndex)
                {
                    this[i] = this[Count - 1];
                    this[Count - 1] = -1;
                    Count--;
                    return true;
                }
            }
            return false;
        }

        /// <summary>清空桶。</summary>
        public void Clear()
        {
            for (int i = 0; i < Count; i++)
                this[i] = -1;
            Count = 0;
        }
    }
}
