using System;
using System.Windows.Forms;
using System.Threading;

namespace SmoothRoller
{
    internal static class Program
    {
        private static Mutex mutex = null;
        
        [STAThread]
        static void Main()
        {
            // 确保只有一个实例运行
            const string appName = "SmoothRollerApp";
            bool createdNew;
            
            mutex = new Mutex(true, appName, out createdNew);
            
            if (!createdNew)
            {
                MessageBox.Show("SmoothRoller 已经在运行中！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 启动主应用程序
            var app = new SmoothRollerApp();
            Application.Run(app);
            
            mutex?.ReleaseMutex();
        }
    }
}