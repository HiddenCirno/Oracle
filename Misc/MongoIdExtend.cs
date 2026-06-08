using EFT;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Oracle.Misc
{
    /// <summary>
    /// MongoId的拓展方法类
    /// </summary>
    public static class MongoIdExtend
    {
        //全局的sha256实例, 线程安全
        [ThreadStatic]
        private static SHA256 _sha256;
        //HEX预查表
        private static readonly char[] HexLookup = "0123456789abcdef".ToCharArray();
        /// <summary>
        /// MongoId的扩展方法, 基于原ID生成新的MongoId
        /// </summary>
        /// <param name="original">原始ID</param>
        /// <param name="salt">传入的加盐信息</param>
        /// <returns></returns>
        public static MongoID Regenerate(this MongoID original, string salt)
        {
            if (original == null) return original;
            string input = original.ToString() + (salt ?? string.Empty);
            if (_sha256 == null) _sha256 = SHA256.Create();
            //哈希计算
            byte[] hashBytes = _sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            //生成十六进制字符串
            char[] hexBuffer = new char[24];
            //截取字符串
            for (int i = 0; i < 12; i++)
            {
                byte b = hashBytes[i];
                //位运算拆分
                hexBuffer[i * 2] = HexLookup[b >> 4];
                hexBuffer[i * 2 + 1] = HexLookup[b & 0x0F];
            }
            //传入
            return new MongoID(new string(hexBuffer));
        }
    }
}