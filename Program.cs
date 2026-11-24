using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HDRSwitch
{
    #region Localization
    /// <summary>
    /// Provides localized strings following the Windows display language.
    /// Defaults to Portuguese if Windows is in Portuguese (pt-BR, pt-PT), otherwise English.
    /// </summary>
    public static class Loc
    {
        public static readonly bool IsPortuguese =
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase);

        // Tray Context Menu
        public static string MenuOn { get { return IsPortuguese ? "Ligado" : "On"; } }
        public static string MenuOff { get { return IsPortuguese ? "Desligado" : "Off"; } }
        public static string MenuSettings { get { return IsPortuguese ? "Configurações" : "Settings"; } }
        public static string MenuExit { get { return IsPortuguese ? "Sair" : "Exit"; } }

        // Tooltips
        public static string TooltipOn { get { return IsPortuguese ? "HDR: Ligado (Clique para alternar)" : "HDR: On (Click to toggle)"; } }
        public static string TooltipOff { get { return IsPortuguese ? "HDR: Desligado (Clique para alternar)" : "HDR: Off (Click to toggle)"; } }
        public static string TooltipNotSupported { get { return IsPortuguese ? "HDR: Não suportado" : "HDR: Not supported"; } }

        // Balloon Notifications
        public static string AppTitle { get { return "HDR Switch"; } }
        public static string BalloonNoHdrMsg
        {
            get
            {
                return IsPortuguese
                    ? "Nenhum monitor com suporte a HDR foi detectado."
                    : "No HDR-compatible monitor was detected.";
            }
        }

        // Settings Dialog
        public static string SettingsTitle { get { return IsPortuguese ? "Configurações - HDR Switch" : "Settings - HDR Switch"; } }
        public static string SettingsHeader { get { return IsPortuguese ? "Configurações do HDR Switch" : "HDR Switch Settings"; } }
        public static string SettingsSubHeader
        {
            get
            {
                return IsPortuguese
                    ? "Aplicativo portátil para controle rápido de HDR"
                    : "Portable utility for quick HDR toggle";
            }
        }
        public static string GroupStartup { get { return IsPortuguese ? "Inicialização" : "Startup"; } }
        public static string ChkAutoStart
        {
            get
            {
                return IsPortuguese
                    ? "Inicializar com o login do usuário (Windows)"
                    : "Start with user login (Windows)";
            }
        }
        public static string LblAutoStartInfo
        {
            get
            {
                return IsPortuguese
                    ? "Inicia o aplicativo na bandeja do sistema ao fazer login."
                    : "Starts the app minimized to the system tray upon login.";
            }
        }

        public static string MonitorStatus(int count, bool isAnyOn)
        {
            if (IsPortuguese)
            {
                return string.Format("• {0} monitor(es) com suporte a HDR detectado(s).\n  Estado atual: {1}", count, isAnyOn ? "HDR Ligado" : "HDR Desligado");
            }
            return string.Format("• {0} HDR-capable monitor(s) detected.\n  Current state: {1}", count, isAnyOn ? "HDR On" : "HDR Off");
        }

        public static string NoHdrMonitorsDetected
        {
            get
            {
                return IsPortuguese
                    ? "• Nenhum monitor com suporte a HDR detectado no momento."
                    : "• No HDR-compatible monitor detected at this time.";
            }
        }

        public static string ErrorSavingStartup
        {
            get
            {
                return IsPortuguese
                    ? "Erro ao salvar configuração de inicialização:\n"
                    : "Error saving startup configuration:\n";
            }
        }

        public static string ErrorTitle { get { return IsPortuguese ? "Erro" : "Error"; } }
        public static string BtnSave { get { return IsPortuguese ? "Salvar" : "Save"; } }
        public static string BtnCancel { get { return IsPortuguese ? "Cancelar" : "Cancel"; } }
    }
    #endregion

    static class Program
    {
        private static Mutex appMutex;

        [STAThread]
        static void Main()
        {
            // Ensure only one instance runs in the system tray
            bool createdNew;
            appMutex = new Mutex(true, "HDRSwitch_Portable_Tray_Mutex_Unique", out createdNew);
            if (!createdNew)
            {
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new TrayAppContext());
            }
            finally
            {
                if (appMutex != null)
                {
                    appMutex.ReleaseMutex();
                    appMutex.Close();
                }
            }
        }
    }

    #region Display & HDR Controller (Win32 CCD API)
    public class HdrController
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public uint refreshRateNumerator;
            public uint refreshRateDenominator;
            public uint scanLineOrdering;
            public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_MODE_INFO
        {
            public uint infoType;
            public uint id;
            public LUID adapterId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] details;
        }

        public enum DISPLAYCONFIG_DEVICE_INFO_TYPE : uint
        {
            GET_ADVANCED_COLOR_INFO = 9,
            SET_ADVANCED_COLOR_STATE = 10,
            GET_ADVANCED_COLOR_INFO_2 = 15,
            SET_HDR_STATE = 16
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        // Legacy struct for Windows 10 & earlier Windows 11 builds (< 24H2)
        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;
            public uint colorEncoding;
            public uint bitsPerColorChannel;
        }

        // Modern struct for Windows 11 24H2+ (decouples HDR from WCG/ACM)
        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;
            public uint colorEncoding;
            public uint bitsPerColorChannel;
            public uint activeColorMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SET_HDR_STATE
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;
        }

        [DllImport("user32.dll")]
        public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        public static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathInfoArray, ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 requestPacket);

        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
        public static extern int DisplayConfigGetDeviceInfoLegacy(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_HDR_STATE setPacket);

        public const uint QDC_ONLY_ACTIVE_PATHS = 2;

        public class MonitorInfo
        {
            public int Index { get; set; }
            public LUID AdapterId { get; set; }
            public uint TargetId { get; set; }
            public bool Supported { get; set; }
            public bool Enabled { get; set; }
            public bool UsesModernHdrApi { get; set; }
        }

        public static List<MonitorInfo> GetMonitors()
        {
            var list = new List<MonitorInfo>();
            uint pathCount, modeCount;
            int status = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount);
            if (status != 0 || pathCount == 0) return list;

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            status = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (status != 0) return list;

            for (int i = 0; i < pathCount; i++)
            {
                var target = paths[i].targetInfo;

                // 1. Try modern Windows 11 24H2+ API (Type 15) first.
                // Decouples true HDR state from ACM/WCG (Automatic Color Management).
                var pModern = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2();
                pModern.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_ADVANCED_COLOR_INFO_2;
                pModern.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2));
                pModern.header.adapterId = target.adapterId;
                pModern.header.id = target.id;

                int modernRes = DisplayConfigGetDeviceInfo(ref pModern);
                if (modernRes == 0)
                {
                    // Bit 4 (0x10): highDynamicRangeSupported
                    // Bit 5 (0x20): highDynamicRangeUserEnabled
                    bool supported = (pModern.value & 0x10) != 0;
                    bool enabled = (pModern.value & 0x20) != 0;

                    list.Add(new MonitorInfo
                    {
                        Index = i + 1,
                        AdapterId = target.adapterId,
                        TargetId = target.id,
                        Supported = supported,
                        Enabled = enabled,
                        UsesModernHdrApi = true
                    });
                    continue;
                }

                // 2. Fallback to legacy API (Type 9) for Windows 10 and earlier Windows 11 builds
                var pLegacy = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                pLegacy.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_ADVANCED_COLOR_INFO;
                pLegacy.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO));
                pLegacy.header.adapterId = target.adapterId;
                pLegacy.header.id = target.id;

                int legacyRes = DisplayConfigGetDeviceInfoLegacy(ref pLegacy);
                if (legacyRes == 0)
                {
                    // Bit 0 (0x1): advancedColorSupported
                    // Bit 1 (0x2): advancedColorEnabled
                    bool supported = (pLegacy.value & 0x1) != 0;
                    bool enabled = (pLegacy.value & 0x2) != 0;

                    list.Add(new MonitorInfo
                    {
                        Index = i + 1,
                        AdapterId = target.adapterId,
                        TargetId = target.id,
                        Supported = supported,
                        Enabled = enabled,
                        UsesModernHdrApi = false
                    });
                }
            }
            return list;
        }

        public static bool IsAnyHdrEnabled(out bool anySupported)
        {
            var monitors = GetMonitors();
            anySupported = monitors.Any(m => m.Supported);
            return monitors.Any(m => m.Supported && m.Enabled);
        }

        public static void SetAllHdr(bool enable)
        {
            var monitors = GetMonitors().Where(m => m.Supported).ToList();
            if (monitors.Count == 0) return;

            foreach (var m in monitors)
            {
                if (m.UsesModernHdrApi)
                {
                    // Windows 11 24H2+: use type 16 (SET_HDR_STATE)
                    var p16 = new DISPLAYCONFIG_SET_HDR_STATE();
                    p16.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.SET_HDR_STATE;
                    p16.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SET_HDR_STATE));
                    p16.header.adapterId = m.AdapterId;
                    p16.header.id = m.TargetId;
                    p16.value = enable ? 1u : 0u;

                    DisplayConfigSetDeviceInfo(ref p16);
                }
                else
                {
                    // Windows 10 & earlier Windows 11: use type 10 (SET_ADVANCED_COLOR_STATE)
                    var p10 = new DISPLAYCONFIG_SET_HDR_STATE();
                    p10.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.SET_ADVANCED_COLOR_STATE;
                    p10.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SET_HDR_STATE));
                    p10.header.adapterId = m.AdapterId;
                    p10.header.id = m.TargetId;
                    p10.value = enable ? 1u : 0u;

                    DisplayConfigSetDeviceInfo(ref p10);
                }
            }
        }
    }
    #endregion

    #region Tray Application Context
    public class TrayAppContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem itemOn;
        private ToolStripMenuItem itemOff;
        private ToolStripMenuItem itemConfig;
        private ToolStripMenuItem itemExit;
        private System.Windows.Forms.Timer syncTimer;
        private DateTime lastToggleTime = DateTime.MinValue;
        private Icon iconOn;
        private Icon iconOff;
        private SettingsForm settingsForm;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public TrayAppContext()
        {
            InitializeIcons();
            InitializeContextMenu();

            trayIcon = new NotifyIcon
            {
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            trayIcon.MouseClick += TrayIcon_MouseClick;

            // Listen for system display settings changes (e.g. Win+Alt+B or Windows Settings app)
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            // Background timer to ensure periodic state synchronization every 2 seconds
            syncTimer = new System.Windows.Forms.Timer();
            syncTimer.Interval = 2000;
            syncTimer.Tick += (s, e) => UpdateTrayState();
            syncTimer.Start();

            // Initial state update
            UpdateTrayState();
        }

        private void InitializeIcons()
        {
            int iconSize = SystemInformation.SmallIconSize.Width;
            if (iconSize < 16) iconSize = 16;
            if (iconSize > 48) iconSize = 32;

            iconOn = CreateDynamicIcon(true, iconSize);
            iconOff = CreateDynamicIcon(false, iconSize);
        }

        private Icon CreateDynamicIcon(bool enabled, int size)
        {
            using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    float pad = size * 0.08f;
                    RectangleF rect = new RectangleF(pad, pad * 1.4f, size - pad * 2, size - pad * 2.8f);
                    float radius = size * 0.22f;

                    using (GraphicsPath path = GetRoundedRect(rect, radius))
                    {
                        if (enabled)
                        {
                            // Vibrant blue/cyan gradient when HDR is ON
                            using (LinearGradientBrush brush = new LinearGradientBrush(rect, Color.FromArgb(0, 120, 245), Color.FromArgb(0, 205, 255), 45f))
                            {
                                g.FillPath(brush, path);
                            }
                        }
                        else
                        {
                            // Dark slate gray background when HDR is OFF
                            using (SolidBrush brush = new SolidBrush(Color.FromArgb(75, 80, 90)))
                            {
                                g.FillPath(brush, path);
                            }
                            using (Pen pen = new Pen(Color.FromArgb(115, 120, 130), 1f))
                            {
                                g.DrawPath(pen, path);
                            }
                        }
                    }

                    // "HDR" text
                    float fontSize = size * 0.32f;
                    using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    })
                    {
                        Color textColor = enabled ? Color.White : Color.FromArgb(205, 210, 215);
                        using (SolidBrush textBrush = new SolidBrush(textColor))
                        {
                            RectangleF textRect = new RectangleF(0, rect.Y - 1, size, rect.Height);
                            g.DrawString("HDR", font, textBrush, textRect, sf);
                        }
                    }

                    // Status indicator dot
                    float dotSize = size * 0.26f;
                    RectangleF dotRect = new RectangleF(size - dotSize - 1, size - dotSize - 1, dotSize, dotSize);
                    Color dotColor = enabled ? Color.FromArgb(46, 204, 113) : Color.FromArgb(150, 150, 150);
                    using (SolidBrush dotBrush = new SolidBrush(dotColor))
                    {
                        g.FillEllipse(dotBrush, dotRect);
                    }
                    using (Pen dotPen = new Pen(Color.FromArgb(30, 30, 30), 1f))
                    {
                        g.DrawEllipse(dotPen, dotRect);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(hIcon))
                    {
                        return (Icon)temp.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        private GraphicsPath GetRoundedRect(RectangleF bounds, float radius)
        {
            float diameter = radius * 2;
            SizeF size = new SizeF(diameter, diameter);
            RectangleF arc = new RectangleF(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void InitializeContextMenu()
        {
            contextMenu = new ContextMenuStrip();
            contextMenu.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Localized options following Windows display language
            itemOn = new ToolStripMenuItem(Loc.MenuOn, null, (s, e) => SetHdr(true));
            itemOff = new ToolStripMenuItem(Loc.MenuOff, null, (s, e) => SetHdr(false));
            itemConfig = new ToolStripMenuItem(Loc.MenuSettings, null, (s, e) => ShowSettings());
            itemExit = new ToolStripMenuItem(Loc.MenuExit, null, (s, e) => ExitApplication());

            contextMenu.Items.Add(itemOn);
            contextMenu.Items.Add(itemOff);
            contextMenu.Items.Add(itemConfig);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(itemExit);

            contextMenu.Opening += (s, e) => UpdateTrayState();
        }

        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            // Left click toggles HDR
            if (e.Button == MouseButtons.Left)
            {
                if ((DateTime.Now - lastToggleTime).TotalMilliseconds < 600)
                {
                    return; // Prevent rapid accidental double clicking
                }
                lastToggleTime = DateTime.Now;

                bool anySupported;
                bool isCurrentlyOn = HdrController.IsAnyHdrEnabled(out anySupported);

                if (!anySupported)
                {
                    trayIcon.ShowBalloonTip(3000, Loc.AppTitle, Loc.BalloonNoHdrMsg, ToolTipIcon.Warning);
                    return;
                }

                // Toggle: if currently ON -> turn OFF; if currently OFF -> turn ON
                SetHdr(!isCurrentlyOn);
            }
        }

        private void SetHdr(bool enable)
        {
            bool anySupported;
            bool isCurrentlyOn = HdrController.IsAnyHdrEnabled(out anySupported);

            if (!anySupported)
            {
                trayIcon.ShowBalloonTip(3000, Loc.AppTitle, Loc.BalloonNoHdrMsg, ToolTipIcon.Warning);
                return;
            }

            if (isCurrentlyOn != enable)
            {
                HdrController.SetAllHdr(enable);

                // Brief delay allowing display driver to switch color pipeline
                Thread.Sleep(300);
            }

            UpdateTrayState();
        }

        private void UpdateTrayState()
        {
            try
            {
                bool anySupported;
                bool isOn = HdrController.IsAnyHdrEnabled(out anySupported);

                if (!anySupported)
                {
                    trayIcon.Icon = iconOff;
                    trayIcon.Text = Loc.TooltipNotSupported;
                    itemOn.Checked = false;
                    itemOn.Enabled = false;
                    itemOff.Checked = false;
                    itemOff.Enabled = false;
                }
                else
                {
                    trayIcon.Icon = isOn ? iconOn : iconOff;
                    trayIcon.Text = isOn ? Loc.TooltipOn : Loc.TooltipOff;
                    itemOn.Enabled = true;
                    itemOff.Enabled = true;
                    itemOn.Checked = isOn;
                    itemOff.Checked = !isOn;
                }
            }
            catch
            {
                // Silent catch to preserve tray stability
            }
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            UpdateTrayState();
        }

        private void ShowSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm();
            }

            if (!settingsForm.Visible)
            {
                settingsForm.ShowDialog();
            }
            else
            {
                settingsForm.Activate();
            }

            UpdateTrayState();
        }

        private void ExitApplication()
        {
            syncTimer.Stop();
            syncTimer.Dispose();
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            if (iconOn != null) iconOn.Dispose();
            if (iconOff != null) iconOff.Dispose();

            Application.Exit();
        }
    }
    #endregion

    #region Settings Dialog
    public class SettingsForm : Form
    {
        private const string RunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "HDRSwitchPortable";

        private CheckBox chkAutoStart;
        private Label lblStatusMonitors;
        private Button btnSave;
        private Button btnCancel;

        public SettingsForm()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void InitializeComponent()
        {
            this.Text = Loc.SettingsTitle;
            this.Size = new Size(420, 260);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = true;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            // Top Header Panel
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 55,
                BackColor = Color.White
            };
            Label lblHeader = new Label
            {
                Text = Loc.SettingsHeader,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Location = new Point(18, 14),
                AutoSize = true
            };
            Label lblHeaderSub = new Label
            {
                Text = Loc.SettingsSubHeader,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                ForeColor = Color.FromArgb(110, 110, 110),
                Location = new Point(19, 34),
                AutoSize = true
            };
            headerPanel.Controls.Add(lblHeader);
            headerPanel.Controls.Add(lblHeaderSub);

            // Divider Line
            Panel divider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(222, 226, 230)
            };

            // Startup Group
            GroupBox grpStartup = new GroupBox
            {
                Text = Loc.GroupStartup,
                Location = new Point(18, 68),
                Size = new Size(368, 68),
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            chkAutoStart = new CheckBox
            {
                Text = Loc.ChkAutoStart,
                Location = new Point(16, 22),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 20, 20)
            };

            Label lblAutoStartInfo = new Label
            {
                Text = Loc.LblAutoStartInfo,
                Location = new Point(34, 43),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 100, 100)
            };

            grpStartup.Controls.Add(chkAutoStart);
            grpStartup.Controls.Add(lblAutoStartInfo);

            // Monitor Status Label
            lblStatusMonitors = new Label
            {
                Location = new Point(20, 145),
                Size = new Size(365, 30),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            // Action Buttons
            btnSave = new Button
            {
                Text = Loc.BtnSave,
                DialogResult = DialogResult.OK,
                Size = new Size(85, 28),
                Location = new Point(210, 180),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = Loc.BtnCancel,
                DialogResult = DialogResult.Cancel,
                Size = new Size(85, 28),
                Location = new Point(302, 180),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(40, 40, 40),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(btnCancel);
            this.Controls.Add(btnSave);
            this.Controls.Add(lblStatusMonitors);
            this.Controls.Add(grpStartup);
            this.Controls.Add(divider);
            this.Controls.Add(headerPanel);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void LoadCurrentSettings()
        {
            try
            {
                // Check Windows Registry for auto-start with user login
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistrySubKey, false))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(AppRegistryName);
                        if (value != null)
                        {
                            chkAutoStart.Checked = true;
                        }
                    }
                }

                // Detect connected monitors and genuine HDR state
                var monitors = HdrController.GetMonitors();
                var hdrMonitors = monitors.Where(m => m.Supported).ToList();
                if (hdrMonitors.Count > 0)
                {
                    bool isAnyOn = hdrMonitors.Any(m => m.Enabled);
                    lblStatusMonitors.Text = Loc.MonitorStatus(hdrMonitors.Count, isAnyOn);
                }
                else
                {
                    lblStatusMonitors.Text = Loc.NoHdrMonitorsDetected;
                }
            }
            catch (Exception ex)
            {
                lblStatusMonitors.Text = Loc.ErrorTitle + ": " + ex.Message;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistrySubKey, true))
                {
                    if (key != null)
                    {
                        if (chkAutoStart.Checked)
                        {
                            string exePath = "\"" + Application.ExecutablePath + "\"";
                            key.SetValue(AppRegistryName, exePath);
                        }
                        else
                        {
                            key.DeleteValue(AppRegistryName, false);
                        }
                    }
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.ErrorSavingStartup + ex.Message, Loc.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    #endregion
}
