namespace AFOCS.Infrastructure
{
    /// <summary>
    /// 全部输入信号（4个R3-3200模块，共128路）
    /// Module-1: bits 0~31 | Module-2: bits 32~63 | Module-3: bits 64~95 | Module-4: bits 96~127
    /// </summary>
    public enum AllInputs
    {
        // ==================== Module-1: R3-3200-1（左工位通用IO）====================
        #region Module-1

        /// <summary>工位1按钮1 | X00</summary>
        Left_Button1 = 0,
        /// <summary>工位1按钮2 | X01</summary>
        Left_Button2 = 1,
        /// <summary>工位1按钮3 | X02</summary>
        Left_Button3 = 2,
        /// <summary>工位1按钮4 | X03</summary>
        Left_Button4 = 3,
        /// <summary>工位1按钮5 | X04</summary>
        Left_Button5 = 4,
        /// <summary>工位1按钮6 | X05</summary>
        Left_Button6 = 5,
        /// <summary>工位1急停 | X06</summary>
        Left_EmergencyStop = 6,

        /// <summary>OE电口插拔气缸缩回 | X10</summary>
        Left_OEPlugRetract = 8,
        /// <summary>OE电口插拔气缸伸出 | X11</summary>
        Left_OEPlugExtend = 9,
        /// <summary>擦胶夹紧气缸松开 | X12</summary>
        Left_WipeClampRelease = 10,
        /// <summary>擦胶夹紧气缸夹紧 | X13</summary>
        Left_WipeClampClamp = 11,
        /// <summary>治具夹紧气缸松开 | X14</summary>
        Left_JigClampRelease = 12,
        /// <summary>治具夹紧气缸夹紧 | X15</summary>
        Left_JigClampClamp = 13,

        /// <summary>夹具左前FA气缸松开 | X20</summary>
        Left_FixtureLeftFrontFARelease = 16,
        /// <summary>夹具左前FA气缸夹紧 | X21</summary>
        Left_FixtureLeftFrontFAClamp = 17,
        /// <summary>夹具左FA气缸松开 | X22</summary>
        Left_FixtureLeftFARelease = 18,
        /// <summary>夹具左FA气缸夹紧 | X23</summary>
        Left_FixtureLeftFAClamp = 19,
        /// <summary>夹具右FA气缸松开 | X24</summary>
        Left_FixtureRightFARelease = 20,
        /// <summary>夹具右FA气缸夹紧 | X25</summary>
        Left_FixtureRightFAClamp = 21,

        /// <summary>UV升降气缸缩回 | X30</summary>
        Left_UVLiftRetract = 24,
        /// <summary>UV升降气缸伸出 | X31</summary>
        Left_UVLiftExtend = 25,
        /// <summary>UV伸缩气缸缩回 | X32</summary>
        Left_UVTelescopeRetract = 26,
        /// <summary>UV伸缩气缸伸出 | X33</summary>
        Left_UVTelescopeExtend = 27,
        /// <summary>点胶升降气缸缩回 | X34</summary>
        Left_DispenseLiftRetract = 28,
        /// <summary>点胶升降气缸伸出 | X35</summary>
        Left_DispenseLiftExtend = 29,
        /// <summary>点胶伸缩气缸缩回 | X36</summary>
        Left_DispenseTelescopeRetract = 30,
        /// <summary>点胶伸缩气缸伸出 | X37</summary>
        Left_DispenseTelescopeExtend = 31,

        #endregion

        // ==================== Module-2: R3-3200-2（左工位真空/UV/温度）====================
        #region Module-2

        /// <summary>夹具真空检测 | X40</summary>
        Left_FixtureVacuum = 32,
        /// <summary>左耦合夹爪真空检测 | X41</summary>
        Left_LCouplingGripperVacuum = 33,
        /// <summary>右耦合夹爪真空检测 | X42</summary>
        Left_RCouplingGripperVacuum = 34,

        /// <summary>夹爪UV控制器状态 | X50</summary>
        Left_GripperUVStatus = 40,
        /// <summary>夹爪UV控制器报警 | X51</summary>
        Left_GripperUVAlarm = 41,
        /// <summary>相机UV控制器状态 | X52</summary>
        Left_CameraUVStatus = 42,
        /// <summary>相机UV控制器报警 | X53</summary>
        Left_CameraUVAlarm = 43,
        /// <summary>夹具加热温度到达设定 | X54</summary>
        Left_FixtureHeatReached = 44,

        #endregion

        // ==================== Module-3: R3-3200-3（右工位通用IO）====================
        #region Module-3

