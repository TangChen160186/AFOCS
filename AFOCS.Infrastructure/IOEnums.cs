namespace AFOCS.Infrastructure
{
    /// <summary>
    /// 全部输入信号（4个R3-3200模块，共128路）
    /// Module-1: bits 8~39 | Module-2: bits 40~71 | Module-3: bits 72~103 | Module-4: bits 104~135
    /// </summary>
    public enum AllInputs
    {
        // ==================== Module-1: R3-3200-1（左工位通用IO）bits 8~39 ====================
        #region Module-1

        /// <summary>工位1按钮1 | X00 bit8</summary>
        Left_Button1 = 8,
        /// <summary>工位1按钮2 | X01 bit9</summary>
        Left_Button2 = 9,
        /// <summary>工位1按钮3 | X02 bit10</summary>
        Left_Button3 = 10,
        /// <summary>工位1按钮4 | X03 bit11</summary>
        Left_Button4 = 11,
        /// <summary>工位1按钮5 | X04 bit12</summary>
        Left_Button5 = 12,
        /// <summary>工位1按钮6 | X05 bit13</summary>
        Left_Button6 = 13,
        /// <summary>工位1急停 | X06 bit14</summary>
        Left_EmergencyStop = 14,

        /// <summary>1工位OE电口插拔气缸缩回信号 | X10 bit16</summary>
        Left_OEPlugRetract = 16,
        /// <summary>1工位OE电口插拔气缸伸出信号 | X11 bit17</summary>
        Left_OEPlugExtend = 17,
        /// <summary>1工位擦胶夹紧气缸松开信号 | X12 bit18</summary>
        Left_WipeClampRelease = 18,
        /// <summary>1工位擦胶夹紧气缸夹紧信号 | X13 bit19</summary>
        Left_WipeClampClamp = 19,
        /// <summary>1工位治具夹紧气缸松开信号 | X14 bit20</summary>
        Left_JigClampRelease = 20,
        /// <summary>1工位治具夹紧气缸夹紧信号 | X15 bit21</summary>
        Left_JigClampClamp = 21,

        /// <summary>1工位夹具左前FA气缸松开信号 | X20 bit24</summary>
        Left_FixtureLeftFrontFARelease = 24,
        /// <summary>1工位夹具左前FA气缸夹紧信号 | X21 bit25</summary>
        Left_FixtureLeftFrontFAClamp = 25,
        /// <summary>1工位夹具左FA气缸松开信号 | X22 bit26</summary>
        Left_FixtureLeftFARelease = 26,
        /// <summary>1工位夹具左FA气缸夹紧信号 | X23 bit27</summary>
        Left_FixtureLeftFAClamp = 27,
        /// <summary>1工位夹具右FA气缸松开信号 | X24 bit28</summary>
        Left_FixtureRightFARelease = 28,
        /// <summary>1工位夹具右FA气缸夹紧信号 | X25 bit29</summary>
        Left_FixtureRightFAClamp = 29,

        /// <summary>1工位UV升降气缸缩回信号 | X30 bit32</summary>
        Left_UVLiftRetract = 32,
        /// <summary>1工位UV升降气缸伸出信号 | X31 bit33</summary>
        Left_UVLiftExtend = 33,
        /// <summary>1工位UV伸缩气缸缩回信号 | X32 bit34</summary>
        Left_UVTelescopeRetract = 34,
        /// <summary>1工位UV伸缩气缸伸出信号 | X33 bit35</summary>
        Left_UVTelescopeExtend = 35,
        /// <summary>1工位点胶升降气缸缩回信号 | X34 bit36</summary>
        Left_DispenseLiftRetract = 36,
        /// <summary>1工位点胶升降气缸伸出信号 | X35 bit37</summary>
        Left_DispenseLiftExtend = 37,
        /// <summary>1工位点胶伸缩气缸缩回信号 | X36 bit38</summary>
        Left_DispenseTelescopeRetract = 38,
        /// <summary>1工位点胶伸缩气缸伸出信号 | X37 bit39</summary>
        Left_DispenseTelescopeExtend = 39,

        #endregion

        // ==================== Module-2: R3-3200-2（左工位真空/UV/温度）bits 40~71 ====================
        #region Module-2

        /// <summary>1工位夹具真空检测 | X40 bit40</summary>
        Left_FixtureVacuum = 40,
        /// <summary>1工位左耦合夹爪真空检测 | X41 bit41</summary>
        Left_LCouplingGripperVacuum = 41,
        /// <summary>1工位右耦合夹爪真空检测 | X42 bit42</summary>
        Left_RCouplingGripperVacuum = 42,

        /// <summary>1工位夹爪UV控制器状态信号 | X50 bit48</summary>
        Left_GripperUVStatus = 48,
        /// <summary>1工位夹爪UV控制器报警信号 | X51 bit49</summary>
        Left_GripperUVAlarm = 49,
        /// <summary>1工位相机UV控制器状态信号 | X52 bit50</summary>
        Left_CameraUVStatus = 50,
        /// <summary>1工位相机UV控制器报警信号 | X53 bit51</summary>
        Left_CameraUVAlarm = 51,
        /// <summary>1工位夹具加热温度到达设定 | X54 bit52</summary>
        Left_FixtureHeatReached = 52,

        #endregion

        // ==================== Module-3: R3-3200-3（右工位通用IO）bits 72~103 ====================
        #region Module-3

        /// <summary>工位2按钮1 | X100 bit72</summary>
        Right_Button1 = 72,
        /// <summary>工位2按钮2 | X101 bit73</summary>
        Right_Button2 = 73,
        /// <summary>工位2按钮3 | X102 bit74</summary>
        Right_Button3 = 74,
        /// <summary>工位2按钮4 | X103 bit75</summary>
        Right_Button4 = 75,
        /// <summary>工位2按钮5 | X104 bit76</summary>
        Right_Button5 = 76,
        /// <summary>工位2按钮6 | X105 bit77</summary>
        Right_Button6 = 77,
        /// <summary>工位2急停 | X106 bit78</summary>
        Right_EmergencyStop = 78,

        /// <summary>2工位OE电口插拔气缸缩回信号 | X110 bit80</summary>
        Right_OEPlugRetract = 80,
        /// <summary>2工位OE电口插拔气缸伸出信号 | X111 bit81</summary>
        Right_OEPlugExtend = 81,
        /// <summary>2工位擦胶伸缩气缸缩回信号 | X112 bit82</summary>
        Right_WipeTelescopeRetract = 82,
        /// <summary>2工位擦胶伸缩气缸伸出信号 | X113 bit83</summary>
        Right_WipeTelescopeExtend = 83,
        /// <summary>2工位治具夹紧气缸夹紧信号 | X114 bit84</summary>
        Right_JigClampClamp = 84,
        /// <summary>2工位治具夹紧气缸夹紧信号 | X115 bit85</summary>
        Right_JigClampClamp2 = 85,

        /// <summary>2工位夹具左前FA气缸松开信号 | X120 bit88</summary>
        Right_FixtureLeftFrontFARelease = 88,
        /// <summary>2工位夹具左前FA气缸夹紧信号 | X121 bit89</summary>
        Right_FixtureLeftFrontFAClamp = 89,
        /// <summary>2工位夹具左FA气缸松开信号 | X122 bit90</summary>
        Right_FixtureLeftFARelease = 90,
        /// <summary>2工位夹具左FA气缸夹紧信号 | X123 bit91</summary>
        Right_FixtureLeftFAClamp = 91,
        /// <summary>2工位夹具右FA气缸松开信号 | X124 bit92</summary>
        Right_FixtureRightFARelease = 92,
        /// <summary>2工位夹具右FA气缸夹紧信号 | X125 bit93</summary>
        Right_FixtureRightFAClamp = 93,

        /// <summary>2工位UV升降气缸缩回信号 | X130 bit96</summary>
        Right_UVLiftRetract = 96,
        /// <summary>2工位UV升降气缸伸出信号 | X131 bit97</summary>
        Right_UVLiftExtend = 97,
        /// <summary>2工位UV伸缩气缸缩回信号 | X132 bit98</summary>
        Right_UVTelescopeRetract = 98,
        /// <summary>2工位UV伸缩气缸伸出信号 | X133 bit99</summary>
        Right_UVTelescopeExtend = 99,
        /// <summary>2工位点胶升降气缸缩回信号 | X134 bit100</summary>
        Right_DispenseLiftRetract = 100,
        /// <summary>2工位点胶升降气缸伸出信号 | X135 bit101</summary>
        Right_DispenseLiftExtend = 101,
        /// <summary>2工位点胶伸缩气缸缩回信号 | X136 bit102</summary>
        Right_DispenseTelescopeRetract = 102,
        /// <summary>2工位点胶伸缩气缸伸出信号 | X137 bit103</summary>
        Right_DispenseTelescopeExtend = 103,

        #endregion

        // ==================== Module-4: R3-3200-4（右工位真空/UV/温度）bits 104~135 ====================
        #region Module-4

        /// <summary>2工位治具真空检测 | X140 bit104</summary>
        Right_FixtureVacuum = 104,
        /// <summary>2工位左耦合模组真空检测 | X141 bit105</summary>
        Right_LCouplingVacuum = 105,
        /// <summary>2工位右耦合模组真空检测 | X142 bit106</summary>
        Right_RCouplingVacuum = 106,

        /// <summary>2工位夹爪UV控制器状态信号 | X150 bit112</summary>
        Right_GripperUVStatus = 112,
        /// <summary>2工位夹爪UV控制器报警信号 | X151 bit113</summary>
        Right_GripperUVAlarm = 113,
        /// <summary>2工位相机UV控制器状态信号 | X152 bit114</summary>
        Right_CameraUVStatus = 114,
        /// <summary>2工位相机UV控制器报警信号 | X153 bit115</summary>
        Right_CameraUVAlarm = 115,
        /// <summary>2工位夹具加热温度到达设定 | X154 bit116</summary>
        Right_FixtureHeatReached = 116,

        #endregion
    }

    /// <summary>
    /// 全部输出信号（4个R3-0032-N模块，共128路）
    /// Module-5: bits 8~39 | Module-6: bits 40~71 | Module-7: bits 72~103 | Module-8: bits 104~135
    /// </summary>
    public enum AllOutputs
    {
        // ==================== Module-5: R3-0032-N-1（左工位通用输出）bits 8~39 ====================
        #region Module-5

        /// <summary>三色灯红 | Y00 bit8</summary>
        TowerRed = 8,
        /// <summary>三色灯黄 | Y01 bit9</summary>
        TowerYellow = 9,
        /// <summary>三色灯绿 | Y02 bit10</summary>
        TowerGreen = 10,
        /// <summary>蜂鸣器 | Y03 bit11</summary>
        Buzzer = 11,
        /// <summary>1工位上相机光源 | Y04 bit12</summary>
        Left_CamUpLight = 12,
        /// <summary>1工位侧相机光源 | Y05 bit13</summary>
        Left_CamSideLight = 13,

        /// <summary>1工位OE电口插拔气缸缩回 | Y10 bit16</summary>
        Left_OEPlugRetract = 16,
        /// <summary>1工位OE电口插拔气缸伸出 | Y11 bit17</summary>
        Left_OEPlugExtend = 17,
        /// <summary>1工位擦胶伸缩气缸缩回 | Y12 bit18</summary>
        Left_WipeTelescopeRetract = 18,
        /// <summary>1工位擦胶伸缩气缸伸出 | Y13 bit19</summary>
        Left_WipeTelescopeExtend = 19,
        /// <summary>1工位治具夹紧气缸松开 | Y14 bit20</summary>
        Left_JigClampRelease = 20,
        /// <summary>1工位治具夹紧气缸夹紧 | Y15 bit21</summary>
        Left_JigClampClamp = 21,

        /// <summary>1工位夹具左前FA气缸松开 | Y20 bit24</summary>
        Left_FixtureLeftFrontFARelease = 24,
        /// <summary>1工位夹具左前FA气缸夹紧 | Y21 bit25</summary>
        Left_FixtureLeftFrontFAClamp = 25,
        /// <summary>1工位夹具左FA气缸松开 | Y22 bit26</summary>
        Left_FixtureLeftFARelease = 26,
        /// <summary>1工位夹具左FA气缸夹紧 | Y23 bit27</summary>
        Left_FixtureLeftFAClamp = 27,
        /// <summary>1工位夹具右FA气缸松开 | Y24 bit28</summary>
        Left_FixtureRightFARelease = 28,
        /// <summary>1工位夹具右FA气缸夹紧 | Y25 bit29</summary>
        Left_FixtureRightFAClamp = 29,

        /// <summary>1工位UV升降气缸缩回 | Y30 bit32</summary>
        Left_UVLiftRetract = 32,
        /// <summary>1工位UV升降气缸伸出 | Y31 bit33</summary>
        Left_UVLiftExtend = 33,
        /// <summary>1工位UV伸缩气缸缩回 | Y32 bit34</summary>
        Left_UVTelescopeRetract = 34,
        /// <summary>1工位UV伸缩气缸伸出 | Y33 bit35</summary>
        Left_UVTelescopeExtend = 35,
        /// <summary>1工位点胶升降气缸缩回 | Y34 bit36</summary>
        Left_DispenseLiftRetract = 36,
        /// <summary>1工位点胶升降气缸伸出 | Y35 bit37</summary>
        Left_DispenseLiftExtend = 37,
        /// <summary>1工位点胶伸缩气缸缩回 | Y36 bit38</summary>
        Left_DispenseTelescopeRetract = 38,
        /// <summary>1工位点胶伸缩气缸伸出 | Y37 bit39</summary>
        Left_DispenseTelescopeExtend = 39,

        #endregion

        // ==================== Module-6: R3-0032-N-2（左工位真空/UV/加热）bits 40~71 ====================
        #region Module-6

        /// <summary>1工位治具真空 | Y40 bit40</summary>
        Left_FixtureVacuum = 40,
        /// <summary>1工位左耦合夹爪真空 | Y41 bit41</summary>
        Left_LCouplingGripperVacuum = 41,
        /// <summary>1工位右耦合夹爪真空 | Y42 bit42</summary>
        Left_RCouplingGripperVacuum = 42,
        /// <summary>1工位上相机UV光源 | Y47 bit47</summary>
        Left_CamUpUVLight = 47,

        /// <summary>1工位左夹爪UV光源 | Y50 bit48</summary>
        Left_LGripperUVLight = 48,
        /// <summary>1工位右夹爪UV光源 | Y51 bit49</summary>
        Left_RGripperUVLight = 49,
        /// <summary>1工位夹具加热 | Y52 bit50</summary>
        Left_FixtureHeat = 50,
        /// <summary>1工位点胶控制器 | Y53 bit51</summary>
        Left_DispenseController = 51,

        #endregion

        // ==================== Module-7: R3-0032-N-3（右工位通用输出）bits 72~103 ====================
        #region Module-7

        /// <summary>2工位上相机光源 | Y104 bit76</summary>
        Right_CamUpLight = 76,
        /// <summary>2工位侧相机光源 | Y105 bit77</summary>
        Right_CamSideLight = 77,

        /// <summary>2工位OE电口气缸缩回 | Y110 bit80</summary>
        Right_OEPlugRetract = 80,
        /// <summary>2工位OE电口气缸伸出 | Y111 bit81</summary>
        Right_OEPlugExtend = 81,
        /// <summary>2工位擦胶伸缩气缸缩回 | Y112 bit82</summary>
        Right_WipeTelescopeRetract = 82,
        /// <summary>2工位擦胶伸缩气缸伸出 | Y113 bit83</summary>
        Right_WipeTelescopeExtend = 83,
        /// <summary>2工位治具夹紧气缸夹紧 | Y114 bit84</summary>
        Right_JigClampClamp = 84,
        /// <summary>2工位治具夹紧气缸夹紧 | Y115 bit85</summary>
        Right_JigClampClamp2 = 85,

        /// <summary>2工位夹具左前FA气缸松开 | Y120 bit88</summary>
        Right_FixtureLeftFrontFARelease = 88,
        /// <summary>2工位夹具左前FA气缸夹紧 | Y121 bit89</summary>
        Right_FixtureLeftFrontFAClamp = 89,
        /// <summary>2工位夹具左FA气缸松开 | Y122 bit90</summary>
        Right_FixtureLeftFARelease = 90,
        /// <summary>2工位夹具左FA气缸夹紧 | Y123 bit91</summary>
        Right_FixtureLeftFAClamp = 91,
        /// <summary>2工位夹具右FA气缸松开 | Y124 bit92</summary>
        Right_FixtureRightFARelease = 92,
        /// <summary>2工位夹具右FA气缸夹紧 | Y125 bit93</summary>
        Right_FixtureRightFAClamp = 93,

        /// <summary>2工位UV升降气缸缩回 | Y130 bit96</summary>
        Right_UVLiftRetract = 96,
        /// <summary>2工位UV升降气缸伸出 | Y131 bit97</summary>
        Right_UVLiftExtend = 97,
        /// <summary>2工位UV伸缩气缸缩回 | Y132 bit98</summary>
        Right_UVTelescopeRetract = 98,
        /// <summary>2工位UV伸缩气缸伸出 | Y133 bit99</summary>
        Right_UVTelescopeExtend = 99,
        /// <summary>2工位点胶升降气缸缩回 | Y134 bit100</summary>
        Right_DispenseLiftRetract = 100,
        /// <summary>2工位点胶升降气缸伸出 | Y135 bit101</summary>
        Right_DispenseLiftExtend = 101,
        /// <summary>2工位点胶伸缩气缸缩回 | Y136 bit102</summary>
        Right_DispenseTelescopeRetract = 102,
        /// <summary>2工位点胶伸缩气缸伸出 | Y137 bit103</summary>
        Right_DispenseTelescopeExtend = 103,

        #endregion

        // ==================== Module-8: R3-0032-N-4（右工位真空/UV/加热）bits 104~135 ====================
        #region Module-8

        /// <summary>2工位治具真空 | Y140 bit104</summary>
        Right_FixtureVacuum = 104,
        /// <summary>2工位左耦合夹爪真空 | Y141 bit105</summary>
        Right_LCouplingGripperVacuum = 105,
        /// <summary>2工位右耦合夹爪真空 | Y142 bit106</summary>
        Right_RCouplingGripperVacuum = 106,
        /// <summary>2工位上相机UV光源 | Y147 bit111</summary>
        Right_CamUpUVLight = 111,

        /// <summary>2工位左夹爪UV光源 | Y150 bit112</summary>
        Right_LGripperUVLight = 112,
        /// <summary>2工位右夹爪UV光源 | Y151 bit113</summary>
        Right_RGripperUVLight = 113,
        /// <summary>2工位夹具加热 | Y152 bit114</summary>
        Right_FixtureHeat = 114,
        /// <summary>2工位点胶控制器 | Y153 bit115</summary>
        Right_DispenseController = 115,

        #endregion
    }

    /// <summary>
    /// 输入信号的中文显示名称
    /// </summary>
    public static class SignalNames
    {
        public static readonly Dictionary<AllInputs, string> Module1 = new()
        {
            [AllInputs.Left_Button1] = "工位1按钮1",
            [AllInputs.Left_Button2] = "工位1按钮2",
            [AllInputs.Left_Button3] = "工位1按钮3",
            [AllInputs.Left_Button4] = "工位1按钮4",
            [AllInputs.Left_Button5] = "工位1按钮5",
            [AllInputs.Left_Button6] = "工位1按钮6",
            [AllInputs.Left_EmergencyStop] = "工位1急停",
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
            [AllInputs.Right_Button1] = "工位2按钮1",
            [AllInputs.Right_Button2] = "工位2按钮2",
            [AllInputs.Right_Button3] = "工位2按钮3",
            [AllInputs.Right_Button4] = "工位2按钮4",
            [AllInputs.Right_Button5] = "工位2按钮5",
            [AllInputs.Right_Button6] = "工位2按钮6",
            [AllInputs.Right_EmergencyStop] = "工位2急停",
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

        public static readonly Dictionary<AllOutputs, string> Module5 = new()
        {
            [AllOutputs.TowerRed] = "三色灯红",
            [AllOutputs.TowerYellow] = "三色灯黄",
            [AllOutputs.TowerGreen] = "三色灯绿",
            [AllOutputs.Buzzer] = "蜂鸣器",
            [AllOutputs.Left_CamUpLight] = "上相机光源",
            [AllOutputs.Left_CamSideLight] = "侧相机光源",
            [AllOutputs.Left_OEPlugRetract] = "OE电口插拔气缸缩回",
            [AllOutputs.Left_OEPlugExtend] = "OE电口插拔气缸伸出",
            [AllOutputs.Left_WipeTelescopeRetract] = "擦胶伸缩气缸缩回",
            [AllOutputs.Left_WipeTelescopeExtend] = "擦胶伸缩气缸伸出",
            [AllOutputs.Left_JigClampRelease] = "治具夹紧气缸松开",
            [AllOutputs.Left_JigClampClamp] = "治具夹紧气缸夹紧",
            [AllOutputs.Left_FixtureLeftFrontFARelease] = "夹具左前FA松开",
            [AllOutputs.Left_FixtureLeftFrontFAClamp] = "夹具左前FA夹紧",
            [AllOutputs.Left_FixtureLeftFARelease] = "夹具左FA松开",
            [AllOutputs.Left_FixtureLeftFAClamp] = "夹具左FA夹紧",
            [AllOutputs.Left_FixtureRightFARelease] = "夹具右FA松开",
            [AllOutputs.Left_FixtureRightFAClamp] = "夹具右FA夹紧",
            [AllOutputs.Left_UVLiftRetract] = "UV升降气缸缩回",
            [AllOutputs.Left_UVLiftExtend] = "UV升降气缸伸出",
            [AllOutputs.Left_UVTelescopeRetract] = "UV伸缩气缸缩回",
            [AllOutputs.Left_UVTelescopeExtend] = "UV伸缩气缸伸出",
            [AllOutputs.Left_DispenseLiftRetract] = "点胶升降气缸缩回",
            [AllOutputs.Left_DispenseLiftExtend] = "点胶升降气缸伸出",
            [AllOutputs.Left_DispenseTelescopeRetract] = "点胶伸缩气缸缩回",
            [AllOutputs.Left_DispenseTelescopeExtend] = "点胶伸缩气缸伸出",
        };

        public static readonly Dictionary<AllOutputs, string> Module6 = new()
        {
            [AllOutputs.Left_FixtureVacuum] = "治具真空",
            [AllOutputs.Left_LCouplingGripperVacuum] = "左耦合夹爪真空",
            [AllOutputs.Left_RCouplingGripperVacuum] = "右耦合夹爪真空",
            [AllOutputs.Left_CamUpUVLight] = "上相机UV光源",
            [AllOutputs.Left_LGripperUVLight] = "左夹爪UV光源",
            [AllOutputs.Left_RGripperUVLight] = "右夹爪UV光源",
            [AllOutputs.Left_FixtureHeat] = "夹具加热",
            [AllOutputs.Left_DispenseController] = "点胶控制器",
        };

        public static readonly Dictionary<AllOutputs, string> Module7 = new()
        {
            [AllOutputs.Right_CamUpLight] = "上相机光源",
            [AllOutputs.Right_CamSideLight] = "侧相机光源",
            [AllOutputs.Right_OEPlugRetract] = "OE电口气缸缩回",
            [AllOutputs.Right_OEPlugExtend] = "OE电口气缸伸出",
            [AllOutputs.Right_WipeTelescopeRetract] = "擦胶伸缩气缸缩回",
            [AllOutputs.Right_WipeTelescopeExtend] = "擦胶伸缩气缸伸出",
            [AllOutputs.Right_JigClampClamp] = "治具夹紧气缸夹紧",
            [AllOutputs.Right_JigClampClamp2] = "治具夹紧气缸夹紧2",
            [AllOutputs.Right_FixtureLeftFrontFARelease] = "夹具左前FA松开",
            [AllOutputs.Right_FixtureLeftFrontFAClamp] = "夹具左前FA夹紧",
            [AllOutputs.Right_FixtureLeftFARelease] = "夹具左FA松开",
            [AllOutputs.Right_FixtureLeftFAClamp] = "夹具左FA夹紧",
            [AllOutputs.Right_FixtureRightFARelease] = "夹具右FA松开",
            [AllOutputs.Right_FixtureRightFAClamp] = "夹具右FA夹紧",
            [AllOutputs.Right_UVLiftRetract] = "UV升降气缸缩回",
            [AllOutputs.Right_UVLiftExtend] = "UV升降气缸伸出",
            [AllOutputs.Right_UVTelescopeRetract] = "UV伸缩气缸缩回",
            [AllOutputs.Right_UVTelescopeExtend] = "UV伸缩气缸伸出",
            [AllOutputs.Right_DispenseLiftRetract] = "点胶升降气缸缩回",
            [AllOutputs.Right_DispenseLiftExtend] = "点胶升降气缸伸出",
            [AllOutputs.Right_DispenseTelescopeRetract] = "点胶伸缩气缸缩回",
            [AllOutputs.Right_DispenseTelescopeExtend] = "点胶伸缩气缸伸出",
        };

        public static readonly Dictionary<AllOutputs, string> Module8 = new()
        {
            [AllOutputs.Right_FixtureVacuum] = "治具真空",
            [AllOutputs.Right_LCouplingGripperVacuum] = "左耦合夹爪真空",
            [AllOutputs.Right_RCouplingGripperVacuum] = "右耦合夹爪真空",
            [AllOutputs.Right_CamUpUVLight] = "上相机UV光源",
            [AllOutputs.Right_LGripperUVLight] = "左夹爪UV光源",
            [AllOutputs.Right_RGripperUVLight] = "右夹爪UV光源",
            [AllOutputs.Right_FixtureHeat] = "夹具加热",
            [AllOutputs.Right_DispenseController] = "点胶控制器",
        };
    }
}
