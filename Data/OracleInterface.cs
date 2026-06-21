using BepInEx.Configuration;

namespace Oracle.Data
{
    internal class OracleInterface
    {
        /// <summary>
        /// 通用配置接口
        /// 所有继承此接口的类将在启动时被自动扫描并注册
        /// </summary>
        public interface IOracleCfg
        {
            void Initialize(ConfigFile config);
        }
        /// <summary>
        /// 通用快捷键监听接口
        /// </summary>
        public interface IOracleKeyUpdate
        {
            void RegisterKeyUpdate();
        }
        /// <summary>
        /// 事件订阅接口类
        /// </summary>
        public interface IOracleEventSubscribe
        {
            void SubscribeEvent();
        }
        /// <summary>
        /// ManagerGUI使用的订阅接口
        /// </summary>
        public interface IOracleManagerGUI : IOracleEventSubscribe
        {
        }
        /// <summary>
        /// ESP使用的订阅接口
        /// </summary>
        public interface IOracleESP : IOracleEventSubscribe
        {
        }
        /// <summary>
        /// 准星使用的订阅接口
        /// </summary>
        public interface IOracleCrosshair : IOracleEventSubscribe
        {
        }
        /// <summary>
        /// 自瞄使用的订阅接口
        /// </summary>
        public interface IOracleAimbot : IOracleEventSubscribe
        {
        }
    }
}
