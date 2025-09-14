using System;

namespace SmoothRoller
{
    public enum ApplicationType
    {
        Default,
        CodeEditor,  // IDEA, VSCode, Visual Studio等
        Browser,     // Chrome, Firefox等
        Office,      // Word, Excel等
        System       // 系统应用
    }

    public class ScrollConfig
    {
        /// <summary>
        /// 步长：单次操作鼠标窗口滑动的像素距离 (最小值: 1)
        /// </summary>
        public int StepSize { get; set; } = 90;
        
        /// <summary>
        /// 动画时间：滚动动画效果持续的时间长度 (100 - 2000ms)
        /// </summary>
        public int AnimationTime { get; set; } = 360;
        
        /// <summary>
        /// 滚动加速：数值越高，窗口滚动也越快 (1 - 100)
        /// </summary>
        public int AccelerationDelta { get; set; } = 70;
        
        /// <summary>
        /// 最大加速倍速：滚动速度最高能加速到正常速度的倍数 (1 - 20)
        /// </summary>
        public int AccelerationMax { get; set; } = 7;
        
        /// <summary>
        /// 减速/加速比：一次滚动中减速时间与加速时间的比值 (1 - 10)
        /// </summary>
        public int TailToHeadRatio { get; set; } = 4;
        
        /// <summary>
        /// 是否启用平滑滚动
        /// </summary>
        public bool EnableSmoothScroll { get; set; } = true;
        
        /// <summary>
        /// 是否反转滚动方向
        /// </summary>
        public bool ReverseScrollDirection { get; set; } = false;
        
        /// <summary>
        /// 最小滚动阈值
        /// </summary>
        public double MinScrollThreshold { get; set; } = 0.5;
        
        /// <summary>
        /// 自适应帧率：根据滚动速度动态调整更新频率
        /// </summary>
        public bool EnableAdaptiveFrameRate { get; set; } = true;
        
        /// <summary>
        /// 最小更新间隔(ms)：防止过度频繁的更新
        /// </summary>
        public int MinUpdateInterval { get; set; } = 8;
        
        /// <summary>
        /// 最大更新间隔(ms)：确保足够的流畅度
        /// </summary>
        public int MaxUpdateInterval { get; set; } = 33;
        
        /// <summary>
        /// 获取针对特定应用类型优化的配置
        /// </summary>
        public static ScrollConfig GetOptimizedConfig(ApplicationType appType)
        {
            var config = new ScrollConfig();
            
            switch (appType)
            {
                case ApplicationType.CodeEditor:
                    // 代码编辑器优化：更小的步长，更快的响应
                    config.StepSize = 30;  // 减少单次滚动行数
                    config.AnimationTime = 200;  // 更快的动画
                    config.AccelerationDelta = 50;
                    config.AccelerationMax = 4;
                    config.MinUpdateInterval = 12;  // 稍微降低帧率
                    config.MaxUpdateInterval = 25;
                    break;
                    
                case ApplicationType.Browser:
                    // 浏览器优化：中等步长，平滑滚动
                    config.StepSize = 60;
                    config.AnimationTime = 300;
                    config.AccelerationDelta = 60;
                    config.AccelerationMax = 6;
                    break;
                    
                case ApplicationType.Office:
                    // 办公软件优化：较大步长，适合文档浏览
                    config.StepSize = 120;
                    config.AnimationTime = 400;
                    config.AccelerationDelta = 80;
                    config.AccelerationMax = 8;
                    break;
                    
                default:
                    // 保持默认配置
                    break;
            }
            
            config.Validate();
            return config;
        }
        
        /// <summary>
        /// 验证配置参数是否有效
        /// </summary>
        public void Validate()
        {
            StepSize = Math.Max(1, StepSize);
            AnimationTime = Math.Max(100, Math.Min(2000, AnimationTime));
            AccelerationDelta = Math.Max(1, Math.Min(100, AccelerationDelta));
            AccelerationMax = Math.Max(1, Math.Min(20, AccelerationMax));
            TailToHeadRatio = Math.Max(1, Math.Min(10, TailToHeadRatio));
            MinScrollThreshold = Math.Max(0.1, Math.Min(2.0, MinScrollThreshold));
            MinUpdateInterval = Math.Max(4, Math.Min(50, MinUpdateInterval));
            MaxUpdateInterval = Math.Max(MinUpdateInterval, Math.Min(100, MaxUpdateInterval));
        }
        
        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefaults()
        {
            StepSize = 90;
            AnimationTime = 360;
            AccelerationDelta = 70;
            AccelerationMax = 7;
            TailToHeadRatio = 4;
            EnableSmoothScroll = true;
            ReverseScrollDirection = false;
            MinScrollThreshold = 0.5;
        }
    }
}