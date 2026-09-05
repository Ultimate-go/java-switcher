using System;
using System.IO;

namespace JavaSwitcher
{
    /// <summary>描述一个已扫描到的 Java 安装。</summary>
    public sealed class JavaInstallation
    {
        private readonly string _home;
        private readonly string _version;
        private readonly string _vendor;
        private readonly bool _jdk;
        private readonly string _source;

        public JavaInstallation(string home, string version, string vendor, bool jdk, string source)
        {
            _home = home;
            _version = version;
            _vendor = vendor;
            _jdk = jdk;
            _source = source;
        }

        /// <summary>Java 安装根目录（含 bin/java.exe）。</summary>
        public string Home
        {
            get { return _home; }
        }

        /// <summary>版本号，如 1.8.0_201 / 11.0.11 / 17.0.18。</summary>
        public string Version
        {
            get { return _version; }
        }

        /// <summary>厂商信息，可能为 null。</summary>
        public string Vendor
        {
            get { return _vendor; }
        }

        /// <summary>是否为 JDK（含 javac）。</summary>
        public bool IsJdk
        {
            get { return _jdk; }
        }

        /// <summary>发现来源：注册表 / PATH / 目录扫描。</summary>
        public string Source
        {
            get { return _source; }
        }

        public string JavaExe
        {
            get { return Path.Combine(_home, "bin", "java.exe"); }
        }

        public bool IsValid
        {
            get { return File.Exists(JavaExe); }
        }

        /// <summary>卡片标题，例如 “JDK 17.0.18 / Eclipse Adoptium / JDK”。</summary>
        public string Headline
        {
            get
            {
                string text = string.IsNullOrWhiteSpace(_version) ? "未知版本" : _version.Trim();
                if (!string.IsNullOrWhiteSpace(_vendor)) text += " / " + _vendor.Trim();
                text += " / " + (_jdk ? "JDK" : "JRE");
                return text;
            }
        }

        public override string ToString()
        {
            return Headline + " @ " + _home;
        }
    }
}
