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
        /// ManagerGUI使用的订阅接口
        /// </summary>
        public interface IOracleManagerGUI
        {
            void SubscribeEvent();
        }
    }
}
