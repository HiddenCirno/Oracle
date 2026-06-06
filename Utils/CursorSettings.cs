using System.Linq;
using System.Reflection;
using EFT.UI;
using SPT.Reflection.Utils; // 确保你的 SPT 版本引用了这个命名空间

namespace Oracle.Utils
{
    /// <summary>
    /// 用于调用塔科夫底层鼠标样式的反射工具类
    /// </summary>
    public static class CursorSettings
    {
        private static readonly MethodInfo setCursorMethod;

        static CursorSettings()
        {
            // 遍历塔科夫底层所有类型，精准狙击包含 "SetCursor" 方法的类
            var cursorType = PatchConstants.EftTypes.Single(x => x.GetMethod("SetCursor") != null);
            setCursorMethod = cursorType.GetMethod("SetCursor");
        }

        public static void SetCursor(ECursorType type)
        {
            setCursorMethod?.Invoke(null, new object[] { type });
        }
    }
}