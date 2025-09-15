using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;

namespace SmoothRoller
{
    public class SmoothRollerApp : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private MouseHook mouseHook;
        private SettingsForm settingsForm;
        private ScrollConfig config;
        private readonly string configPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "data", "config.json");
        private Icon currentIcon; // 保存当前图标引用以便释放
        
        // 内存优化相关
        private System.Threading.Timer _gcTimer;
        private const int GC_INTERVAL_MS = 15000; // 15秒执行一次GC
        
        public SmoothRollerApp()
        {
            InitializeComponent();
            LoadConfiguration();
            InitializeMouseHook();
            
            // 初始化定时GC机制
            _gcTimer = new System.Threading.Timer(OnPeriodicGC, null, GC_INTERVAL_MS, GC_INTERVAL_MS);
        }
        
        private void InitializeComponent()
        {
            // 创建系统托盘图标
            trayIcon = new NotifyIcon()
            {
                Icon = CreateTrayIcon(),
                Visible = true,
                Text = "SmoothRoller - 平滑滚动"
            };
            
            // 创建右键菜单
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("设置", null, OnSettings);
            contextMenu.Items.Add("-");
            var autoStartItem = (ToolStripMenuItem)contextMenu.Items.Add("开机自启", null, OnAutoStart);
            autoStartItem.Checked = IsAutoStartEnabled();
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("退出", null, OnExit);
            
            // 添加菜单关闭事件，强制垃圾回收
            contextMenu.Closed += (s, e) => {
                // 强制垃圾回收，立即释放菜单相关内存
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            };
            
            trayIcon.ContextMenuStrip = contextMenu;
            trayIcon.DoubleClick += OnSettings;
        }
        
        private Icon CreateTrayIcon()
        {
            // 尝试从assets文件夹加载图标，如果失败则创建默认图标
            try
            {
                string iconPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "assets", "app-icon.svg");
                if (File.Exists(iconPath))
                {
                    // 由于.NET不直接支持SVG，我们创建一个基于SVG设计的位图图标
                    return CreateEnhancedTrayIcon();
                }
            }
            catch { }
            
            // 创建默认图标
            return CreateDefaultTrayIcon();
        }
        
        private Icon CreateEnhancedTrayIcon()
        {
            // 基于assets中SVG设计的图标 - 与SVG文件保持一致
            Bitmap bitmap = null;
            Graphics g = null;
            IntPtr hIcon = IntPtr.Zero;
            
            try
            {
                bitmap = new Bitmap(32, 32);
                g = Graphics.FromImage(bitmap);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                // 白色圆角矩形背景 (对应SVG中的背景)
                using (var brush = new SolidBrush(Color.FromArgb(242, 255, 255, 255)))
                using (var borderPen = new Pen(Color.FromArgb(224, 224, 224), 0.5f))
                {
                    var bgRect = new Rectangle(2, 2, 28, 28);
                    using (var path = CreateRoundedRectanglePath(bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height, 6))
                    {
                        g.FillPath(brush, path);
                        g.DrawPath(borderPen, path);
                    }
                }
                
                // 鼠标主体 (蓝色渐变)
                using (var brush = new LinearGradientBrush(
                    new Point(12, 6), new Point(20, 24),
                    Color.FromArgb(74, 144, 226), Color.FromArgb(44, 95, 138)))
                using (var borderPen = new Pen(Color.FromArgb(26, 74, 107), 0.5f))
                using (var mousePath = CreateMousePath())
                {
                    g.FillPath(brush, mousePath);
                    g.DrawPath(borderPen, mousePath);
                }
                
                // 滚轮
                using (var brush = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
                using (var borderPen = new Pen(Color.FromArgb(44, 95, 138), 0.25f))
                {
                    var wheelRect = new Rectangle(15, 9, 2, 4);
                    using (var path = CreateRoundedRectanglePath(wheelRect.X, wheelRect.Y, wheelRect.Width, wheelRect.Height, 1))
                    {
                        g.FillPath(brush, path);
                        g.DrawPath(borderPen, path);
                    }
                }
                
                // 滚轮中心线
                using (var pen = new Pen(Color.FromArgb(44, 95, 138), 0.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, 16, 10, 16, 12);
                }
                
                // 左侧水流效果
                using (var pen = new Pen(Color.FromArgb(120, 135, 206, 235), 1f))
                {
                    g.DrawCurve(pen, new Point[] { new Point(8, 10), new Point(9, 15), new Point(8, 20), new Point(7, 25) });
                }
                
                // 右侧水流效果
                using (var pen = new Pen(Color.FromArgb(120, 135, 206, 235), 1f))
                {
                    g.DrawCurve(pen, new Point[] { new Point(24, 10), new Point(23, 15), new Point(24, 20), new Point(25, 25) });
                }
                
                // 中央流线
                using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 0.75f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, 16, 4, 16, 7);
                    g.DrawLine(pen, 16, 25, 16, 28);
                }
                
                // 获取图标句柄并创建Icon
                hIcon = bitmap.GetHicon();
                return Icon.FromHandle(hIcon);
            }
            finally
            {
                // 确保资源正确释放
                g?.Dispose();
                bitmap?.Dispose();
            }
        }
        
        private GraphicsPath CreateMousePath()
        {
            var path = new GraphicsPath();
            // 创建鼠标形状路径 (对应SVG中的鼠标主体)
            var mouseRect = new Rectangle(12, 8, 8, 16);
            path.AddPath(CreateRoundedRectanglePath(mouseRect.X, mouseRect.Y, mouseRect.Width, mouseRect.Height, 2), false);
            return path;
        }
        
        private GraphicsPath CreateRoundedRectanglePath(int x, int y, int width, int height, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, radius * 2, radius * 2, 180, 90);
            path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
            path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
        
        private Icon CreateDefaultTrayIcon()
        {
            // 创建简单的默认图标
            Bitmap bitmap = null;
            Graphics g = null;
            Font font = null;
            IntPtr hIcon = IntPtr.Zero;
            
            try
            {
                bitmap = new Bitmap(16, 16);
                g = Graphics.FromImage(bitmap);
                font = new Font("Arial", 8, FontStyle.Bold);
                
                g.Clear(Color.Transparent);
                g.FillEllipse(Brushes.DodgerBlue, 2, 2, 12, 12);
                g.DrawString("S", font, Brushes.White, 4, 2);
                
                hIcon = bitmap.GetHicon();
                return Icon.FromHandle(hIcon);
            }
            finally
            {
                // 确保资源正确释放
                font?.Dispose();
                g?.Dispose();
                bitmap?.Dispose();
            }
        }
        
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<ScrollConfig>(json) ?? new ScrollConfig();
                }
                else
                {
                    config = new ScrollConfig();
                    SaveConfiguration();
                }
            }
            catch
            {
                config = new ScrollConfig();
                SaveConfiguration();
            }
        }
        
        private void SaveConfiguration()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void InitializeMouseHook()
        {
            mouseHook = new MouseHook(config);
            mouseHook.InstallHook();
        }
        
        private void OnSettings(object sender, EventArgs e)
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(config);
                settingsForm.ConfigChanged += (s, args) => {
                    SaveConfiguration();
                    mouseHook.UpdateConfig(config);
                };
                
                // 添加窗口关闭事件处理，确保资源正确释放
                settingsForm.FormClosed += (s, args) => {
                    settingsForm?.Dispose();
                    settingsForm = null;
                    // 强制垃圾回收，立即释放内存
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                };
            }
            else
            {
                // 确保使用最新的配置值
                settingsForm.UpdateConfig(config);
            }
            settingsForm.Show();
            settingsForm.BringToFront();
        }
        
        private void OnAutoStart(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem.Checked)
            {
                DisableAutoStart();
                menuItem.Checked = false;
            }
            else
            {
                EnableAutoStart();
                menuItem.Checked = true;
            }
        }
        
        private void OnExit(object sender, EventArgs e)
        {
            mouseHook?.Dispose();
            trayIcon.Visible = false;
            Application.Exit();
        }
        
        private bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key?.GetValue("SmoothRoller") != null;
                }
            }
            catch
            {
                return false;
            }
        }
        
        private void EnableAutoStart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.SetValue("SmoothRoller", Application.ExecutablePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置开机自启失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void DisableAutoStart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.DeleteValue("SmoothRoller", false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"取消开机自启失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// 定时GC回调方法
        /// </summary>
        private void OnPeriodicGC(object state)
        {
            try
            {
                // 执行分代垃圾回收，优先清理短期对象
                GC.Collect(0, GCCollectionMode.Optimized);
                GC.WaitForPendingFinalizers();
                
                // 如果内存压力较大，执行完整GC
                var memoryBefore = GC.GetTotalMemory(false);
                if (memoryBefore > 15 * 1024 * 1024) // 超过15MB时执行完整GC
                {
                    GC.Collect(2, GCCollectionMode.Forced);
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
                else if (memoryBefore > 12 * 1024 * 1024) // 超过12MB时执行优化GC
                {
                    GC.Collect(1, GCCollectionMode.Optimized);
                    GC.WaitForPendingFinalizers();
                }
            }
            catch
            {
                // 忽略GC过程中的异常
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 停止并释放GC定时器
                _gcTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                _gcTimer?.Dispose();
                _gcTimer = null;
                
                mouseHook?.Dispose();
                trayIcon?.Dispose();
                currentIcon?.Dispose();
                settingsForm?.Dispose();
                settingsForm = null;
                
                // 强制垃圾回收，立即释放所有资源
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            base.Dispose(disposing);
        }
    }
}