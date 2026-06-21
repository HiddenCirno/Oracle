using System;
using System.Collections.Generic;
using System.Text;

namespace Oracle.Data
{
    internal class OracleEvent
    {
        public static Action OnUpdate;
        public static Action OnKeyUpdate;
        public static Action OnDrawManagerGUI;
        public static Action OnDrawESP;
        public static Action OnDrawAimbot;
        public static Action OnDrawCrosshair;
        public static void Draw()
        {
            OnDrawESP?.Invoke();
            OnDrawAimbot?.Invoke();
        }
        public static void DrawCrosshair()
        {
            OnDrawCrosshair?.Invoke();
        }
        public static void DrawManagerGUI()
        {
            OnDrawManagerGUI?.Invoke();
        }
        public static void Update()
        {
            OnUpdate?.Invoke();
            OnKeyUpdate?.Invoke();
        }
    }
}
