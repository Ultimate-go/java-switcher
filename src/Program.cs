using System;
using System.Windows.Forms;

namespace JavaSwitcher
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // 管理员提权后由子进程执行静默切换：JavaSwitcher.exe --switch "<jdk home>"
            if (args != null && args.Length == 2 && string.Equals(args[0], "--switch", StringComparison.OrdinalIgnoreCase))
            {
                return RunSilentSwitch(args[1]);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            return RunGui();
        }

        private static int RunGui()
        {
            string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JavaSwitcher.error.log");
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                WriteLog(logPath, e.Exception);
                MessageBox.Show("发生错误：\n" + e.Exception.Message + "\n\n详细日志：" + logPath, "Java 环境切换器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                if (ex != null) WriteLog(logPath, ex);
            };

            try
            {
                Application.Run(new MainForm());
                return 0;
            }
            catch (Exception ex)
            {
                WriteLog(logPath, ex);
                MessageBox.Show("启动失败：\n" + ex.Message + "\n\n详细日志：" + logPath, "Java 环境切换器", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static void WriteLog(string logPath, Exception ex)
        {
            try
            {
                System.IO.File.WriteAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + ex);
            }
            catch
            {
            }
        }

        private static int RunSilentSwitch(string home)
        {
            if (string.IsNullOrWhiteSpace(home))
            {
                MessageBox.Show("切换路径为空。", "Java 环境切换器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }

            string javaExe = System.IO.Path.Combine(home, "bin", "java.exe");
            if (!System.IO.File.Exists(javaExe))
            {
                MessageBox.Show("目标目录不是有效的 Java 安装：\n" + home, "Java 环境切换器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }

            try
            {
                System.Collections.Generic.List<string> notes = EnvironmentService.PerformSwitch(home);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < notes.Count; i++)
                {
                    if (i > 0) sb.Append("\r\n");
                    sb.Append("· ").Append(notes[i]);
                }
                sb.Append("\r\n\r\n新开的终端或 IDE 会话生效。");
                MessageBox.Show(sb.ToString(), "Java 环境切换完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("切换失败：\n" + ex.Message, "Java 环境切换器", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
