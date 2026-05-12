using EFT;
using EFT.InventoryLogic;
using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Oracle.Utils
{

    public static class MongoIdExtend
    {
        // 1. 复用 SHA256 实例，避免每次调用都去底层申请非托管内存
        // 使用 ThreadStatic 防止极小概率的异步多线程冲突
        [ThreadStatic]
        private static SHA256 _sha256;

        // 2. 预查表：用于最高效的 Byte 转 Hex 字符
        private static readonly char[] HexLookup = "0123456789abcdef".ToCharArray();

        public static MongoID Regenerate(this MongoID original, string salt)
        {
            if (original == null) return original; // 或者保留你的抛出异常逻辑

            string input = original.ToString() + (salt ?? string.Empty);

            if (_sha256 == null) _sha256 = SHA256.Create();

            // 计算哈希
            byte[] hashBytes = _sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

            // 3. 核心优化：完全抛弃 StringBuilder 和 BitConverter
            // 直接在内存中组装 24 位的 char 数组，实现“零额外字符串碎片”分配
            char[] hexBuffer = new char[24];

            // 我们只需要 MongoID 的前 12 字节
            for (int i = 0; i < 12; i++)
            {
                byte b = hashBytes[i];
                // 通过位运算直接将 Byte 拆成两个 Hex 字符
                hexBuffer[i * 2] = HexLookup[b >> 4];       // 高 4 位
                hexBuffer[i * 2 + 1] = HexLookup[b & 0x0F]; // 低 4 位
            }

            // 直接将字符数组转换为最终的 string，传入 MongoID
            return new MongoID(new string(hexBuffer));
        }
    }
}