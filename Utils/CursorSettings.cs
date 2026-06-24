using EFT.UI;
using SPT.Reflection.Utils;
using System.Linq;
using System.Reflection;

namespace Oracle.Utils
{
    /// <summary>
    /// 调用塔科夫的鼠标样式
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