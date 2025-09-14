using System;
using System.Drawing;
using System.Windows.Forms;

namespace SmoothRoller
{
    public partial class SettingsForm : Form
    {
        private ScrollConfig config;
        public event EventHandler ConfigChanged;
        
        private NumericUpDown stepSizeNumeric;
        private NumericUpDown animationTimeNumeric;
        private NumericUpDown accelerationDeltaNumeric;
        private NumericUpDown accelerationMaxNumeric;
        private NumericUpDown tailToHeadRatioNumeric;
        
        private Label stepSizeLabel;
        private Label animationTimeLabel;
        private Label accelerationDeltaLabel;
        private Label accelerationMaxLabel;
        private Label tailToHeadRatioLabel;
        
        private CheckBox enableSmoothScrollCheckBox;
        private CheckBox reverseDirectionCheckBox;
        
        private Button resetButton;
        
        public SettingsForm(ScrollConfig config)
        {
            this.config = config;
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "SmoothRoller 设置";
            this.Size = new Size(450, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };
            
            int yPos = 10;
            
            // 启用平滑滚动
            enableSmoothScrollCheckBox = new CheckBox
            {
                Text = "启用平滑滚动",
                Location = new Point(10, yPos),
                Size = new Size(200, 25)
            };
            enableSmoothScrollCheckBox.CheckedChanged += OnSettingChanged;
            panel.Controls.Add(enableSmoothScrollCheckBox);
            yPos += 35;
            
            // 反转滚动方向
            reverseDirectionCheckBox = new CheckBox
            {
                Text = "反转滚动方向",
                Location = new Point(10, yPos),
                Size = new Size(200, 25)
            };
            reverseDirectionCheckBox.CheckedChanged += OnSettingChanged;
            panel.Controls.Add(reverseDirectionCheckBox);
            yPos += 45;
            
            // 步长
            CreateNumericSetting(panel, "步长(像素):", ref stepSizeLabel, ref stepSizeNumeric, 
                ref yPos, 1, 10000);
            
            // 动画时间
            CreateNumericSetting(panel, "动画时间(ms):", ref animationTimeLabel, ref animationTimeNumeric, 
                ref yPos, 100, 2000);
            
            // 滚动加速
            CreateNumericSetting(panel, "滚动加速:", ref accelerationDeltaLabel, ref accelerationDeltaNumeric, 
                ref yPos, 1, 100);
            
            // 最大加速倍速
            CreateNumericSetting(panel, "最大加速倍速:", ref accelerationMaxLabel, ref accelerationMaxNumeric, 
                ref yPos, 1, 20);
            
            // 减速/加速比
            CreateNumericSetting(panel, "减速/加速比:", ref tailToHeadRatioLabel, ref tailToHeadRatioNumeric, 
                ref yPos, 1, 10);
            
            yPos += 20;
            
            // 按钮
            resetButton = new Button
            {
                Text = "重置默认",
                Location = new Point(10, yPos),
                Size = new Size(100, 30)
            };
            resetButton.Click += OnResetClick;
            panel.Controls.Add(resetButton);
            
            this.Controls.Add(panel);
            
            // 居中重置按钮（初始和后续尺寸变化时）
            resetButton.Left = Math.Max(0, (panel.ClientSize.Width - resetButton.Width) / 2);
            panel.Resize += (s, e) =>
            {
                resetButton.Left = Math.Max(0, (panel.ClientSize.Width - resetButton.Width) / 2);
            };
            
            // 根据内容高度收缩窗口，减少底部空白
            int desiredHeight = resetButton.Bottom + 60;
            this.ClientSize = new Size(this.ClientSize.Width, desiredHeight);
        }
        
        private void CreateNumericSetting(Panel parent, string labelText, ref Label label, ref NumericUpDown numeric, 
            ref int yPos, int min, int max)
        {
            label = new Label
            {
                Text = labelText,
                Location = new Point(10, yPos),
                Size = new Size(150, 20)
            };
            parent.Controls.Add(label);
            
            numeric = new NumericUpDown
            {
                Location = new Point(160, yPos),
                Size = new Size(100, 25),
                Minimum = min,
                Maximum = max,
                DecimalPlaces = 0
            };
            numeric.ValueChanged += OnSettingChanged;
            parent.Controls.Add(numeric);
            
            yPos += 35;
        }
        
