using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SmoothRoller
{
    public class MouseHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_MOUSEHWHEEL = 0x020E;
        
        private readonly LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private ScrollConfig _config;
        private ScrollConfig _currentAppConfig;  // 当前应用的优化配置
        private System.Threading.Timer _scrollTimer;
        private double _targetDistance = 0;
        private double _scrolledDistance = 0;
        private DateTime _animationStartTime = DateTime.Now;
        private DateTime _lastScrollTime = DateTime.Now;
        private readonly object _lockObject = new object();
        private bool _isScrolling = false;
        private bool _disposed = false;
        
        // 自适应帧率相关
        private int _currentUpdateInterval = 16;
        private DateTime _lastFrameTime = DateTime.Now;
        private double _lastFrameDistance = 0;
        
        // 缓存系统窗口检测结果，避免重复API调用
        private static readonly ConcurrentDictionary<IntPtr, bool> _systemWindowCache = new ConcurrentDictionary<IntPtr, bool>();
        private static readonly StringBuilder _classNameBuffer = new StringBuilder(256);
        private static IntPtr _lastForegroundWindow = IntPtr.Zero;
        private static bool _lastIsSystemWindow = false;
        private static DateTime _lastWindowCheck = DateTime.MinValue;
        
        // 滚动事件发送优化缓存
        private static POINT _lastCursorPos;
        private static IntPtr _lastTargetWindow = IntPtr.Zero;
        private static DateTime _lastCursorCheck = DateTime.MinValue;
        private static readonly object _cursorCacheLock = new object();
        
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
        
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        public MouseHook(ScrollConfig config)
        {
            _config = config;
            _currentAppConfig = config;  // 初始使用默认配置
            _proc = HookCallback;
            _scrollTimer = new System.Threading.Timer(OnScrollTimer, null, Timeout.Infinite, Timeout.Infinite);
        }
        
        public void InstallHook()
        {
            _hookID = SetHook(_proc);
        }
        
        public void UninstallHook()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    UninstallHook();
                    
                    lock (_lockObject)
                    {
                        _isScrolling = false;
                        _scrollTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                        _scrollTimer?.Dispose();
                        _scrollTimer = null;
                    }
                }
                _disposed = true;
            }
        }
        
        public void UpdateConfig(ScrollConfig config)
        {
            lock (_lockObject)
            {
                _config = config;
            }
        }
        
        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }
        
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !_disposed)
            {
                if (wParam == (IntPtr)WM_MOUSEWHEEL || wParam == (IntPtr)WM_MOUSEHWHEEL)
                {
                    // 基于前台窗口焦点判断是否为系统窗口，避免频繁的鼠标位置检测
                    if (IsForegroundWindowSystem())
                    {
                        // 对于系统窗口，让原始滚轮事件通过，不进行平滑滚动处理
                        return CallNextHookEx(_hookID, nCode, wParam, lParam);
                    }
                    
                    // 快速检查配置状态，避免不必要的锁
                    if (_config?.EnableSmoothScroll == true)
                    {
                        var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                        var delta = (short)((hookStruct.mouseData >> 16) & 0xffff);
                        
                        ProcessSmoothScroll(delta, wParam == (IntPtr)WM_MOUSEHWHEEL);
                        
                        // 阻止原始滚动事件
                        return (IntPtr)1;
                    }
                }
            }
            
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        
        private void ProcessSmoothScroll(short delta, bool isHorizontal)
        {
            var now = DateTime.Now;
            var timeDelta = (now - _lastScrollTime).TotalMilliseconds;
            _lastScrollTime = now;
            
            // 统一使用用户自定义配置，不区分应用类型
            _currentAppConfig = _config;
            
            // 计算滚动方向
            var scrollDirection = delta > 0 ? 1 : -1;
            if (_currentAppConfig.ReverseScrollDirection)
                scrollDirection *= -1;
            
            // 计算基础滚动距离（使用当前应用的步长）
            var baseScrollLines = Math.Abs(delta) / 120.0; // 标准滚轮一格是120
            var scrollDistance = baseScrollLines * _currentAppConfig.StepSize * scrollDirection;
            
            // 应用加速度（快速连续滚动时）
            if (timeDelta < 100 && _isScrolling)
            {
                var accelerationFactor = Math.Min(_currentAppConfig.AccelerationMax, 
                    1.0 + (_currentAppConfig.AccelerationDelta / 100.0) * (100 - timeDelta) / 100.0);
                scrollDistance *= accelerationFactor;
            }
            
            // 改进：同向滚动时重置动画起点并承接剩余距离，避免临近尾声时“发力不足”的停顿感
            if (_isScrolling)
            {
                // 计算当前已经到达的位置与剩余距离
                var elapsed = (now - _animationStartTime).TotalMilliseconds;
                var progress = Math.Min(1.0, elapsed / _currentAppConfig.AnimationTime);
                var eased = EaseOutQuart(progress, _currentAppConfig.TailToHeadRatio);
                var currentPos = _targetDistance * eased;
                var remaining = _targetDistance - currentPos;

                var remainingDir = remaining >= 0 ? 1 : -1;
                var newDir = scrollDirection;

                if (remainingDir != newDir)
                {
                    // 方向相反：丢弃剩余距离，从0开始新的动画
                    _targetDistance = scrollDistance;
                }
                else
                {
                    // 方向相同：把“尚未完成的剩余距离”与新的滚动距离合并，并从当前位置重新开始
                    _targetDistance = remaining + scrollDistance;
                }

                _scrolledDistance = 0;
                _animationStartTime = now;
            }
            else
            {
                // 开始新的滚动动画
                _targetDistance = scrollDistance;
                _scrolledDistance = 0;
                _animationStartTime = now;
                _isScrolling = true;
            }
            
            // 计算自适应更新间隔
            if (_currentAppConfig.EnableAdaptiveFrameRate)
            {
                // 根据滚动距离动态调整帧率
                var scrollMagnitude = Math.Abs(scrollDistance);
                if (scrollMagnitude > _currentAppConfig.StepSize * 2)
                {
                    // 大幅滚动时使用较高帧率
                    _currentUpdateInterval = _currentAppConfig.MinUpdateInterval;
                }
                else if (scrollMagnitude < _currentAppConfig.StepSize * 0.5)
                {
                    // 小幅滚动时使用较低帧率
                    _currentUpdateInterval = _currentAppConfig.MaxUpdateInterval;
                }
                else
                {
                    // 中等滚动使用中等帧率
                    _currentUpdateInterval = (_currentAppConfig.MinUpdateInterval + _currentAppConfig.MaxUpdateInterval) / 2;
                }
            }
            else
            {
                _currentUpdateInterval = 16; // 默认60FPS
            }
            
            // 启动平滑滚动定时器
            _scrollTimer.Change(0, _currentUpdateInterval);
        }
        
        private void OnScrollTimer(object state)
        {
            // 快速检查是否还在滚动状态，避免不必要的锁开销
            if (!_isScrolling) return;
            
            lock (_lockObject)
            {
                if (!_isScrolling || Math.Abs(_targetDistance) < _currentAppConfig.MinScrollThreshold)
                {
                    _isScrolling = false;
                    _scrollTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    return;
                }
                
                var now = DateTime.Now;
                var elapsed = (now - _animationStartTime).TotalMilliseconds;
                
                // 检查动画是否完成
                if (elapsed >= _currentAppConfig.AnimationTime)
                {
                    _isScrolling = false;
                    _scrollTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    return;
                }
                
                var progress = elapsed / _currentAppConfig.AnimationTime;
                
                // 使用缓动函数计算当前位置
                var easedProgress = EaseOutQuart(progress, _currentAppConfig.TailToHeadRatio);
                var targetPosition = _targetDistance * easedProgress;
                var frameDistance = targetPosition - _scrolledDistance;
                
                // 自适应帧率优化：根据帧间距离调整更新频率
                if (_currentAppConfig.EnableAdaptiveFrameRate)
                {
                    var frameTime = (now - _lastFrameTime).TotalMilliseconds;
                    var distanceChange = Math.Abs(frameDistance - _lastFrameDistance);
                    
                    // 如果变化很小，可以降低更新频率
                    if (distanceChange < _currentAppConfig.MinScrollThreshold * 0.5 && frameTime < _currentUpdateInterval * 1.5)
                    {
                        _currentUpdateInterval = Math.Min(_currentAppConfig.MaxUpdateInterval, _currentUpdateInterval + 2);
                    }
                    else if (distanceChange > _currentAppConfig.MinScrollThreshold * 2)
                    {
                        _currentUpdateInterval = Math.Max(_currentAppConfig.MinUpdateInterval, _currentUpdateInterval - 2);
                    }
                    
                    _lastFrameTime = now;
                    _lastFrameDistance = frameDistance;
                }
                
                if (Math.Abs(frameDistance) >= _currentAppConfig.MinScrollThreshold)
                {
                    // 发送滚动事件
                    SendScrollEvent((int)(frameDistance * 120 / _currentAppConfig.StepSize));
                    _scrolledDistance = targetPosition;
                }
                
                // 动态调整定时器间隔
                if (_currentAppConfig.EnableAdaptiveFrameRate)
                {
                    _scrollTimer.Change(_currentUpdateInterval, _currentUpdateInterval);
                }
            }
        }
        
        private static double EaseOutQuart(double t, int ratio)
        {
            // 实现减速/加速比的缓动函数
            var accelerationTime = 1.0 / (1.0 + ratio);
            
            if (t <= accelerationTime)
            {
                // 加速阶段：二次缓入
                var normalizedT = t / accelerationTime;
                return normalizedT * normalizedT * accelerationTime;
            }
            else
            {
                // 减速阶段：四次缓出
                var normalizedT = (t - accelerationTime) / (1.0 - accelerationTime);
                var decelerated = 1.0 - Math.Pow(1.0 - normalizedT, 4);
                return accelerationTime + decelerated * (1.0 - accelerationTime);
            }
        }
        
        /// <summary>
        /// 基于前台窗口焦点判断是否为系统窗口，避免频繁的鼠标位置检测
        /// </summary>
        private static bool IsForegroundWindowSystem()
        {
            var now = DateTime.Now;
            var foregroundWindow = GetForegroundWindow();
            
            // 缓存检测结果，避免频繁API调用
            if (foregroundWindow == _lastForegroundWindow && 
                (now - _lastWindowCheck).TotalMilliseconds < 100)
            {
                return _lastIsSystemWindow;
            }
            
            _lastForegroundWindow = foregroundWindow;
            _lastWindowCheck = now;
            
            if (foregroundWindow == IntPtr.Zero)
            {
                _lastIsSystemWindow = false;
                return false;
            }
            
            // 使用缓存避免重复检测
            if (_systemWindowCache.TryGetValue(foregroundWindow, out bool cached))
            {
                _lastIsSystemWindow = cached;
                return cached;
            }
            
            bool isSystem = IsSystemWindow(foregroundWindow);
            _systemWindowCache.TryAdd(foregroundWindow, isSystem);
            _lastIsSystemWindow = isSystem;
            
            return isSystem;
        }
        
        private static bool IsSystemWindow(IntPtr hWnd)
        {
            try
            {
                // 重用StringBuilder避免频繁分配
                _classNameBuffer.Clear();
                if (GetClassName(hWnd, _classNameBuffer, _classNameBuffer.Capacity) > 0)
                {
                    string name = _classNameBuffer.ToString();
                    
                    // 系统设置界面的窗口类名通常包含这些特征
                    return name.Contains("ApplicationFrame") || 
                           name.Contains("Windows.UI.Core") ||
                           name.Contains("Windows.UI.Input") ||
                           name.Contains("Shell_") ||
                           name.Contains("IME") ||
                           name.StartsWith("#") ||
                           name.StartsWith("Windows.") ||
                           name.Contains("SystemSettings") ||
                           name.Contains("ControlPanel");
                }
            }
            catch
            {
                // 忽略异常
            }
            return false;
        }
        
        /// <summary>
        /// 检测当前前台窗口的应用类型
        /// </summary>
        private static ApplicationType DetectApplicationType(IntPtr hWnd)
        {
            try
            {
                _classNameBuffer.Clear();
                if (GetClassName(hWnd, _classNameBuffer, _classNameBuffer.Capacity) > 0)
                {
                    string className = _classNameBuffer.ToString();
                    
                    // 检测代码编辑器 - 更精确的检测逻辑
                    if (className.Contains("SunAwtFrame") ||  // IDEA主窗口 (Java Swing)
                        className.Contains("IDEA_") ||
                        className.Contains("IntelliJ") ||
                        className.Contains("JetBrains") ||
                        className.Contains("Afx:400000:0") ||  // Visual Studio 2019/2022
                        className.Contains("Afx:00400000:0") || // Visual Studio变体
                        className.Contains("HwndWrapper[DefaultDomain") || // WPF应用(可能是VS)
                        className.Contains("Notepad++") ||
                        className.Contains("Qt5QWindowIcon") || // Qt应用(可能是某些编辑器)
                        className.Equals("Chrome_WidgetWin_1") && IsLikelyVSCode(hWnd) ||
                        className.Contains("Electron") || // Electron应用
                        className.Contains("ApplicationFrameWindow") && IsLikelyCodeEditor(hWnd))
                    {
                        return ApplicationType.CodeEditor;
                    }
                    
                    // 检测浏览器
                    if (className.Contains("Chrome_WidgetWin_1") ||
                        className.Contains("MozillaWindowClass") ||
                        className.Contains("ApplicationFrameWindow") && IsBrowser(hWnd))
                    {
                        return ApplicationType.Browser;
                    }
                    
                    // 检测Office应用
                    if (className.Contains("OpusApp") ||  // Word
                        className.Contains("XLMAIN") ||   // Excel
                        className.Contains("PP") ||       // PowerPoint
                        className.Contains("rctrl_renwnd32"))  // Office通用
                    {
                        return ApplicationType.Office;
                    }
                }
            }
            catch
            {
                // 忽略异常
            }
            
            return ApplicationType.Default;
        }
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        
        private static bool IsLikelyVSCode(IntPtr hWnd)
        {
            try
            {
                var titleBuffer = new StringBuilder(256);
                if (GetWindowText(hWnd, titleBuffer, titleBuffer.Capacity) > 0)
                {
                    string title = titleBuffer.ToString();
                    return title.Contains("Visual Studio Code") || 
                           title.Contains("VSCode") ||
                           title.EndsWith(".js - Visual Studio Code") ||
                           title.EndsWith(".ts - Visual Studio Code") ||
                           title.EndsWith(".py - Visual Studio Code") ||
                           title.EndsWith(".cs - Visual Studio Code");
                }
            }
            catch { }
            return false;
        }
        
        private static bool IsLikelyCodeEditor(IntPtr hWnd)
        {
            try
            {
                var titleBuffer = new StringBuilder(256);
                if (GetWindowText(hWnd, titleBuffer, titleBuffer.Capacity) > 0)
                {
                    string title = titleBuffer.ToString();
                    return title.Contains("Visual Studio Code") ||
                           title.Contains("IntelliJ IDEA") ||
                           title.Contains("PyCharm") ||
                           title.Contains("WebStorm") ||
                           title.Contains("Android Studio") ||
                           title.Contains("CLion") ||
                           title.Contains("PhpStorm") ||
                           title.Contains("RubyMine") ||
                           title.Contains("GoLand") ||
                           title.Contains("Rider") ||
                           title.Contains("DataGrip");
                }
            }
            catch { }
            return false;
        }
        
        private static bool IsBrowser(IntPtr hWnd)
        {
            try
            {
                var titleBuffer = new StringBuilder(256);
                if (GetWindowText(hWnd, titleBuffer, titleBuffer.Capacity) > 0)
                {
                    string title = titleBuffer.ToString();
                    return title.Contains("Google Chrome") ||
                           title.Contains("Mozilla Firefox") ||
                           title.Contains("Microsoft Edge") ||
                           title.Contains("Safari") ||
                           title.EndsWith(" - Chrome") ||
                           title.EndsWith(" - Firefox") ||
                           title.EndsWith(" - Edge");
                }
            }
            catch { }
            return false;
        }
        
        private void SendScrollEvent(int delta)
        {
            if (delta == 0 || _disposed) return;
            
            try
            {
                POINT cursorPos;
                IntPtr targetWindow;
                var now = DateTime.Now;
                
                // 使用缓存减少API调用频率
                lock (_cursorCacheLock)
                {
                    var timeSinceLastCheck = (now - _lastCursorCheck).TotalMilliseconds;
                    
                    // 如果距离上次检查时间很短，使用缓存的位置和窗口
                    if (timeSinceLastCheck < 50 && _lastTargetWindow != IntPtr.Zero)
                    {
                        cursorPos = _lastCursorPos;
                        targetWindow = _lastTargetWindow;
                    }
                    else
                    {
                        // 获取新的鼠标位置和目标窗口
                        if (!GetCursorPos(out cursorPos))
                            return;
                        
                        targetWindow = WindowFromPoint(cursorPos);
                        if (targetWindow == IntPtr.Zero)
                            return;
                        
                        // 更新缓存
                        _lastCursorPos = cursorPos;
                        _lastTargetWindow = targetWindow;
                        _lastCursorCheck = now;
                    }
                }
                
                // 发送平滑滚动事件到目标窗口
                var wParam = (IntPtr)((delta << 16) | 0x0000);
                var lParam = (IntPtr)((cursorPos.y << 16) | (cursorPos.x & 0xFFFF));
                
                // 使用PostMessage而不是SendMessage，避免阻塞
                PostMessage(targetWindow, (uint)WM_MOUSEWHEEL, wParam, lParam);
            }
            catch
            {
                // 忽略发送失败的情况，但清除缓存以防止使用无效数据
                lock (_cursorCacheLock)
                {
                    _lastTargetWindow = IntPtr.Zero;
                }
            }
        }
    }
}