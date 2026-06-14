using EFT.UI;
using SPT.Reflection.Utils;
using System.Linq;
using System.Reflection;

namespace Oracle.Tools
{
    /// <summary>
    /// 用于调用塔科夫底层鼠标样式的反射工具类
    /// </summary>
    public static class CursorSettings
    {
        private static readonly MethodInfo setCursorMethod;

        static CursorSettings()
        {
            var cursorType = PatchConstants.EftTypes.Single(x => x.GetMethod("SetCursor") != null);
            setCursorMethod = cursorType.GetMethod("SetCursor");
        }

        public static void SetCursor(ECursorType type)
        {
            setCursorMethod?.Invoke(null, new object[] { type });
        }
    }
}