using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace JavaSwitcher
{
    public sealed class MainForm : Form
    {
        private readonly Panel _cardPanel = new Panel();
        private readonly Label _countLabel = new Label();
        private readonly Label _permLabel = new Label();

        private readonly TextBox _txtMachineHome = CreateReadOnlyTextBox();
        private readonly TextBox _txtUserHome = CreateReadOnlyTextBox();
        private readonly TextBox _txtEffective = CreateReadOnlyTextBox();
        private readonly TextBox _txtJavaExe = CreateReadOnlyTextBox();
        private readonly TextBox _txtJavaVersion = CreateReadOnlyTextBox();

        private readonly TextBox _logBox = new TextBox();
        private readonly Button _btnScan = new Button();
        private readonly Button _btnRefresh = new Button();
        private Button _btnRunAsAdmin;

        private bool _busy;

        public MainForm()
        {
            Text = "Java 环境监测与切换器";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 700);
            Size = new Size(1200, 920);
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9.5f);
            DoubleBuffered = true;

            BuildUi();
            RefreshState();
            ScanAsync();
        }

        // ---------------- UI ----------------
        private void BuildUi()
        {
            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(12);
            content.Controls.Add(BuildCenterPanel());
            content.Controls.Add(BuildBottomPanel());
            content.Controls.Add(BuildTopPanel());
            Controls.Add(content);
        }

        private Control BuildTopPanel()
        {
            const int fieldRowHeight = 44;
            const int toolbarRowHeight = 60;

            _btnScan.Text = "重新扫描";
            _btnScan.Width = 116;
            _btnScan.Height = 34;
            _btnScan.Margin = new Padding(0, 13, 8, 13);
            _btnScan.Click += delegate { ScanAsync(); };

            _btnRefresh.Text = "刷新状态";
            _btnRefresh.Width = 116;
            _btnRefresh.Height = 34;
            _btnRefresh.Margin = new Padding(0, 13, 8, 13);
            _btnRefresh.Click += delegate { RefreshState(); };

            _btnRunAsAdmin = new Button();
            _btnRunAsAdmin.Text = "以管理员身份重启";
            _btnRunAsAdmin.Width = 172;
            _btnRunAsAdmin.Height = 34;
            _btnRunAsAdmin.Margin = new Padding(0, 13, 8, 13);
            _btnRunAsAdmin.Click += delegate { LaunchAsAdmin(); };

            _permLabel.AutoSize = true;
            _permLabel.Margin = new Padding(12, 13, 0, 13);
            _permLabel.TextAlign = ContentAlignment.MiddleLeft;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.Controls.Add(_btnScan);
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnRunAsAdmin);
            toolbar.Controls.Add(_permLabel);

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.ColumnCount = 2;
            grid.RowCount = 6;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 5; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, fieldRowHeight));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, toolbarRowHeight));

            AddFieldRow(grid, 0, "JAVA_HOME（系统）", _txtMachineHome);
            AddFieldRow(grid, 1, "JAVA_HOME（用户）", _txtUserHome);
            AddFieldRow(grid, 2, "当前生效 JAVA_HOME", _txtEffective);
            AddFieldRow(grid, 3, "java.exe 实际位置", _txtJavaExe);
            AddFieldRow(grid, 4, "当前 java 版本", _txtJavaVersion);

            grid.Controls.Add(toolbar, 0, 5);
            grid.SetColumnSpan(toolbar, 2);

            GroupBox box = new GroupBox();
            box.Text = "当前状态（读取自注册表；切换后请在新开的终端里生效）";
            box.Dock = DockStyle.Top;
            box.Height = 306;
            box.Padding = new Padding(12, 6, 12, 10);

            grid.Dock = DockStyle.Fill;
            box.Controls.Add(grid);
            return box;
        }

        private static void AddFieldRow(TableLayoutPanel grid, int row, string caption, TextBox textBox)
        {
            Label label = new Label();
            label.Text = caption;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(2, 6, 10, 6);
            grid.Controls.Add(label, 0, row);

            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(0, 6, 2, 6);
            grid.Controls.Add(textBox, 1, row);
        }

        private Control BuildCenterPanel()
        {
            _countLabel.AutoSize = true;
            _countLabel.Margin = new Padding(2, 2, 2, 4);
            _countLabel.ForeColor = Color.FromArgb(80, 84, 92);

            _cardPanel.AutoScroll = true;
            _cardPanel.Dock = DockStyle.Fill;
            _cardPanel.Padding = new Padding(4);
            _cardPanel.BackColor = Color.White;
            _cardPanel.Resize += delegate { ReflowCards(); };

            GroupBox box = new GroupBox();
            box.Text = "已扫描到的 Java 环境（点击任意版本卡片即可切换）";
            box.Dock = DockStyle.Fill;
            box.Padding = new Padding(8, 4, 8, 8);

            Panel inner = new Panel();
            inner.Dock = DockStyle.Fill;
            inner.Controls.Add(_cardPanel);
            inner.Controls.Add(_countLabel);
            _countLabel.Dock = DockStyle.Top;

            box.Controls.Add(inner);
            return box;
        }

        private Control BuildBottomPanel()
        {
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.ScrollBars = ScrollBars.Vertical;
            _logBox.BackColor = Color.FromArgb(30, 32, 38);
            _logBox.ForeColor = Color.FromArgb(220, 224, 230);
            _logBox.BorderStyle = BorderStyle.None;
            _logBox.Font = new Font("Consolas", 10f);
            _logBox.Dock = DockStyle.Fill;

            GroupBox box = new GroupBox();
            box.Text = "日志";
            box.Dock = DockStyle.Bottom;
            box.Height = 240;
            box.Padding = new Padding(12, 8, 12, 12);
            box.Controls.Add(_logBox);
            return box;
        }

        private void ReflowCards()
        {
            int clientWidth = _cardPanel.ClientSize.Width - 14;
            if (clientWidth < 120) clientWidth = 120;
            int y = 4;
            for (int i = 0; i < _cardPanel.Controls.Count; i++)
            {
                Control control = _cardPanel.Controls[i];
                VersionCard card = control as VersionCard;
                if (card == null) continue;
                card.Width = clientWidth;
                card.Left = 7;
                card.Top = y;
                y += card.Height + 10;
            }
        }

        // ---------------- 状态刷新 ----------------
        private void RefreshState()
        {
            _txtMachineHome.Text = ToDisplay(EnvironmentService.MachineJavaHome);
            _txtUserHome.Text = ToDisplay(EnvironmentService.UserJavaHome);
            _txtEffective.Text = ToDisplay(EnvironmentService.EffectiveJavaHome());

            string exe = EnvironmentService.ResolveCurrentJavaExe();
            _txtJavaExe.Text = exe == null ? "(未找到，请检查 PATH 中的 java.exe)" : exe;
            _txtJavaVersion.Text = EnvironmentService.ReadJavaFirstLine(exe);
            if (string.IsNullOrEmpty(_txtJavaVersion.Text)) _txtJavaVersion.Text = "(无法获取)";

            bool isAdmin = EnvironmentService.IsAdministrator();
            if (isAdmin)
            {
                _permLabel.Text = "● 管理员模式：可直接修改系统环境变量";
                _permLabel.ForeColor = Color.FromArgb(46, 139, 87);
                _btnRunAsAdmin.Visible = false;
            }
            else
            {
                _permLabel.Text = "● 普通模式：涉及系统环境时切换将自动请求管理员授权";
                _permLabel.ForeColor = Color.FromArgb(220, 120, 20);
                _btnRunAsAdmin.Visible = true;
            }

            Log("状态已刷新");
        }

        private static string ToDisplay(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "(未设置)";
            return value.Trim();
        }

        // ---------------- 扫描 ----------------
        private void ScanAsync()
        {
            SetBusy(true, "正在扫描本机 Java 环境…");
            Thread thread = new Thread(delegate()
            {
                List<JavaInstallation> list;
                try
                {
                    list = JavaScanner.Scan();
                }
                catch (Exception ex)
                {
                    list = new List<JavaInstallation>();
                    if (!IsDisposed)
                    {
                        try
                        {
                            BeginInvoke(new Action<string>(delegate(string message) { SetBusy(false, "扫描失败"); Log(message); }), "扫描失败: " + ex.Message);
                        }
                        catch
                        {
                        }
                    }
                    return;
                }
                if (IsDisposed) return;
                try
                {
                    BeginInvoke(new Action<List<JavaInstallation>>(delegate(List<JavaInstallation> items) { RenderResults(items); }), list);
                }
                catch
                {
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void RenderResults(List<JavaInstallation> installations)
        {
            _cardPanel.Controls.Clear();
            string effective = EnvironmentService.EffectiveJavaHome();
            string effectiveKey = NormalizeKey(effective);

            int y = 4;
            for (int i = 0; i < installations.Count; i++)
            {
                JavaInstallation installation = installations[i];
                bool current = NormalizeKey(installation.Home) == effectiveKey;
                VersionCard card = new VersionCard(installation, current);
                card.Clicked += OnCardClicked;
                card.Height = 68;
                card.Width = Math.Max(180, _cardPanel.ClientSize.Width - 14);
                card.Left = 7;
                card.Top = y;
                _cardPanel.Controls.Add(card);
                y += card.Height + 10;
            }

            if (installations.Count == 0)
            {
                Label empty = new Label();
                empty.Text = "未扫描到可用的 Java 环境。可点击“重新扫描”，或确认 Java 已安装。";
                empty.AutoSize = true;
                empty.ForeColor = Color.FromArgb(120, 124, 132);
                empty.Location = new Point(10, 10);
                _cardPanel.Controls.Add(empty);
            }

            _countLabel.Text = "共发现 " + installations.Count + " 个 Java 环境";
            SetBusy(false, "扫描完成");
            Log("扫描完成：共发现 " + installations.Count + " 个 Java 环境");
        }

        private static string NormalizeKey(string home)
        {
            if (string.IsNullOrWhiteSpace(home)) return "";
            try
            {
                return System.IO.Path.GetFullPath(home).TrimEnd('\\').ToLowerInvariant();
            }
            catch
            {
                return home.Trim().ToLowerInvariant();
            }
        }

        // ---------------- 切换 ----------------
        private void OnCardClicked(JavaInstallation installation)
        {
            if (_busy) return;
            bool needsMachine = EnvironmentService.MachineHasJavaConfig();
            bool isAdmin = EnvironmentService.IsAdministrator();

            StringBuilder message = new StringBuilder();
            message.Append("确认将当前 Java 环境切换为：\n\n");
            message.Append("   版本：").Append(installation.Headline).Append('\n');
            message.Append("   路径：").Append(installation.Home).Append('\n');
            message.Append('\n');
            message.Append(needsMachine
                ? "本机 Java 配置在系统级，将同步修改系统与用户的 JAVA_HOME / Path（需要管理员授权）。"
                : "将修改当前用户的 JAVA_HOME / Path。");

            DialogResult result = MessageBox.Show(
                message.ToString(),
                "确认切换",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes) return;

            if (needsMachine && !isAdmin)
            {
                if (!LaunchElevated(installation.Home)) return;
                Log("已通过管理员进程完成切换：" + installation.Headline);
                RefreshState();
                return;
            }

            try
            {
                SetBusy(true, "正在写入环境变量…");
                List<string> notes = EnvironmentService.PerformSwitch(installation.Home);
                StringBuilder summary = new StringBuilder();
                for (int i = 0; i < notes.Count; i++)
                {
                    if (i > 0) summary.Append("\n");
                    summary.Append("· ").Append(notes[i]);
                }
                summary.Append("\n\n请新开终端或 IDE 验证：java -version");
                Log("切换完成：" + installation.Headline);
                MessageBox.Show(summary.ToString(), "切换完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("切换失败：" + ex.Message);
                MessageBox.Show("切换失败：\n" + ex.Message, "Java 环境切换器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, "切换完成");
                RefreshState();
            }
        }

        private bool LaunchElevated(string home)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Application.ExecutablePath;
                startInfo.Arguments = "--switch \"" + home + "\"";
                startInfo.Verb = "runas";
                startInfo.UseShellExecute = true;
                using (Process process = Process.Start(startInfo))
                {
                    if (process != null) process.WaitForExit();
                }
                return true;
            }
            catch (Win32Exception)
            {
                MessageBox.Show(
                    "未获得管理员授权，系统级切换未执行。\n\n可点击“以管理员身份重启”让整个程序以管理员模式运行，之后再切换即可。",
                    "Java 环境切换器",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        private void LaunchAsAdmin()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Application.ExecutablePath;
                startInfo.Verb = "runas";
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
                Application.Exit();
            }
            catch (Win32Exception)
            {
                MessageBox.Show("未获得管理员授权。", "Java 环境切换器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---------------- 工具 ----------------
        private void SetBusy(bool busy, string statusMessage)
        {
            _busy = busy;
            _btnScan.Enabled = !busy;
            _btnRefresh.Enabled = !busy;
            _cardPanel.Enabled = !busy;
            _countLabel.Text = (busy ? "扫描中…" : _countLabel.Text);
            if (statusMessage != null) Log(statusMessage);
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            _logBox.AppendText("[" + time + "] " + message + "\r\n");
        }

        private static TextBox CreateReadOnlyTextBox()
        {
            TextBox textBox = new TextBox();
            textBox.ReadOnly = true;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.BackColor = Color.White;
            return textBox;
        }
    }

    /// <summary>一个可点击的 Java 版本卡片。</summary>
    internal sealed class VersionCard : Control
    {
        private readonly JavaInstallation _installation;
        private readonly bool _current;
        private bool _hover;

        public event Action<JavaInstallation> Clicked;

        public VersionCard(JavaInstallation installation, bool current)
        {
            _installation = installation;
            _current = current;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            BackColor = Color.White;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Clicked != null)
            {
                Clicked(_installation);
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = _hover
                ? Color.FromArgb(240, 246, 255)
                : (_current ? Color.FromArgb(236, 247, 237) : Color.FromArgb(250, 250, 252));
            Color border = _current
                ? Color.FromArgb(70, 160, 90)
                : (_hover ? Color.FromArgb(120, 168, 235) : Color.FromArgb(214, 218, 224));

            using (GraphicsPath path = RoundedRect(rect, 7))
            {
                using (SolidBrush brush = new SolidBrush(fill)) g.FillPath(brush, path);
                using (Pen pen = new Pen(border, _current ? 2f : 1f)) g.DrawPath(pen, path);
            }

            if (_current)
            {
                using (SolidBrush accent = new SolidBrush(Color.FromArgb(46, 139, 87)))
                {
                    g.FillRectangle(accent, 5, 12, 4, Height - 24);
                }
            }

            float textX = 18f;
            float lineY = 9f;

            using (Font titleFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(38, 40, 46)))
            {
                g.DrawString(_installation.Headline, titleFont, titleBrush, textX, lineY);
            }
            lineY += 23f;

            using (Font subFont = new Font("Consolas", 8.5f))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(118, 122, 132)))
            {
                string detail = _installation.Home + "     来源：" + _installation.Source;
                g.DrawString(detail, subFont, subBrush, textX, lineY);
            }

            if (_current)
            {
                string tag = "★ 当前使用";
                using (Font tagFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold))
                using (SolidBrush tagBrush = new SolidBrush(Color.FromArgb(46, 139, 87)))
                {
                    SizeF size = g.MeasureString(tag, tagFont);
                    g.DrawString(tag, tagFont, tagBrush, Width - size.Width - 14f, 9f);
                }
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