        /// <summary>工位2按钮1 | X100</summary>
        Right_Button1 = 64,
        /// <summary>工位2按钮2 | X101</summary>
        Right_Button2 = 65,
        /// <summary>工位2按钮3 | X102</summary>
        Right_Button3 = 66,
        /// <summary>工位2按钮4 | X103</summary>
        Right_Button4 = 67,
        /// <summary>工位2按钮5 | X104</summary>
        Right_Button5 = 68,
        /// <summary>工位2按钮6 | X105</summary>
        Right_Button6 = 69,
        /// <summary>工位2急停 | X106</summary>
        Right_EmergencyStop = 70,

        /// <summary>OE电口插拔气缸缩回 | X110</summary>
        Right_OEPlugRetract = 72,
        /// <summary>OE电口插拔气缸伸出 | X111</summary>
        Right_OEPlugExtend = 73,
        /// <summary>擦胶伸缩气缸缩回 | X112</summary>
        Right_WipeTelescopeRetract = 74,
        /// <summary>擦胶伸缩气缸伸出 | X113</summary>
        Right_WipeTelescopeExtend = 75,
        /// <summary>治具夹紧气缸夹紧 | X114</summary>
        Right_JigClampClamp = 76,
        /// <summary>治具夹紧气缸夹紧(冗余) | X115</summary>
        Right_JigClampClamp2 = 77,

        /// <summary>夹具左前FA气缸松开 | X120</summary>
        Right_FixtureLeftFrontFARelease = 80,
        /// <summary>夹具左前FA气缸夹紧 | X121</summary>
        Right_FixtureLeftFrontFAClamp = 81,
        /// <summary>夹具左FA气缸松开 | X122</summary>
        Right_FixtureLeftFARelease = 82,
        /// <summary>夹具左FA气缸夹紧 | X123</summary>
        Right_FixtureLeftFAClamp = 83,
        /// <summary>夹具右FA气缸松开 | X124</summary>
        Right_FixtureRightFARelease = 84,
        /// <summary>夹具右FA气缸夹紧 | X125</summary>
        Right_FixtureRightFAClamp = 85,

        /// <summary>UV升降气缸缩回 | X130</summary>
        Right_UVLiftRetract = 88,
        /// <summary>UV升降气缸伸出 | X131</summary>
        Right_UVLiftExtend = 89,
        /// <summary>UV伸缩气缸缩回 | X132</summary>
        Right_UVTelescopeRetract = 90,
        /// <summary>UV伸缩气缸伸出 | X133</summary>
        Right_UVTelescopeExtend = 91,
        /// <summary>点胶升降气缸缩回 | X134</summary>
        Right_DispenseLiftRetract = 92,
        /// <summary>点胶升降气缸伸出 | X135</summary>
        Right_DispenseLiftExtend = 93,
        /// <summary>点胶伸缩气缸缩回 | X136</summary>
        Right_DispenseTelescopeRetract = 94,
        /// <summary>点胶伸缩气缸伸出 | X137</summary>
        Right_DispenseTelescopeExtend = 95,

        #endregion

        // ==================== Module-4: R3-3200-4（右工位真空/UV/温度）====================
        #region Module-4

        /// <summary>治具真空检测 | X140</summary>
        Right_FixtureVacuum = 96,
        /// <summary>左耦合模组真空检测 | X141</summary>
        Right_LCouplingVacuum = 97,
        /// <summary>右耦合模组真空检测 | X142</summary>
        Right_RCouplingVacuum = 98,

        /// <summary>夹爪UV控制器状态 | X150</summary>
        Right_GripperUVStatus = 104,
        /// <summary>夹爪UV控制器报警 | X151</summary>
        Right_GripperUVAlarm = 105,
        /// <summary>相机UV控制器状态 | X152</summary>
        Right_CameraUVStatus = 106,
        /// <summary>相机UV控制器报警 | X153</summary>
        Right_CameraUVAlarm = 107,
        /// <summary>夹具加热温度到达设定 | X154</summary>
        Right_FixtureHeatReached = 108,

        #endregion
    }

    /// <summary>
    /// 全部输出信号（4个R3-0032-N模块，共128路）
    /// Module-5: bits 0~31 | Module-6: bits 32~63 | Module-7: bits 64~95 | Module-8: bits 96~127
    /// </summary>
    public enum AllOutputs
    {
        // ==================== Module-5: R3-0032-N-1（左工位通用输出）====================
        #region Module-5

