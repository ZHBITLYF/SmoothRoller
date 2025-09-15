<div align="center">
  <img src="assets/app-icon.svg" alt="SmoothRoller Logo" width="96" />
  <h1>SmoothRoller</h1>
  <p><b>轻 · 快 · 稳 · 省</b> 的 Windows 平滑滚动体验</p>
  <p>
    <img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%2F11-0078D6?style=flat-square&logo=windows&logoColor=white" />
    <img alt=".NET 6.0" src="https://img.shields.io/badge/.NET-6.0-512BD4?style=flat-square&logo=.net&logoColor=white" />
    <img alt="arch x64" src="https://img.shields.io/badge/arch-x64-94A3B8?style=flat-square&labelColor=334155" />
    <img alt="size <1MB" src="https://img.shields.io/badge/size-%3C1MB-8EC5FF?style=flat-square&labelColor=3B82F6" />
    <img alt="memory <30MB" src="https://img.shields.io/badge/memory-%3C30MB-8EC5FF?style=flat-square&labelColor=3B82F6" />
    <img alt="License MIT" src="https://img.shields.io/badge/license-MIT-94A3B8?style=flat-square&labelColor=334155" />
  </p>
</div>

> 🚀 SmoothRoller — 轻、快、稳、省 的 Windows 平滑滚动体验
>
> - 轻：发布包小于 1 MB（框架依赖单文件），启动即用，不占空间。
> - 快：操作丝滑、响应灵敏，滚动动画顺滑不抖动。
> - 稳：长期运行内存稳定在 30 MB 左右，GDI/句柄不攀升。
> - 省：零后台臃肿服务，按需工作，资源占用始终克制。
>
> 性能承诺（默认发布配置）：
> - 包体体积：< 1 MB（非自包含）
> - 常驻内存：< 30 MB（启动后自动收敛工作集）
> - 资源管理：托盘图标与 GDI 资源全链路释放，无句柄/GDI 渗漏
> - 使用体验：系统级平滑滚动，即装即用，毫秒级响应，动画顺滑不抖动
>
> 适合人群：追求“快·稳·省”的效率用户与极客，想要更接近 macOS 质感的 Windows 滚动体验。

### 设计原则：轻 · 快 · 稳 · 省
- 轻：单文件部署，零附加服务；开箱即用，最小惊扰。
- 快：低延迟输入处理与自适应帧率，滚动手感跟手不漂移。
- 稳：资源闭环管理与异常隔离，长时间运行不涨句柄/GDI。
- 省：按需运行与保守后台策略，避免无谓轮询与唤醒。

---

## 目录
- [功能特性](#功能特性)
- [系统要求](#系统要求)
- [安装使用](#安装使用)
- [使用说明](#使用说明)
- [技术实现](#技术实现)
- [文件结构](#文件结构)
- [注意事项](#注意事项)
- [故障排除](#故障排除)
- [许可证](#许可证)
- [贡献](#贡献)

# SmoothRoller - Windows 平滑滚动工具

一个类似 SmoothRoller 的 Windows 应用程序，让 Windows 鼠标滚轮拥有像 macOS 一样的平滑滚动体验。

## 功能特性

- ✅ **全局平滑滚动**: 对所有应用程序生效的系统级平滑滚动
- ✅ **参数调节**: 可调节滚动速度、加速度、平滑度等多项参数
- ✅ **系统托盘**: 最小化到系统托盘，不占用任务栏空间
- ✅ **开机自启**: 支持开机自动启动功能
- ✅ **配置保存**: 自动保存用户设置，重启后保持配置
- ✅ **轻量级**: 占用资源少，运行稳定

## 系统要求

- Windows 10/11
- .NET 6.0 Runtime (如果使用独立版本则不需要)

## 安装使用

### 方法一：直接运行编译好的程序
1. 运行 `SmoothRoller.exe`
2. 程序会自动最小化到系统托盘
3. 右键托盘图标可以打开设置界面

### 方法二：从源码编译
1. 确保已安装 .NET 6.0 SDK
2. 在项目目录下运行：
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
   ```
3. 运行生成的可执行文件
   ```powershell
   dist\win-x64\SmoothRoller.exe
   ```
4. 程序会自动最小化到系统托盘
5. 右键托盘图标可以打开设置界面

## 使用说明

### 基本操作
- **启动程序**: 双击运行 SmoothRoller.exe
- **打开设置**: 右键系统托盘图标 → 设置
- **开机自启**: 右键系统托盘图标 → 开机自启
- **退出程序**: 右键系统托盘图标 → 退出

### 参数说明

| 参数 | 说明 | 范围 | 默认值 |
|------|------|------|--------|
| 步长 | 单次操作鼠标窗口滑动的像素距离 | 1 - 200像素 | 90像素 |
| 动画时间 | 滚动动画效果持续的时间长度 | 100 - 2000ms | 360ms |
| 滚动加速 | 数值越高，窗口滚动也越快 | 1 - 100 | 70 |
| 最大加速倍速 | 滚动速度最高能加速到正常速度的倍数 | 1 - 20 | 7 |
| 减速/加速比 | 一次滚动中减速时间与加速时间的比值设置 | 1 - 10 | 4 |

### 高级选项
- **启用平滑滚动**: 开启/关闭平滑滚动功能
- **反转滚动方向**: 反转鼠标滚轮的滚动方向

## 技术实现

- **全局钩子**: 使用 Windows API 的低级鼠标钩子拦截滚轮事件
- **平滑算法**: 实现基于物理的滚动动画，包含速度、加速度和摩擦力
- **系统集成**: 支持系统托盘、开机自启等 Windows 系统功能
- **配置管理**: 使用 JSON 格式保存用户配置到程序同目录的 data 文件夹

## 文件结构

```
SmoothRoller/
├── Program.cs              # 程序入口点
├── SmoothRollerApp.cs      # 主应用程序类
├── MouseHook.cs            # 鼠标钩子和平滑滚动实现
├── ScrollConfig.cs         # 配置数据类
├── SettingsForm.cs         # 设置界面
├── SmoothRoller.csproj     # 项目文件
└── README.md               # 说明文档
```

## 注意事项

1. **管理员权限**: 某些应用可能需要以管理员身份运行才能生效
2. **防病毒软件**: 由于使用了全局钩子，可能被防病毒软件误报
3. **兼容性**: 在某些特殊应用中可能不生效（如某些游戏的全屏模式）
4. **性能**: 正常使用下对系统性能影响极小

## 故障排除

**Q: 程序无法启动？**
A: 检查是否安装了 .NET 6.0 Runtime，或尝试以管理员身份运行

**Q: 平滑滚动不生效？**
A: 确认设置中已启用平滑滚动，并尝试调整参数

**Q: 某些应用中不工作？**
A: 尝试以管理员身份运行程序，或在设置中调整参数

**Q: 开机自启不工作？**
A: 检查 Windows 启动项设置，确保程序有足够权限修改注册表

## 许可证

本项目采用 MIT 许可证，详见 LICENSE 文件。

## 贡献

欢迎提交 Issue 和 Pull Request 来改进这个项目！