using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;

namespace MouseSideKeyMapper
{
    public class HiddenForm : Form
    {
        private const int WM_USER_ACTION = 0x0400;
        private const int ACTION_X1 = 1;
        private const int ACTION_X2 = 2;

        private const int WH_MOUSE_LL = 14;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc _mouseHookProc;
        private IntPtr _hMouseHook = IntPtr.Zero;
        private GCHandle _hookDelegateHandle;

        private static Dictionary<string, ushort> NameToVk;
        private string _上侧键;
        private string _下侧键;
        private string _iniPath;
        private string _currentConfigName;
        private bool _mappingEnabled;
        private bool _cleanedUp;

        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _menuAutoStart;
        private ToolStripMenuItem _menuConfigs;
        private ToolStripMenuItem _openINI;
        private ToolStripMenuItem _reloadItem;
        private ToolStripMenuItem _exitItem;

        #region SendInput
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion u;
        }
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        const uint KEYEVENTF_SCANCODE = 0x0008;
        const uint MAPVK_VK_TO_VSC = 0;
        #endregion

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        #region API
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern int GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, char[] lpReturnedString, int nSize, string lpFileName);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool WritePrivateProfileString(string lpAppName, string lpKeyName, string lpString, string lpFileName);
        [DllImport("kernel32.dll")]
        public static extern void Sleep(uint dwMilliseconds);
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);
        #endregion

        static HiddenForm()
        {
            NameToVk = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            NameToVk.Add("A", 0x41);
            NameToVk.Add("B", 0x42);
            NameToVk.Add("C", 0x43);
            NameToVk.Add("D", 0x44);
            NameToVk.Add("E", 0x45);
            NameToVk.Add("F", 0x46);
            NameToVk.Add("G", 0x47);
            NameToVk.Add("H", 0x48);
            NameToVk.Add("I", 0x49);
            NameToVk.Add("J", 0x4A);
            NameToVk.Add("K", 0x4B);
            NameToVk.Add("L", 0x4C);
            NameToVk.Add("M", 0x4D);
            NameToVk.Add("N", 0x4E);
            NameToVk.Add("O", 0x4F);
            NameToVk.Add("P", 0x50);
            NameToVk.Add("Q", 0x51);
            NameToVk.Add("R", 0x52);
            NameToVk.Add("S", 0x53);
            NameToVk.Add("T", 0x54);
            NameToVk.Add("U", 0x55);
            NameToVk.Add("V", 0x56);
            NameToVk.Add("W", 0x57);
            NameToVk.Add("X", 0x58);
            NameToVk.Add("Y", 0x59);
            NameToVk.Add("Z", 0x5A);

            NameToVk.Add("F1", 0x70);
            NameToVk.Add("F2", 0x71);
            NameToVk.Add("F3", 0x72);
            NameToVk.Add("F4", 0x73);
            NameToVk.Add("F5", 0x74);
            NameToVk.Add("F6", 0x75);
            NameToVk.Add("F7", 0x76);
            NameToVk.Add("F8", 0x77);
            NameToVk.Add("F9", 0x78);
            NameToVk.Add("F10", 0x79);
            NameToVk.Add("F11", 0x7A);
            NameToVk.Add("F12", 0x7B);

            NameToVk.Add("ESC", 0x1B);
            NameToVk.Add("TAB", 0x09);
            NameToVk.Add("SPACE", 0x20);
            NameToVk.Add("ENTER", 0x0D);
            NameToVk.Add("BACK", 0x08);
            NameToVk.Add("DEL", 0x2E);
            NameToVk.Add("HOME", 0x24);
            NameToVk.Add("END", 0x23);
            NameToVk.Add("UP", 0x26);
            NameToVk.Add("DOWN", 0x28);
            NameToVk.Add("LEFT", 0x25);
            NameToVk.Add("RIGHT", 0x27);

            NameToVk.Add("0", 0x30);
            NameToVk.Add("1", 0x31);
            NameToVk.Add("2", 0x32);
            NameToVk.Add("3", 0x33);
            NameToVk.Add("4", 0x34);
            NameToVk.Add("5", 0x35);
            NameToVk.Add("6", 0x36);
            NameToVk.Add("7", 0x37);
            NameToVk.Add("8", 0x38);
            NameToVk.Add("9", 0x39);

            NameToVk.Add("CTRL", 0xA2);
            NameToVk.Add("LCTRL", 0xA2);
            NameToVk.Add("RCTRL", 0xA3);
            NameToVk.Add("SHIFT", 0xA0);
            NameToVk.Add("LSHIFT", 0xA0);
            NameToVk.Add("RSHIFT", 0xA1);
            NameToVk.Add("ALT", 0xA4);
            NameToVk.Add("LALT", 0xA4);
            NameToVk.Add("RALT", 0xA5);
            NameToVk.Add("WIN", 0x5B);
            NameToVk.Add("LWIN", 0x5B);
            NameToVk.Add("RWIN", 0x5C);
			NameToVk.Add("INSERT", 0x2D);   // 或 INS
			NameToVk.Add("PRTSC", 0x2C);    // Print Screen
        }

        public HiddenForm()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(0, 0);
            Opacity = 0;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-9999, -9999);

            _mappingEnabled = true;
            _currentConfigName = LoadLastConfig();
            _iniPath = Path.Combine(Application.StartupPath, _currentConfigName + ".ini");
            LoadIni();
            InitTray();

            _mouseHookProc = HookCallback;
            _hookDelegateHandle = GCHandle.Alloc(_mouseHookProc);
            IntPtr hUser32 = LoadLibrary("user32.dll");
            _hMouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc, hUser32, 0);

            UpdateTrayTip();
            EnsureAutoStartTaskPath();   // 启动时校验计划任务路径与当前 exe 一致
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080;
                cp.ExStyle |= 0x08000000;
                cp.ExStyle &= ~0x00040000;
                return cp;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _mappingEnabled)
            {
                MSLLHOOKSTRUCT m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                uint msg = (uint)wParam;
                uint btn = (m.mouseData >> 16) & 0xFFFF;
                if (msg == 0x020B) // WM_XBUTTONDOWN
                {
                    if (btn == 1)
                    {
                        PostMessage(this.Handle, WM_USER_ACTION, (IntPtr)ACTION_X1, IntPtr.Zero);
                        return (IntPtr)1; // 吞掉事件，抑制侧键原始功能
                    }
                    if (btn == 2)
                    {
                        PostMessage(this.Handle, WM_USER_ACTION, (IntPtr)ACTION_X2, IntPtr.Zero);
                        return (IntPtr)1; // 吞掉事件，抑制侧键原始功能
                    }
                }
                if (msg == 0x020C) // WM_XBUTTONUP，一并吞掉防止残留
                {
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hMouseHook, nCode, wParam, lParam);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_USER_ACTION)
            {
                int action = (int)m.WParam;
                if (action == ACTION_X1)
                {
                    RunAction(_下侧键);
                }
                else if (action == ACTION_X2)
                {
                    RunAction(_上侧键);
                }
                return;
            }
            base.WndProc(ref m);
        }

        private void RunAction(string action)
        {
            string cmd = action.Trim().ToUpper();
            if (cmd == "TASKVIEW")
            {
                System.Diagnostics.Process.Start("explorer.exe", "shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}");
                return;
            }
            if (cmd == "WIN+D")
            {
                keybd_event(0x5B, 0x5B, 0, UIntPtr.Zero);
                keybd_event((byte)'D', 0x20, 0, UIntPtr.Zero);
                Sleep(20);
                keybd_event((byte)'D', 0x20, 2, UIntPtr.Zero);
                keybd_event(0x5B, 0x5B, 2, UIntPtr.Zero);
                return;
            }
            if (cmd == "WIN+E")
            {
                keybd_event(0x5B, 0x5B, 0, UIntPtr.Zero);
                keybd_event((byte)'E', 0x12, 0, UIntPtr.Zero);
                Sleep(20);
                keybd_event((byte)'E', 0x12, 2, UIntPtr.Zero);
                keybd_event(0x5B, 0x5B, 2, UIntPtr.Zero);
                return;
            }
            if (cmd == "WIN+R")
            {
                keybd_event(0x5B, 0x5B, 0, UIntPtr.Zero);
                keybd_event((byte)'R', 0x13, 0, UIntPtr.Zero);
                Sleep(20);
                keybd_event((byte)'R', 0x13, 2, UIntPtr.Zero);
                keybd_event(0x5B, 0x5B, 2, UIntPtr.Zero);
                return;
            }
            if (cmd == "WIN+L")
            {
                keybd_event(0x5B, 0x5B, 0, UIntPtr.Zero);
                keybd_event((byte)'L', 0x26, 0, UIntPtr.Zero);
                Sleep(20);
                keybd_event((byte)'L', 0x26, 2, UIntPtr.Zero);
                keybd_event(0x5B, 0x5B, 2, UIntPtr.Zero);
                return;
            }
            SendKeybdCombo(action);
        }

        #region INI（UTF-8 文件解析，兼容中文键名）
        private string ReadIniValue(string section, string key, string def)
        {
            try
            {
                if (!File.Exists(_iniPath)) return def;
                bool inSection = false;
                foreach (string rawLine in File.ReadAllLines(_iniPath))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line[0] == ';') continue;  // 跳过空行和注释行
                    if (line[0] == '[')
                    {
                        inSection = string.Equals(line, "[" + section + "]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inSection) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) continue;
                    string v = line.Substring(eq + 1).Trim();
                    int ci = v.IndexOf(';');  // 去掉行尾注释
                    if (ci >= 0) v = v.Substring(0, ci).Trim();
                    return v;
                }
                return def;
            }
            catch
            {
                return def;
            }
        }
        private void LoadIni()
        {
            _上侧键 = ReadIniValue("Mapping", "上侧键", "WIN+D");
            _下侧键 = ReadIniValue("Mapping", "下侧键", "TASKVIEW");
            if (!File.Exists(_iniPath))
            {
                SaveIni();
            }
        }
        private void SaveIni()
        {
            string[] lines = new string[]
            {
                "[Mapping]",
                "上侧键=" + _上侧键,
                "下侧键=" + _下侧键
            };
            File.WriteAllLines(_iniPath, lines, new UTF8Encoding(false));
        }
        private void OpenIni()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _iniPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("当前配置文件不存在！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        #endregion

        #region Tray
        private void InitTray()
        {
            _menu = new ContextMenuStrip();
            _menuAutoStart = new ToolStripMenuItem("开机启动");
            _menuAutoStart.Click += MenuAutoStart_Click;
            UpdateAutoStartText();

            _openINI = new ToolStripMenuItem("打开配置");
            _openINI.Click += Open_Click;

            _menuConfigs = new ToolStripMenuItem("切换配置");
            BuildConfigMenu();

            _reloadItem = new ToolStripMenuItem("重载配置");
            _reloadItem.Click += Reload_Click;

            _exitItem = new ToolStripMenuItem("退出");
            _exitItem.Click += Exit_Click;

            _menu.Items.Add(_menuAutoStart);
            _menu.Items.Add(_menuConfigs);
			_menu.Items.Add(_openINI);
            _menu.Items.Add(_reloadItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_exitItem);

            _notifyIcon = new NotifyIcon();
			// 使用EXE本身嵌入图标
			_notifyIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            _notifyIcon.ContextMenuStrip = _menu;   // NotifyIcon 自动弹菜单（标准托盘行为，不进任务栏）
            _notifyIcon.Visible = true;
            _notifyIcon.MouseDown += NotifyIcon_MouseDown;
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }
        // 左键：临时移除菜单避免自动弹出；右键：确保菜单由 NotifyIcon 自动弹出
        private void NotifyIcon_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _notifyIcon.ContextMenuStrip = null;
            }
            else if (e.Button == MouseButtons.Right)
            {
                _notifyIcon.ContextMenuStrip = _menu;
            }
        }
        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _mappingEnabled = !_mappingEnabled;   // 左键单击切换映射开关
                UpdateTrayTip();
            }
        }

        private void MenuAutoStart_Click(object sender, EventArgs e)
        {
            SetAutoStart(!IsAutoStart());
            UpdateAutoStartText();
        }
        private void Open_Click(object sender, EventArgs e)
        {
            OpenIni();
        }
        private void BuildConfigMenu()
        {
            _menuConfigs.DropDownItems.Clear();
            string[] files = System.IO.Directory.GetFiles(Application.StartupPath, "*.ini");
            if (files.Length == 0)
            {
                ToolStripMenuItem none = new ToolStripMenuItem("（无配置文件）");
                none.Enabled = false;
                _menuConfigs.DropDownItems.Add(none);
                return;
            }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string f in files)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(f);
                ToolStripMenuItem item = new ToolStripMenuItem(name);
                item.Checked = (name == _currentConfigName);
                item.Tag = name;
                item.Click += Config_Click;
                _menuConfigs.DropDownItems.Add(item);
            }
        }
        private void Config_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || item.Tag == null) return;
            SwitchConfig((string)item.Tag);
        }
        private void SwitchConfig(string name)
        {
            _currentConfigName = name;
            _iniPath = Path.Combine(Application.StartupPath, name + ".ini");
            LoadIni();
            SaveLastConfig(name);
            BuildConfigMenu();
            UpdateTrayTip();
            _notifyIcon.ShowBalloonTip(800, "配置已切换", "当前配置：" + name, ToolTipIcon.Info);
        }
        // 读取上次使用的配置名（不存在或无对应 ini 时回退"默认"）
        private string LoadLastConfig()
        {
            try
            {
                string f = Path.Combine(Application.StartupPath, "lastconfig.txt");
                if (File.Exists(f))
                {
                    string name = File.ReadAllText(f, Encoding.UTF8).Trim();
                    if (!string.IsNullOrEmpty(name) &&
                        File.Exists(Path.Combine(Application.StartupPath, name + ".ini")))
                    {
                        return name;
                    }
                }
            }
            catch { }
            return "默认";
        }
        private void SaveLastConfig(string name)
        {
            try
            {
                File.WriteAllText(Path.Combine(Application.StartupPath, "lastconfig.txt"), name, new UTF8Encoding(false));
            }
            catch { }
        }
        private void Reload_Click(object sender, EventArgs e)
        {
            LoadIni();
            BuildConfigMenu();
        }        
        private void CleanupAll()
        {
            if (_cleanedUp) return;   // 幂等，重复调用安全
            _cleanedUp = true;

            // 1. 先卸低级鼠标钩子，杜绝回调残留
            if (_hMouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hMouseHook);
                _hMouseHook = IntPtr.Zero;
            }
            // 2. 释放固定回调委托的 GCHandle
            if (_hookDelegateHandle.IsAllocated)
            {
                _hookDelegateHandle.Free();
            }
            // 3. 隐藏并释放托盘图标
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            // 4. 释放托盘菜单
            if (_menu != null)
            {
                _menu.Dispose();
                _menu = null;
            }
        }
        private void Exit_Click(object sender, EventArgs e)
        {
            CleanupAll();          // 显式、按顺序释放
            Application.Exit();    // 结束消息循环，进程退出
        }
        private void UpdateTrayTip()
        {
            if (_mappingEnabled)
            {
                _notifyIcon.Text = "映射已开启 点击切换";
            }
            else
            {
                _notifyIcon.Text = "映射已关闭 点击切换";
            }
        }
        #endregion

        #region AutoStart（任务计划：适用于需要管理员权限的 EXE）
        private const string TaskName = "MouseSideKeyMapper";
        private static string lastSchtasksError;
        // 用完整路径调用 schtasks，避免依赖 PATH 找不到
        private static string SchtasksExe
        {
            get { return Path.Combine(Environment.SystemDirectory, "schtasks.exe"); }
        }
        // 查询：存在返回退出码 0
        private bool IsAutoStart()
        {
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = SchtasksExe;
                p.StartInfo.Arguments = "/Query /TN \"" + TaskName + "\"";
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.Start();
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch { return false; }
        }
        // schtasks /XML 导入任务
        private void SetAutoStart(bool enable)
        {
            try
            {
                if (enable)
                {
                    if (CreateStartupTask() != 0)
                        MessageBox.Show("开机启动设置失败：" + (lastSchtasksError ?? "未知错误"),
                            "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    if (DeleteStartupTask() != 0)
                        MessageBox.Show("取消开机启动失败：" + (lastSchtasksError ?? "未知错误"),
                            "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("开机启动设置失败：" + ex.Message,
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        // 用完整路径调用 schtasks 并返回退出码
        private static int RunSchtasks(string arguments)
        {
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = SchtasksExe;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.Start();
                p.WaitForExit();
                return p.ExitCode;
            }
            catch (Exception ex) { lastSchtasksError = ex.Message; return -1; }
        }
        // 创建：先生成任务 XML（UTF-16 写临时文件），再 /Create /XML 导入；失败回退简单命令
        private int CreateStartupTask()
        {
            string xmlPath = null;
            try
            {
                string xml = BuildStartupTaskXml();
                if (xml == null) { lastSchtasksError = "无法获取当前用户"; return -1; }
                string dir = Path.GetTempPath();
                xmlPath = Path.Combine(dir, "MSKM_" + Guid.NewGuid().ToString("N") + ".xml");
                using (FileStream fs = new FileStream(xmlPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    byte[] bom = System.Text.Encoding.Unicode.GetPreamble();
                    byte[] body = System.Text.Encoding.Unicode.GetBytes(xml);
                    fs.Write(bom, 0, bom.Length);
                    fs.Write(body, 0, body.Length);
                    fs.Flush();
                }
                int rc = RunSchtasks("/Create /F /TN " + TaskName + " /XML \"" + xmlPath + "\"");
                if (rc == 0) return 0;
                // 回退：简单命令行创建
                return RunSchtasks("/Create /F /SC ONLOGON /RL HIGHEST /TN " + TaskName
                    + " /TR \"" + Application.ExecutablePath + "\"");
            }
            catch (Exception ex) { lastSchtasksError = ex.Message; return -1; }
            finally { try { if (xmlPath != null) File.Delete(xmlPath); } catch { } }
        }
        // 生成任务 XML：登录时触发 + 最高权限 + 关闭电池限制（完全参照 Pavise）
        private static string BuildStartupTaskXml()
        {
            string user;
            try
            {
                using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                    user = id.User.Value;
            }
            catch { user = null; }
            if (string.IsNullOrEmpty(user))
            {
                user = Environment.UserDomainName + "\\" + Environment.UserName;
                if (user.Length <= 1) return null;
            }
            string exe = Application.ExecutablePath;
            string cmd = XmlText(exe);
            string wd = XmlText(Path.GetDirectoryName(exe));
            string start = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n"
                + "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n"
                + "  <RegistrationInfo><URI>\\" + TaskName + "</URI></RegistrationInfo>\r\n"
                + "  <Principals><Principal id=\"Author\"><UserId>" + XmlText(user) + "</UserId>"
                + "<LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel>"
                + "</Principal></Principals>\r\n"
                + "  <Settings>"
                + "<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>"
                + "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>"
                + "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>"
                + "<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>"
                + "</Settings>\r\n"
                + "  <Triggers><LogonTrigger><StartBoundary>" + start + "</StartBoundary></LogonTrigger></Triggers>\r\n"
                + "  <Actions Context=\"Author\"><Exec><Command>" + cmd + "</Command>"
                + "<WorkingDirectory>" + wd + "</WorkingDirectory></Exec></Actions>\r\n"
                + "</Task>";
        }
        private static string XmlText(string value)
        {
            return (value ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
        private int DeleteStartupTask()
        {
            return RunSchtasks("/Delete /F /TN " + TaskName);
        }
        private void UpdateAutoStartText()
        {
            if (IsAutoStart())
            {
                _menuAutoStart.Text = "取消开机启动";
            }
            else
            {
                _menuAutoStart.Text = "开机启动";
            }
        }
        // 启动时校验：计划任务指向的 exe 若与当前运行的 exe 不一致则自动更新（文件名/位置变化后无需手动重建）
        private void EnsureAutoStartTaskPath()
        {
            try
            {
                if (!IsAutoStart()) return;   // 任务不存在（用户未开启开机启动），不动
                string cur = Application.ExecutablePath;
                string taskPath = GetTaskRunPathCom();
                if (taskPath != null &&
                    !string.Equals(taskPath.Trim(), cur.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    CreateStartupTask();   // 用 XML 重建（覆盖），指向当前 exe
                }
            }
            catch { }
        }
        // 用 COM 读取计划任务指向的 exe 路径（避免中文路径编码问题）
        private string GetTaskRunPathCom()
        {
            try
            {
                Type t = Type.GetTypeFromProgID("Schedule.Service");
                if (t == null) return null;
                dynamic sched = Activator.CreateInstance(t);
                sched.Connect();
                dynamic folder = sched.GetFolder("\\");
                dynamic task = folder.GetTask(TaskName);
                dynamic action = task.Definition.Actions[0];
                return (string)action.Path;
            }
            catch { return null; }
        }
        #endregion

        #region 组合键发送（支持三键，统一keybd_event + 正确扫描码/扩展键）
        private static void SendKeybdCombo(string comboText)
        {
            string[] parts = comboText.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            List<ushort> vks = new List<ushort>();
            foreach (string p in parts)
            {
                string k = p.Trim().ToUpper();
                if (NameToVk.ContainsKey(k))
                {
                    vks.Add(NameToVk[k]);
                }
            }
            if (vks.Count == 0) return;
            foreach (ushort vk in vks)
            {
                uint flags = IsExtendedKey(vk) ? KEYEVENTF_EXTENDEDKEY : 0;
                keybd_event((byte)vk, (byte)MapVirtualKey(vk, MAPVK_VK_TO_VSC), flags, UIntPtr.Zero);
            }
            Sleep(30);
            for (int i = vks.Count - 1; i >= 0; i--)
            {
                uint flags = KEYEVENTF_KEYUP;
                if (IsExtendedKey(vks[i])) flags |= KEYEVENTF_EXTENDEDKEY;
                keybd_event((byte)vks[i], (byte)MapVirtualKey(vks[i], MAPVK_VK_TO_VSC), flags, UIntPtr.Zero);
            }
        }
        private static bool IsExtendedKey(ushort vk)
        {
            switch (vk)
            {
                case 0x5B: case 0x5C:   // LWIN / RWIN
                case 0xA3: case 0xA5:   // RCTRL / RALT
                case 0x21: case 0x22:   // PAGEUP / PAGEDOWN
                case 0x23: case 0x24:   // END / HOME
                case 0x25: case 0x26: case 0x27: case 0x28: // 方向键
                case 0x2D: case 0x2E:   // INSERT / DELETE
                    return true;
                default:
                    return false;
            }
        }
        #endregion

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            CleanupAll();   // 兜底释放
            base.OnFormClosed(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            // 命名互斥量实现单实例：第二个实例启动即退出
            bool createdNew;
            using (System.Threading.Mutex single = new System.Threading.Mutex(true, "MouseSideKeyMapper_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("鼠标侧键映射已在运行", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new HiddenForm());
            }
        }
    }
}