        private void LoadSettings()
        {
            if (enableSmoothScrollCheckBox != null)
                enableSmoothScrollCheckBox.Checked = config.EnableSmoothScroll;
            if (reverseDirectionCheckBox != null)
                reverseDirectionCheckBox.Checked = config.ReverseScrollDirection;
            
            if (stepSizeNumeric != null)
                stepSizeNumeric.Value = config.StepSize;
            if (animationTimeNumeric != null)
                animationTimeNumeric.Value = config.AnimationTime;
            if (accelerationDeltaNumeric != null)
                accelerationDeltaNumeric.Value = config.AccelerationDelta;
            if (accelerationMaxNumeric != null)
                accelerationMaxNumeric.Value = config.AccelerationMax;
            if (tailToHeadRatioNumeric != null)
                tailToHeadRatioNumeric.Value = config.TailToHeadRatio;
        }
        
        private void OnSettingChanged(object sender, EventArgs e)
        {
            // 更新配置
            config.EnableSmoothScroll = enableSmoothScrollCheckBox.Checked;
            config.ReverseScrollDirection = reverseDirectionCheckBox.Checked;
            config.StepSize = (int)stepSizeNumeric.Value;
            config.AnimationTime = (int)animationTimeNumeric.Value;
            config.AccelerationDelta = (int)accelerationDeltaNumeric.Value;
            config.AccelerationMax = (int)accelerationMaxNumeric.Value;
            config.TailToHeadRatio = (int)tailToHeadRatioNumeric.Value;
            
            config.Validate();
            
            // 实时保存配置
            ConfigChanged?.Invoke(this, EventArgs.Empty);
        }
        
        private void OnResetClick(object sender, EventArgs e)
        {
            // 暂时禁用事件处理
            DisableEvents();
            
            config.ResetToDefaults();
            LoadSettings();
            
            // 重新启用事件处理
            EnableEvents();
            
            // 只触发一次配置变更事件
            ConfigChanged?.Invoke(this, EventArgs.Empty);
        }
        
        private void OnCloseClick(object sender, EventArgs e)
        {
            this.Hide();
        }
        
        public void UpdateConfig(ScrollConfig newConfig)
        {
            DisableEvents();
            config = newConfig;
            LoadSettings();
            EnableEvents();
        }
        
        private void DisableEvents()
        {
            if (stepSizeNumeric != null)
                stepSizeNumeric.ValueChanged -= OnSettingChanged;
            if (animationTimeNumeric != null)
                animationTimeNumeric.ValueChanged -= OnSettingChanged;
            if (accelerationDeltaNumeric != null)
                accelerationDeltaNumeric.ValueChanged -= OnSettingChanged;
            if (accelerationMaxNumeric != null)
                accelerationMaxNumeric.ValueChanged -= OnSettingChanged;
            if (tailToHeadRatioNumeric != null)
                tailToHeadRatioNumeric.ValueChanged -= OnSettingChanged;
            if (enableSmoothScrollCheckBox != null)
                enableSmoothScrollCheckBox.CheckedChanged -= OnSettingChanged;
            if (reverseDirectionCheckBox != null)
                reverseDirectionCheckBox.CheckedChanged -= OnSettingChanged;
        }
        
        private void EnableEvents()
        {
            if (stepSizeNumeric != null)
                stepSizeNumeric.ValueChanged += OnSettingChanged;
            if (animationTimeNumeric != null)
                animationTimeNumeric.ValueChanged += OnSettingChanged;
            if (accelerationDeltaNumeric != null)
                accelerationDeltaNumeric.ValueChanged += OnSettingChanged;
            if (accelerationMaxNumeric != null)
                accelerationMaxNumeric.ValueChanged += OnSettingChanged;
            if (tailToHeadRatioNumeric != null)
                tailToHeadRatioNumeric.ValueChanged += OnSettingChanged;
            if (enableSmoothScrollCheckBox != null)
                enableSmoothScrollCheckBox.CheckedChanged += OnSettingChanged;
            if (reverseDirectionCheckBox != null)
                reverseDirectionCheckBox.CheckedChanged += OnSettingChanged;
        }
        
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
            if (value)
            {
                DisableEvents();
                LoadSettings();
                EnableEvents();
            }
        }
    }
}