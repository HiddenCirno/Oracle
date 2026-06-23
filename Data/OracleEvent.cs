using System;

namespace Oracle.Data
{
    /// <summary>
    /// 事件管理器
    /// </summary>
    internal class OracleEvent
    {
        //事件定义
        //我要给这几个玩意儿写注释么?
        //要得要得

        /// <summary>
        /// 更新事件
        /// </summary>
        public static Action OnUpdate;

        /// <summary>
        /// 按键监听事件
        /// </summary>
        public static Action OnKeyUpdate;

        /// <summary>
        /// 管理面板绘制事件
        /// </summary>
        public static Action OnDrawManagerGUI;

        /// <summary>
        /// 透视绘制事件
        /// </summary>
        public static Action OnDrawESP;

        /// <summary>
        /// 自瞄绘制事件
        /// </summary>
        public static Action OnDrawAimbot;

        /// <summary>
        /// 准星绘制事件
        /// </summary>
        public static Action OnDrawCrosshair;

        /// <summary>
        /// 绘制事件执行包装
        /// </summary>
        public static void Draw()
        {
            OnDrawESP?.Invoke();
            OnDrawAimbot?.Invoke();
        }

        /// <summary>
        /// 准星绘制事件执行包装
        /// </summary>
        public static void DrawCrosshair()
        {
            OnDrawCrosshair?.Invoke();
        }

        /// <summary>
        /// 管理面板绘制事件执行包装
        /// </summary>
        public static void DrawManagerGUI()
        {
            OnDrawManagerGUI?.Invoke();
        }

        /// <summary>
        /// 更新事件执行包装
        /// </summary>
        public static void Update()
        {
            OnUpdate?.Invoke();
            OnKeyUpdate?.Invoke();
        }
    }
}
