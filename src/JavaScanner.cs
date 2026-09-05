using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace JavaSwitcher
{
    /// <summary>扫描本机所有可用的 Java 安装（注册表 / PATH / 常见目录递归）。</summary>
    public static class JavaScanner
    {
        private static readonly Regex ReleaseVersion = new Regex("JAVA_VERSION\\s*=\\s*\"([^\"]+)\"");
        private static readonly Regex ReleaseImplementor = new Regex("IMPLEMENTOR\\s*=\\s*\"([^\"]+)\"");
        private static readonly Regex VersionInOutput = new Regex("version\\s+\"([^\"]+)\"");

        public static List<JavaInstallation> Scan()
        {
            Dictionary<string, JavaInstallation> found = new Dictionary<string, JavaInstallation>(StringComparer.OrdinalIgnoreCase);

            ScanRegistry(found);
            ScanPathEntries(found);
            ScanCommonRoots(found);

            List<JavaInstallation> list = new List<JavaInstallation>(found.Values);
            list.Sort(CompareInstallations);
            return list;
        }

        // ---------- 注册表 JavaSoft ----------
        private static void ScanRegistry(Dictionary<string, JavaInstallation> found)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\JavaSoft"))
            {
                WalkJavaSoft(key, 0, found);
            }
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\JavaSoft"))
            {
                WalkJavaSoft(key, 0, found);
            }
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\JavaSoft"))
            {
                WalkJavaSoft(key, 0, found);
            }
        }

        private static void WalkJavaSoft(RegistryKey key, int depth, Dictionary<string, JavaInstallation> found)
        {
            if (key == null || depth > 8) return;
            try
            {
                object homeValue = key.GetValue("JavaHome");
                if (homeValue != null)
                {
                    string home = homeValue.ToString();
                    if (!string.IsNullOrWhiteSpace(home)) AddCandidate(found, home.Trim(), "注册表");
                }
            }
            catch
            {
            }

            try
            {
                string[] names = key.GetSubKeyNames();
                for (int i = 0; i < names.Length; i++)
                {
                    using (RegistryKey child = key.OpenSubKey(names[i]))
                    {
                        if (child != null) WalkJavaSoft(child, depth + 1, found);
                    }
                }
            }
            catch
            {
            }
        }

        // ---------- PATH ----------
        private static void ScanPathEntries(Dictionary<string, JavaInstallation> found)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> merged = new List<string>();
            string machinePath = EnvironmentService.GetMachinePath();
            string userPath = EnvironmentService.GetUserPath();
            string processPath = Environment.GetEnvironmentVariable("PATH");

            if (machinePath != null) merged.AddRange(EnvironmentService.SplitPath(machinePath));
            if (userPath != null) merged.AddRange(EnvironmentService.SplitPath(userPath));
            if (processPath != null) merged.AddRange(EnvironmentService.SplitPath(processPath));

            for (int i = 0; i < merged.Count; i++)
            {
                string entry = merged[i];
                if (!seen.Add(entry.ToLowerInvariant())) continue;
                string expanded = EnvironmentService.ExpandForCheck(entry);
                if (expanded.Length == 0) continue;
                try
                {
                    if (File.Exists(Path.Combine(expanded, "bin", "java.exe")))
                    {
                        AddCandidate(found, expanded, "PATH");
                    }
                    else if (File.Exists(Path.Combine(expanded, "java.exe")))
                    {
                        string home = Path.GetDirectoryName(expanded);
                        if (home != null && File.Exists(Path.Combine(home, "bin", "java.exe")))
                        {
                            AddCandidate(found, home, "PATH");
                        }
                    }
                }
                catch
                {
                }
            }
        }

        // ---------- 常见目录递归 ----------
        private static void ScanCommonRoots(Dictionary<string, JavaInstallation> found)
        {
            List<string> roots = new List<string>();

            string[] drives = Directory.GetLogicalDrives();
            for (int i = 0; i < drives.Length; i++)
            {
                try
                {
                    DriveInfo info = new DriveInfo(drives[i]);
                    if (info.DriveType != DriveType.Fixed) continue;
                }
                catch
                {
                    continue;
                }
                AddRoot(roots, drives[i]);
                AddRoot(roots, Path.Combine(drives[i], "Program Files"));
                AddRoot(roots, Path.Combine(drives[i], "Program Files (x86)"));
                AddRoot(roots, Path.Combine(drives[i], "ProgramData"));
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            AddRoot(roots, Path.Combine(localAppData, "Programs"));
            AddRoot(roots, Path.Combine(userProfile, ".jdks"));
            AddRoot(roots, Path.Combine(userProfile, "Java"));
            AddRoot(roots, Path.Combine(localAppData, "Microsoft", "jdk"));

            for (int i = 0; i < roots.Count; i++)
            {
                Walk(roots[i], 0, 4, found);
            }
        }

        private static void Walk(string dir, int depth, int maxDepth, Dictionary<string, JavaInstallation> found)
        {
            if (dir == null || !Directory.Exists(dir)) return;
            AddCandidate(found, dir, "目录扫描");
            if (depth >= maxDepth) return;
            if (depth > 0 && !ShouldExplore(dir)) return;

            string[] children;
            try
            {
                children = Directory.GetDirectories(dir);
            }
            catch
            {
                return;
            }
            for (int i = 0; i < children.Length; i++)
            {
                Walk(children[i], depth + 1, maxDepth, found);
            }
        }

        private static bool ShouldExplore(string dir)
        {
            string name = Path.GetFileName(dir);
            if (name == null) return false;
            string lower = name.ToLowerInvariant();
            string[] markers = new string[]
            {
                "java", "jdk", "jre", "temurin", "adoptium", "eclipse", "microsoft",
                "oracle", "amazon", "corretto", "zulu", "bellsoft", "liberica",
                "graalvm", "openjdk", "program files", "programdata"
            };
            for (int i = 0; i < markers.Length; i++)
            {
                if (lower.IndexOf(markers[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // ---------- 候选处理 ----------
        private static void AddCandidate(Dictionary<string, JavaInstallation> found, string home, string source)
        {
            if (home == null) return;
            string normalized;
            try
            {
                normalized = Path.GetFullPath(home).TrimEnd('\\');
            }
            catch
            {
                return;
            }
            if (!File.Exists(Path.Combine(normalized, "bin", "java.exe"))) return;
            if (IsNestedJreInsideJdk(normalized)) return;
            if (found.ContainsKey(normalized.ToLowerInvariant())) return;

            Metadata metadata = ReadMetadata(normalized);
            bool jdk = File.Exists(Path.Combine(normalized, "bin", "javac.exe"));
            found.Add(normalized.ToLowerInvariant(), new JavaInstallation(normalized, metadata.Version, metadata.Vendor, jdk, source));
        }

        private static bool IsNestedJreInsideJdk(string home)
        {
            string name = Path.GetFileName(home);
            if (name == null || name.Length < 3) return false;
            if (!name.StartsWith("jre", StringComparison.OrdinalIgnoreCase)) return false;
            string parent = Path.GetDirectoryName(home);
            if (parent == null) return false;
            return File.Exists(Path.Combine(parent, "bin", "javac.exe"));
        }

        private static Metadata ReadMetadata(string home)
        {
            string version = null;
            string vendor = null;

            string releasePath = Path.Combine(home, "release");
            if (File.Exists(releasePath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(releasePath, Encoding.UTF8);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        Match versionMatch = ReleaseVersion.Match(lines[i]);
                        if (versionMatch.Success)
                        {
                            version = StripQuotes(versionMatch.Groups[1].Value);
                            continue;
                        }
                        Match implementorMatch = ReleaseImplementor.Match(lines[i]);
                        if (implementorMatch.Success)
                        {
                            vendor = StripQuotes(implementorMatch.Groups[1].Value);
                        }
                    }
                }
                catch
                {
                }
            }

            if (string.IsNullOrEmpty(version)) version = ReadVersionByRun(home);
            if (string.IsNullOrEmpty(vendor)) vendor = GuessVendor(home);
            return new Metadata(version, vendor);
        }

        private static string ReadVersionByRun(string home)
        {
            string exe = Path.Combine(home, "bin", "java.exe");
            if (!File.Exists(exe)) return null;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(exe, "-version");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardError = true;
                info.RedirectStandardOutput = true;
                using (Process process = Process.Start(info))
                {
                    string output = process.StandardError.ReadToEnd();
                    string stdout = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    if (output == null || output.Length == 0) output = stdout;
                    if (output != null)
                    {
                        Match match = VersionInOutput.Match(output);
                        if (match.Success) return match.Groups[1].Value;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static string GuessVendor(string home)
        {
            if (home == null) return null;
            string lower = home.ToLowerInvariant();
            if (lower.IndexOf("adoptium", StringComparison.OrdinalIgnoreCase) >= 0 || lower.IndexOf("temurin", StringComparison.OrdinalIgnoreCase) >= 0) return "Eclipse Adoptium";
            if (lower.IndexOf("microsoft", StringComparison.OrdinalIgnoreCase) >= 0) return "Microsoft";
            if (lower.IndexOf("corretto", StringComparison.OrdinalIgnoreCase) >= 0) return "Amazon Corretto";
            if (lower.IndexOf("zulu", StringComparison.OrdinalIgnoreCase) >= 0) return "Azul Zulu";
            if (lower.IndexOf("bellsoft", StringComparison.OrdinalIgnoreCase) >= 0 || lower.IndexOf("liberica", StringComparison.OrdinalIgnoreCase) >= 0) return "BellSoft Liberica";
            if (lower.IndexOf("graalvm", StringComparison.OrdinalIgnoreCase) >= 0) return "GraalVM";
            if (lower.IndexOf("oracle", StringComparison.OrdinalIgnoreCase) >= 0) return "Oracle";
            if (lower.IndexOf("openjdk", StringComparison.OrdinalIgnoreCase) >= 0) return "OpenJDK";
            return null;
        }

        private static string StripQuotes(string value)
        {
            if (value == null) return null;
            string text = value.Trim();
            if (text.Length >= 2 && text.StartsWith("\"") && text.EndsWith("\""))
            {
                return text.Substring(1, text.Length - 2);
            }
            return text;
        }

        private static void AddRoot(List<string> roots, string path)
        {
            if (path == null) return;
            try
            {
                if (Directory.Exists(path) && !roots.Contains(path)) roots.Add(path);
            }
            catch
            {
            }
        }

        // ---------- 排序 ----------
        private static int CompareInstallations(JavaInstallation a, JavaInstallation b)
        {
            if (a.IsJdk != b.IsJdk) return a.IsJdk ? -1 : 1;
            int versionCompare = CompareVersions(b.Version, a.Version);
            if (versionCompare != 0) return versionCompare;
            string vendorA = a.Vendor == null ? "" : a.Vendor;
            string vendorB = b.Vendor == null ? "" : b.Vendor;
            int vendorCompare = string.Compare(vendorA, vendorB, StringComparison.OrdinalIgnoreCase);
            if (vendorCompare != 0) return vendorCompare;
            return string.Compare(a.Home, b.Home, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareVersions(string left, string right)
        {
            List<int> leftParts = VersionParts(left);
            List<int> rightParts = VersionParts(right);
            int max = Math.Max(leftParts.Count, rightParts.Count);
            for (int i = 0; i < max; i++)
            {
                int a = i < leftParts.Count ? leftParts[i] : 0;
                int b = i < rightParts.Count ? rightParts[i] : 0;
                if (a != b) return a < b ? -1 : 1;
            }
            return 0;
        }

        private static List<int> VersionParts(string version)
        {
            List<int> parts = new List<int>();
            if (version == null) return parts;
            MatchCollection matches = Regex.Matches(version, "\\d+");
            for (int i = 0; i < matches.Count; i++)
            {
                int number;
                if (int.TryParse(matches[i].Value, out number)) parts.Add(number);
                else parts.Add(0);
            }
            return parts;
        }

        private sealed class Metadata
        {
            public readonly string Version;
            public readonly string Vendor;

            public Metadata(string version, string vendor)
            {
                Version = version;
                Vendor = vendor;
            }
        }
    }
}