        /// <summary>三色灯红 | Y00</summary>
        TowerRed = 0,
        /// <summary>三色灯黄 | Y01</summary>
        TowerYellow = 1,
        /// <summary>三色灯绿 | Y02</summary>
        TowerGreen = 2,
        /// <summary>蜂鸣器 | Y03</summary>
        Buzzer = 3,
        /// <summary>上相机光源 | Y04</summary>
        Left_CamUpLight = 4,
        /// <summary>侧相机光源 | Y05</summary>
        Left_CamSideLight = 5,

        /// <summary>OE电口插拔气缸缩回 | Y10</summary>
        Left_OEPlugRetract = 8,
        /// <summary>OE电口插拔气缸伸出 | Y11</summary>
        Left_OEPlugExtend = 9,
        /// <summary>擦胶伸缩气缸缩回 | Y12</summary>
        Left_WipeTelescopeRetract = 10,
        /// <summary>擦胶伸缩气缸伸出 | Y13</summary>
        Left_WipeTelescopeExtend = 11,
        /// <summary>治具夹紧气缸松开 | Y14</summary>
        Left_JigClampRelease = 12,
        /// <summary>治具夹紧气缸夹紧 | Y15</summary>
        Left_JigClampClamp = 13,

        /// <summary>夹具左前FA气缸松开 | Y20</summary>
        Left_FixtureLeftFrontFARelease = 16,
        /// <summary>夹具左前FA气缸夹紧 | Y21</summary>
        Left_FixtureLeftFrontFAClamp = 17,
        /// <summary>夹具左FA气缸松开 | Y22</summary>
        Left_FixtureLeftFARelease = 18,
        /// <summary>夹具左FA气缸夹紧 | Y23</summary>
        Left_FixtureLeftFAClamp = 19,
        /// <summary>夹具右FA气缸松开 | Y24</summary>
        Left_FixtureRightFARelease = 20,
        /// <summary>夹具右FA气缸夹紧 | Y25</summary>
        Left_FixtureRightFAClamp = 21,

        /// <summary>UV升降气缸缩回 | Y30</summary>
        Left_UVLiftRetract = 24,
        /// <summary>UV升降气缸伸出 | Y31</summary>
        Left_UVLiftExtend = 25,
        /// <summary>UV伸缩气缸缩回 | Y32</summary>
        Left_UVTelescopeRetract = 26,
        /// <summary>UV伸缩气缸伸出 | Y33</summary>
        Left_UVTelescopeExtend = 27,
        /// <summary>点胶升降气缸缩回 | Y34</summary>
        Left_DispenseLiftRetract = 28,
        /// <summary>点胶升降气缸伸出 | Y35</summary>
        Left_DispenseLiftExtend = 29,
        /// <summary>点胶伸缩气缸缩回 | Y36</summary>
        Left_DispenseTelescopeRetract = 30,
        /// <summary>点胶伸缩气缸伸出 | Y37</summary>
        Left_DispenseTelescopeExtend = 31,

        #endregion

        // ==================== Module-6: R3-0032-N-2（左工位真空/UV/加热）====================
        #region Module-6

        /// <summary>治具真空 | Y40</summary>
        Left_FixtureVacuum = 32,
        /// <summary>左耦合夹爪真空 | Y41</summary>
        Left_LCouplingGripperVacuum = 33,
        /// <summary>右耦合夹爪真空 | Y42</summary>
        Left_RCouplingGripperVacuum = 34,
        /// <summary>上相机UV光源 | Y47</summary>
        Left_CamUpUVLight = 39,

        /// <summary>左夹爪UV光源 | Y50</summary>
        Left_LGripperUVLight = 40,
        /// <summary>右夹爪UV光源 | Y51</summary>
        Left_RGripperUVLight = 41,
        /// <summary>夹具加热 | Y52</summary>
        Left_FixtureHeat = 42,
        /// <summary>点胶控制器 | Y53</summary>
        Left_DispenseController = 43,

        #endregion

        // ==================== Module-7: R3-0032-N-3（右工位通用输出）====================
        #region Module-7

        /// <summary>上相机光源 | Y104</summary>
        Right_CamUpLight = 68,
        /// <summary>侧相机光源 | Y105</summary>
        Right_CamSideLight = 69,

        /// <summary>OE电口气缸缩回 | Y110</summary>
        Right_OEPlugRetract = 72,
        /// <summary>OE电口气缸伸出 | Y111</summary>
        Right_OEPlugExtend = 73,
        /// <summary>擦胶伸缩气缸缩回 | Y112</summary>
        Right_WipeTelescopeRetract = 74,
        /// <summary>擦胶伸缩气缸伸出 | Y113</summary>
        Right_WipeTelescopeExtend = 75,
        /// <summary>治具夹紧气缸夹紧 | Y114</summary>
        Right_JigClampClamp = 76,
        /// <summary>治具夹紧气缸夹紧(冗余) | Y115</summary>
        Right_JigClampClamp2 = 77,

