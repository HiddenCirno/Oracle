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
            setCursorMethod = PatchConstants.EftTypes
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                .FirstOrDefault(m =>
                    m.Name == "SetCursor" &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(ECursorType)
                );
        }

        public static void SetCursor(ECursorType type)
        {
            setCursorMethod?.Invoke(null, new object[] { type });
        }
    }
}