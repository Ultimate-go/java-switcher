using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace JavaSwitcher
{
    /// <summary>
    /// 读写 Windows 注册表中的环境变量（JAVA_HOME / Path），并广播系统消息使新会话生效。
    /// 仅触碰 Java 相关条目，不修改其余任何环境配置。
    /// </summary>
    public static class EnvironmentService
    {
        private const string MachineEnvPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
        private const string UserEnvPath = "Environment";

        private const int HWND_BROADCAST = 0xFFFF;
        private const int WM_SETTINGCHANGE = 0x001A;
        private const int SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam, string lParam, int fuFlags, int uTimeout, out IntPtr lpdwResult);

        /// <summary>当前进程是否拥有管理员权限。</summary>
        public static bool IsAdministrator()
        {
            try
            {
                System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static RegistryKey OpenMachine(bool writable)
        {
            return Registry.LocalMachine.OpenSubKey(MachineEnvPath, writable);
        }

        private static RegistryKey OpenUser(bool writable)
        {
            return Registry.CurrentUser.OpenSubKey(UserEnvPath, writable);
        }

        private static string ReadValue(RegistryKey key, string name)
        {
            try
            {
                if (key == null) return null;
                object value = key.GetValue(name);
                return value == null ? null : value.ToString();
            }
            catch
            {
                return null;
            }
        }

        public static string GetMachineValue(string name)
        {
            using (RegistryKey key = OpenMachine(false))
            {
                return ReadValue(key, name);
            }
        }

        public static string GetUserValue(string name)
        {
            using (RegistryKey key = OpenUser(false))
            {
                return ReadValue(key, name);
            }
        }

        private static void WriteMachineValue(string name, string value, RegistryValueKind kind)
        {
            using (RegistryKey key = OpenMachine(true))
            {
                if (key == null) throw new UnauthorizedAccessException("无法打开系统环境变量配置，需要管理员权限。");
                key.SetValue(name, value, kind);
            }
        }

        private static void WriteUserValue(string name, string value, RegistryValueKind kind)
        {
            using (RegistryKey key = OpenUser(true))
            {
                if (key == null) throw new UnauthorizedAccessException("无法打开用户环境变量配置。");
                key.SetValue(name, value, kind);
            }
        }

        public static string MachineJavaHome
        {
            get { return GetMachineValue("JAVA_HOME"); }
        }

        public static string UserJavaHome
        {
            get { return GetUserValue("JAVA_HOME"); }
        }

        /// <summary>新会话实际生效的 JAVA_HOME（用户级覆盖系统级）。</summary>
        public static string EffectiveJavaHome()
        {
            string userHome = UserJavaHome;
            if (!string.IsNullOrWhiteSpace(userHome)) return userHome.Trim();
            string machineHome = MachineJavaHome;
            if (!string.IsNullOrWhiteSpace(machineHome)) return machineHome.Trim();
            return null;
        }

        public static string GetMachinePath()
        {
            return GetMachineValue("Path");
        }

        public static string GetUserPath()
        {
            return GetUserValue("Path");
        }

        /// <summary>按分号拆分 PATH，去掉空白与两端引号。</summary>
        public static List<string> SplitPath(string raw)
        {
            List<string> result = new List<string>();
            if (raw == null) return result;
            string[] parts = raw.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim().Trim('"');
                if (part.Length > 0) result.Add(part);
            }
            return result;
        }

        /// <summary>系统级环境是否已包含 Java 配置（说明切换需同步到系统级，需要管理员）。</summary>
        public static bool MachineHasJavaConfig()
        {
            string home = MachineJavaHome;
            if (!string.IsNullOrWhiteSpace(home)) return true;
            string machinePath = GetMachinePath();
            if (machinePath != null)
            {
                List<string> parts = SplitPath(machinePath);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (IsJavaPathEntry(parts[i])) return true;
                }
            }
            return false;
        }

        /// <summary>判断某条 PATH 是否为 Java 相关（旧 JDK/JRE bin、javapath、%JAVA_HOME%\bin 等）。</summary>
        public static bool IsJavaPathEntry(string rawPart)
        {
            string part = rawPart == null ? "" : rawPart.Trim();
            if (part.Length == 0) return false;
            string lower = part.ToLowerInvariant();
            if (lower.IndexOf("%java_home%", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (lower.IndexOf("javapath", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (lower.IndexOf("java8path", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            string expanded = ExpandForCheck(part);
            if (expanded.Length == 0) return false;
            try
            {
                if (File.Exists(Path.Combine(expanded, "java.exe"))) return true;
                if (File.Exists(Path.Combine(expanded, "bin", "java.exe"))) return true;
            }
            catch
            {
            }

            // 目录已失效的历史残留也视为 Java 条目：
            // 形如 "...\<jdk 或 jre 开头目录>\bin"，例如 C:\...\jdk-17.0.11.9-hotspot\bin
            string pathShape = part.Trim().TrimEnd('\\').ToLowerInvariant();
            if (pathShape.EndsWith("\\bin"))
            {
                int binIndex = pathShape.LastIndexOf("\\bin");
                if (binIndex > 0)
                {
                    int segmentStart = pathShape.LastIndexOf('\\', binIndex - 1);
                    string segment = segmentStart >= 0
                        ? pathShape.Substring(segmentStart + 1, binIndex - segmentStart - 1)
                        : pathShape.Substring(0, binIndex);
                    if (segment.StartsWith("jdk") || segment.StartsWith("jre")) return true;
                }
            }
            return false;
        }

        /// <summary>把 %JAVA_HOME% 等变量展开成实际路径（用于文件存在性判断）。</summary>
        public static string ExpandForCheck(string value)
        {
            if (value == null) return "";
            string text = value;
            string home = EffectiveJavaHome();
            if (!string.IsNullOrEmpty(home))
            {
                text = text.Replace("%JAVA_HOME%", home);
                text = text.Replace("%java_home%", home);
            }
            try
            {
                text = Environment.ExpandEnvironmentVariables(text);
            }
            catch
            {
            }
            return text;
        }

        /// <summary>重建 PATH：把 %JAVA_HOME%\bin 放到最前，并移除旧 Java 相关条目，其余原样保留。</summary>
        public static string RebuildPath(string rawPath)
        {
            List<string> result = new List<string>();
            result.Add("%JAVA_HOME%\\bin");
            if (rawPath != null)
            {
                List<string> parts = SplitPath(rawPath);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (IsJavaPathEntry(parts[i])) continue;
                    result.Add(parts[i]);
                }
            }
            return string.Join(";", result.ToArray());
        }

        /// <summary>
        /// 执行一次切换：始终更新用户级环境；若系统级本就含 Java 配置且当前是管理员，
        /// 则同时同步系统级环境。随后广播消息，让新开的终端 / IDE 生效。
        /// </summary>
        public static List<string> PerformSwitch(string home)
        {
            List<string> notes = new List<string>();
            bool isAdmin = IsAdministrator();
            bool machineManaged = MachineHasJavaConfig();
            string target = home == null ? "" : home.Trim();

            WriteUserValue("JAVA_HOME", target, RegistryValueKind.String);
            WriteUserValue("Path", RebuildPath(GetUserPath()), RegistryValueKind.ExpandString);
            notes.Add("用户环境 JAVA_HOME / Path 已更新");

            if (machineManaged)
            {
                if (isAdmin)
                {
                    WriteMachineValue("JAVA_HOME", target, RegistryValueKind.String);
                    WriteMachineValue("Path", RebuildPath(GetMachinePath()), RegistryValueKind.ExpandString);
                    notes.Add("系统环境 JAVA_HOME / Path 已同步更新");
                }
                else
                {
                    notes.Add("系统环境含旧 Java 配置，需要管理员权限才能完整切换（已跳过）");
                }
            }

            BroadcastEnvironmentChanged();
            return notes;
        }

        /// <summary>广播 WM_SETTINGCHANGE，通知 Explorer 等刷新环境变量。</summary>
        public static void BroadcastEnvironmentChanged()
        {
            try
            {
                IntPtr result;
                SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "Environment", SMTO_ABORTIFHUNG, 1000, out result);
            }
            catch
            {
            }
        }

        /// <summary>解析新会话中 java.exe 会解析到的实际位置（系统 PATH 优先于用户 PATH）。</summary>
        public static string ResolveCurrentJavaExe()
        {
            string machinePath = GetMachinePath();
            string userPath = GetUserPath();
            string merged = (machinePath == null ? "" : machinePath) + ";" + (userPath == null ? "" : userPath);
            List<string> entries = SplitPath(merged);
            for (int i = 0; i < entries.Count; i++)
            {
                string exe = FindJavaInEntry(entries[i]);
                if (exe != null) return exe;
            }

            // 兜底：按当前进程 PATH 再找一次。
            List<string> processEntries = SplitPath(Environment.GetEnvironmentVariable("PATH"));
            for (int i = 0; i < processEntries.Count; i++)
            {
                string exe = FindJavaInEntry(processEntries[i]);
                if (exe != null) return exe;
            }
            return null;
        }

        private static string FindJavaInEntry(string entry)
        {
            string expanded = ExpandForCheck(entry);
            if (expanded.Length == 0) return null;
            try
            {
                if (File.Exists(Path.Combine(expanded, "java.exe"))) return Path.Combine(expanded, "java.exe");
                if (File.Exists(Path.Combine(expanded, "bin", "java.exe"))) return Path.Combine(expanded, "bin", "java.exe");
            }
            catch
            {
            }
            return null;
        }

        /// <summary>运行指定 java.exe -version，返回输出首行。</summary>
        public static string ReadJavaFirstLine(string exe)
        {
            if (exe == null || !File.Exists(exe)) return null;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(exe, "-version");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                using (Process process = Process.Start(info))
                {
                    string output = process.StandardError.ReadToEnd();
                    string stdout = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    if (output == null || output.Length == 0) output = stdout;
                    if (output == null) return null;
                    string[] lines = output.Replace("\r", "").Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (line.Length > 0) return line;
                    }
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