        /// <summary>夹具左前FA气缸松开 | Y120</summary>
        Right_FixtureLeftFrontFARelease = 80,
        /// <summary>夹具左前FA气缸夹紧 | Y121</summary>
        Right_FixtureLeftFrontFAClamp = 81,
        /// <summary>夹具左FA气缸松开 | Y122</summary>
        Right_FixtureLeftFARelease = 82,
        /// <summary>夹具左FA气缸夹紧 | Y123</summary>
        Right_FixtureLeftFAClamp = 83,
        /// <summary>夹具右FA气缸松开 | Y124</summary>
        Right_FixtureRightFARelease = 84,
        /// <summary>夹具右FA气缸夹紧 | Y125</summary>
        Right_FixtureRightFAClamp = 85,

        /// <summary>UV升降气缸缩回 | Y130</summary>
        Right_UVLiftRetract = 88,
        /// <summary>UV升降气缸伸出 | Y131</summary>
        Right_UVLiftExtend = 89,
        /// <summary>UV伸缩气缸缩回 | Y132</summary>
        Right_UVTelescopeRetract = 90,
        /// <summary>UV伸缩气缸伸出 | Y133</summary>
        Right_UVTelescopeExtend = 91,
        /// <summary>点胶升降气缸缩回 | Y134</summary>
        Right_DispenseLiftRetract = 92,
        /// <summary>点胶升降气缸伸出 | Y135</summary>
        Right_DispenseLiftExtend = 93,
        /// <summary>点胶伸缩气缸缩回 | Y136</summary>
        Right_DispenseTelescopeRetract = 94,
        /// <summary>点胶伸缩气缸伸出 | Y137</summary>
        Right_DispenseTelescopeExtend = 95,

        #endregion

        // ==================== Module-8: R3-0032-N-4（右工位真空/UV/加热）====================
        #region Module-8

        /// <summary>治具真空 | Y140</summary>
        Right_FixtureVacuum = 96,
        /// <summary>左耦合夹爪真空 | Y141</summary>
        Right_LCouplingGripperVacuum = 97,
        /// <summary>右耦合夹爪真空 | Y142</summary>
        Right_RCouplingGripperVacuum = 98,
        /// <summary>上相机UV光源 | Y147</summary>
        Right_CamUpUVLight = 103,

        /// <summary>左夹爪UV光源 | Y150</summary>
        Right_LGripperUVLight = 104,
        /// <summary>右夹爪UV光源 | Y151（原表与Y150同名，推断为右）</summary>
        Right_RGripperUVLight = 105,
        /// <summary>夹具加热 | Y152</summary>
        Right_FixtureHeat = 106,
        /// <summary>点胶控制器 | Y153</summary>
        Right_DispenseController = 107,

        #endregion
    }

    /// <summary>
    /// 输入信号的中文显示名称
    /// </summary>
    public static class SignalNames
    {
        public static readonly Dictionary<AllInputs, string> Module1 = new()
        {
            [AllInputs.Left_Button1] = "按钮1",
            [AllInputs.Left_Button2] = "按钮2",
            [AllInputs.Left_Button3] = "按钮3",
            [AllInputs.Left_Button4] = "按钮4",
            [AllInputs.Left_Button5] = "按钮5",
            [AllInputs.Left_Button6] = "按钮6",
            [AllInputs.Left_EmergencyStop] = "急停",
            [AllInputs.Left_OEPlugRetract] = "OE电口插拔气缸缩回",
            [AllInputs.Left_OEPlugExtend] = "OE电口插拔气缸伸出",
            [AllInputs.Left_WipeClampRelease] = "擦胶夹紧气缸松开",
            [AllInputs.Left_WipeClampClamp] = "擦胶夹紧气缸夹紧",
            [AllInputs.Left_JigClampRelease] = "治具夹紧气缸松开",
            [AllInputs.Left_JigClampClamp] = "治具夹紧气缸夹紧",
            [AllInputs.Left_FixtureLeftFrontFARelease] = "夹具左前FA松开",
            [AllInputs.Left_FixtureLeftFrontFAClamp] = "夹具左前FA夹紧",
            [AllInputs.Left_FixtureLeftFARelease] = "夹具左FA松开",
            [AllInputs.Left_FixtureLeftFAClamp] = "夹具左FA夹紧",
            [AllInputs.Left_FixtureRightFARelease] = "夹具右FA松开",
            [AllInputs.Left_FixtureRightFAClamp] = "夹具右FA夹紧",
            [AllInputs.Left_UVLiftRetract] = "UV升降气缸缩回",
            [AllInputs.Left_UVLiftExtend] = "UV升降气缸伸出",
            [AllInputs.Left_UVTelescopeRetract] = "UV伸缩气缸缩回",
            [AllInputs.Left_UVTelescopeExtend] = "UV伸缩气缸伸出",
            [AllInputs.Left_DispenseLiftRetract] = "点胶升降气缸缩回",
            [AllInputs.Left_DispenseLiftExtend] = "点胶升降气缸伸出",
            [AllInputs.Left_DispenseTelescopeRetract] = "点胶伸缩气缸缩回",
            [AllInputs.Left_DispenseTelescopeExtend] = "点胶伸缩气缸伸出",
        };

        public static readonly Dictionary<AllInputs, string> Module2 = new()
        {
            [AllInputs.Left_FixtureVacuum] = "夹具真空",
            [AllInputs.Left_LCouplingGripperVacuum] = "左耦合夹爪真空",
            [AllInputs.Left_RCouplingGripperVacuum] = "右耦合夹爪真空",
            [AllInputs.Left_GripperUVStatus] = "夹爪UV状态",
            [AllInputs.Left_GripperUVAlarm] = "夹爪UV报警",
            [AllInputs.Left_CameraUVStatus] = "相机UV状态",
            [AllInputs.Left_CameraUVAlarm] = "相机UV报警",
            [AllInputs.Left_FixtureHeatReached] = "夹具加热到达",
        };

        public static readonly Dictionary<AllInputs, string> Module3 = new()
        {
            [AllInputs.Right_Button1] = "按钮1",
            [AllInputs.Right_Button2] = "按钮2",
            [AllInputs.Right_Button3] = "按钮3",
            [AllInputs.Right_Button4] = "按钮4",
            [AllInputs.Right_Button5] = "按钮5",
            [AllInputs.Right_Button6] = "按钮6",
            [AllInputs.Right_EmergencyStop] = "急停",
            [AllInputs.Right_OEPlugRetract] = "OE电口插拔气缸缩回",
            [AllInputs.Right_OEPlugExtend] = "OE电口插拔气缸伸出",
            [AllInputs.Right_WipeTelescopeRetract] = "擦胶伸缩气缸缩回",
            [AllInputs.Right_WipeTelescopeExtend] = "擦胶伸缩气缸伸出",
            [AllInputs.Right_JigClampClamp] = "治具夹紧气缸夹紧",
            [AllInputs.Right_JigClampClamp2] = "治具夹紧气缸夹紧2",
            [AllInputs.Right_FixtureLeftFrontFARelease] = "夹具左前FA松开",
            [AllInputs.Right_FixtureLeftFrontFAClamp] = "夹具左前FA夹紧",
            [AllInputs.Right_FixtureLeftFARelease] = "夹具左FA松开",
            [AllInputs.Right_FixtureLeftFAClamp] = "夹具左FA夹紧",
            [AllInputs.Right_FixtureRightFARelease] = "夹具右FA松开",
            [AllInputs.Right_FixtureRightFAClamp] = "夹具右FA夹紧",
            [AllInputs.Right_UVLiftRetract] = "UV升降气缸缩回",
            [AllInputs.Right_UVLiftExtend] = "UV升降气缸伸出",
            [AllInputs.Right_UVTelescopeRetract] = "UV伸缩气缸缩回",
            [AllInputs.Right_UVTelescopeExtend] = "UV伸缩气缸伸出",
            [AllInputs.Right_DispenseLiftRetract] = "点胶升降气缸缩回",
            [AllInputs.Right_DispenseLiftExtend] = "点胶升降气缸伸出",
            [AllInputs.Right_DispenseTelescopeRetract] = "点胶伸缩气缸缩回",
            [AllInputs.Right_DispenseTelescopeExtend] = "点胶伸缩气缸伸出",
        };

        public static readonly Dictionary<AllInputs, string> Module4 = new()
        {
            [AllInputs.Right_FixtureVacuum] = "治具真空",
            [AllInputs.Right_LCouplingVacuum] = "左耦合模组真空",
            [AllInputs.Right_RCouplingVacuum] = "右耦合模组真空",
            [AllInputs.Right_GripperUVStatus] = "夹爪UV状态",
            [AllInputs.Right_GripperUVAlarm] = "夹爪UV报警",
            [AllInputs.Right_CameraUVStatus] = "相机UV状态",
            [AllInputs.Right_CameraUVAlarm] = "相机UV报警",
            [AllInputs.Right_FixtureHeatReached] = "夹具加热到达",
        };
    }
}
