using System.ComponentModel;

namespace AFOCS.Devices.IO;

/// <summary>
/// 全部输入信号（4个R3-3200模块，共128路）
/// Module-1: bits 8~39 | Module-2: bits 40~71 | Module-3: bits 72~103 | Module-4: bits 104~135
/// </summary>
public enum AllInputs
{
    // ==================== Module-1: R3-3200-1（左工位通用IO）bits 8~39 ====================
    #region Module-1

    /// <summary>左工位按钮1 | X00 bit8</summary>
    [Description("左工位按钮1")] Left_Button1 = 8,
    /// <summary>左工位按钮2 | X01 bit9</summary>
    [Description("左工位按钮2")] Left_Button2 = 9,
    /// <summary>左工位按钮3 | X02 bit10</summary>
    [Description("左工位按钮3")] Left_Button3 = 10,
    /// <summary>左工位按钮4 | X03 bit11</summary>
    [Description("左工位按钮4")] Left_Button4 = 11,
    /// <summary>左工位按钮5 | X04 bit12</summary>
    [Description("左工位按钮5")] Left_Button5 = 12,
    /// <summary>左工位按钮6 | X05 bit13</summary>
    [Description("左工位按钮6")] Left_Button6 = 13,
    /// <summary>左工位急停 | X06 bit14</summary>
    [Description("左工位急停")] Left_EmergencyStop = 14,

    /// <summary>左工位OE电口插拔气缸缩回信号 | X10 bit16</summary>
    [Description("左工位OE电口插拔气缸缩回")] Left_OEPlugRetract = 16,
    /// <summary>左工位OE电口插拔气缸伸出信号 | X11 bit17</summary>
    [Description("左工位OE电口插拔气缸伸出")] Left_OEPlugExtend = 17,
    /// <summary>左工位擦胶夹紧气缸松开信号 | X12 bit18</summary>
    [Description("左工位擦胶夹紧气缸松开")] Left_WipeClampRelease = 18,
    /// <summary>左工位擦胶夹紧气缸夹紧信号 | X13 bit19</summary>
    [Description("左工位擦胶夹紧气缸夹紧")] Left_WipeClampClamp = 19,
    /// <summary>左工位治具夹紧气缸松开信号 | X14 bit20</summary>
    [Description("左工位治具夹紧气缸松开")] Left_JigClampRelease = 20,
    /// <summary>左工位治具夹紧气缸夹紧信号 | X15 bit21</summary>
    [Description("左工位治具夹紧气缸夹紧")] Left_JigClampClamp = 21,

    /// <summary>左工位夹具左前FA气缸松开信号 | X20 bit24</summary>
    [Description("左工位夹具左前FA松开")] Left_FixtureLeftFrontFARelease = 24,
    /// <summary>左工位夹具左前FA气缸夹紧信号 | X21 bit25</summary>
    [Description("左工位夹具左前FA夹紧")] Left_FixtureLeftFrontFAClamp = 25,
    /// <summary>左工位夹具左FA气缸松开信号 | X22 bit26</summary>
    [Description("左工位夹具左FA松开")] Left_FixtureLeftFARelease = 26,
    /// <summary>左工位夹具左FA气缸夹紧信号 | X23 bit27</summary>
    [Description("左工位夹具左FA夹紧")] Left_FixtureLeftFAClamp = 27,
    /// <summary>左工位夹具右FA气缸松开信号 | X24 bit28</summary>
    [Description("左工位夹具右FA松开")] Left_FixtureRightFARelease = 28,
    /// <summary>左工位夹具右FA气缸夹紧信号 | X25 bit29</summary>
    [Description("左工位夹具右FA夹紧")] Left_FixtureRightFAClamp = 29,

    /// <summary>左工位UV升降气缸缩回信号 | X30 bit32</summary>
    [Description("左工位UV升降气缸缩回")] Left_UVLiftRetract = 32,
    /// <summary>左工位UV升降气缸伸出信号 | X31 bit33</summary>
    [Description("左工位UV升降气缸伸出")] Left_UVLiftExtend = 33,
    /// <summary>左工位UV伸缩气缸缩回信号 | X32 bit34</summary>
    [Description("左工位UV伸缩气缸缩回")] Left_UVTelescopeRetract = 34,
    /// <summary>左工位UV伸缩气缸伸出信号 | X33 bit35</summary>
    [Description("左工位UV伸缩气缸伸出")] Left_UVTelescopeExtend = 35,
    /// <summary>左工位点胶升降气缸缩回信号 | X34 bit36</summary>
    [Description("左工位点胶升降气缸缩回")] Left_DispenseLiftRetract = 36,
    /// <summary>左工位点胶升降气缸伸出信号 | X35 bit37</summary>
    [Description("左工位点胶升降气缸伸出")] Left_DispenseLiftExtend = 37,
    /// <summary>左工位点胶伸缩气缸缩回信号 | X36 bit38</summary>
    [Description("左工位点胶伸缩气缸缩回")] Left_DispenseTelescopeRetract = 38,
    /// <summary>左工位点胶伸缩气缸伸出信号 | X37 bit39</summary>
    [Description("左工位点胶伸缩气缸伸出")] Left_DispenseTelescopeExtend = 39,

    #endregion

    // ==================== Module-2: R3-3200-2（左工位真空/UV/温度）bits 40~71 ====================
    #region Module-2

    /// <summary>左工位夹具真空检测 | X40 bit40</summary>
    [Description("左工位夹具真空检测")] Left_FixtureVacuum = 40,
    /// <summary>左工位左耦合夹爪真空检测 | X41 bit41</summary>
    [Description("左工位左耦合夹爪真空检测")] Left_LCouplingGripperVacuum = 41,
    /// <summary>左工位右耦合夹爪真空检测 | X42 bit42</summary>
    [Description("左工位右耦合夹爪真空检测")] Left_RCouplingGripperVacuum = 42,

    /// <summary>左工位夹爪UV控制器状态信号 | X50 bit48</summary>
    [Description("左工位夹爪UV状态")] Left_GripperUVStatus = 48,
    /// <summary>左工位夹爪UV控制器报警信号 | X51 bit49</summary>
    [Description("左工位夹爪UV报警")] Left_GripperUVAlarm = 49,
    /// <summary>左工位相机UV控制器状态信号 | X52 bit50</summary>
    [Description("左工位相机UV状态")] Left_CameraUVStatus = 50,
    /// <summary>左工位相机UV控制器报警信号 | X53 bit51</summary>
    [Description("左工位相机UV报警")] Left_CameraUVAlarm = 51,
    /// <summary>左工位夹具加热温度到达设定 | X54 bit52</summary>
    [Description("左工位夹具加热到达")] Left_FixtureHeatReached = 52,

    #endregion

    // ==================== Module-3: R3-3200-3（右工位通用IO）bits 72~103 ====================
    #region Module-3

    /// <summary>右工位按钮1 | X100 bit72</summary>
    [Description("右工位按钮1")] Right_Button1 = 72,
    /// <summary>右工位按钮2 | X101 bit73</summary>
    [Description("右工位按钮2")] Right_Button2 = 73,
    /// <summary>右工位按钮3 | X102 bit74</summary>
    [Description("右工位按钮3")] Right_Button3 = 74,
    /// <summary>右工位按钮4 | X103 bit75</summary>
    [Description("右工位按钮4")] Right_Button4 = 75,
    /// <summary>右工位按钮5 | X104 bit76</summary>
    [Description("右工位按钮5")] Right_Button5 = 76,
    /// <summary>右工位按钮6 | X105 bit77</summary>
    [Description("右工位按钮6")] Right_Button6 = 77,
    /// <summary>右工位急停 | X106 bit78</summary>
    [Description("右工位急停")] Right_EmergencyStop = 78,

    /// <summary>右工位OE电口插拔气缸缩回信号 | X110 bit80</summary>
    [Description("右工位OE电口插拔气缸缩回")] Right_OEPlugRetract = 80,
    /// <summary>右工位OE电口插拔气缸伸出信号 | X111 bit81</summary>
    [Description("右工位OE电口插拔气缸伸出")] Right_OEPlugExtend = 81,
    /// <summary>右工位擦胶伸缩气缸缩回信号 | X112 bit82</summary>
    [Description("右工位擦胶伸缩气缸缩回")] Right_WipeTelescopeRetract = 82,
    /// <summary>右工位擦胶伸缩气缸伸出信号 | X113 bit83</summary>
    [Description("右工位擦胶伸缩气缸伸出")] Right_WipeTelescopeExtend = 83,
    /// <summary>右工位治具夹紧气缸夹紧信号 | X114 bit84</summary>
    [Description("右工位治具夹紧气缸夹紧")] Right_JigClampClamp = 84,
    /// <summary>右工位治具夹紧气缸夹紧信号 | X115 bit85</summary>
    [Description("右工位治具夹紧气缸夹紧2")] Right_JigClampClamp2 = 85,

    /// <summary>右工位夹具左前FA气缸松开信号 | X120 bit88</summary>
    [Description("右工位夹具左前FA松开")] Right_FixtureLeftFrontFARelease = 88,
    /// <summary>右工位夹具左前FA气缸夹紧信号 | X121 bit89</summary>
    [Description("右工位夹具左前FA夹紧")] Right_FixtureLeftFrontFAClamp = 89,
    /// <summary>右工位夹具左FA气缸松开信号 | X122 bit90</summary>
    [Description("右工位夹具左FA松开")] Right_FixtureLeftFARelease = 90,
    /// <summary>右工位夹具左FA气缸夹紧信号 | X123 bit91</summary>
    [Description("右工位夹具左FA夹紧")] Right_FixtureLeftFAClamp = 91,
    /// <summary>右工位夹具右FA气缸松开信号 | X124 bit92</summary>
    [Description("右工位夹具右FA松开")] Right_FixtureRightFARelease = 92,
    /// <summary>右工位夹具右FA气缸夹紧信号 | X125 bit93</summary>
    [Description("右工位夹具右FA夹紧")] Right_FixtureRightFAClamp = 93,

    /// <summary>右工位UV升降气缸缩回信号 | X130 bit96</summary>
    [Description("右工位UV升降气缸缩回")] Right_UVLiftRetract = 96,
    /// <summary>右工位UV升降气缸伸出信号 | X131 bit97</summary>
    [Description("右工位UV升降气缸伸出")] Right_UVLiftExtend = 97,
    /// <summary>右工位UV伸缩气缸缩回信号 | X132 bit98</summary>
    [Description("右工位UV伸缩气缸缩回")] Right_UVTelescopeRetract = 98,
    /// <summary>右工位UV伸缩气缸伸出信号 | X133 bit99</summary>
    [Description("右工位UV伸缩气缸伸出")] Right_UVTelescopeExtend = 99,
    /// <summary>右工位点胶升降气缸缩回信号 | X134 bit100</summary>
    [Description("右工位点胶升降气缸缩回")] Right_DispenseLiftRetract = 100,
    /// <summary>右工位点胶升降气缸伸出信号 | X135 bit101</summary>
    [Description("右工位点胶升降气缸伸出")] Right_DispenseLiftExtend = 101,
    /// <summary>右工位点胶伸缩气缸缩回信号 | X136 bit102</summary>
    [Description("右工位点胶伸缩气缸缩回")] Right_DispenseTelescopeRetract = 102,
    /// <summary>右工位点胶伸缩气缸伸出信号 | X137 bit103</summary>
    [Description("右工位点胶伸缩气缸伸出")] Right_DispenseTelescopeExtend = 103,

    #endregion

    // ==================== Module-4: R3-3200-4（右工位真空/UV/温度）bits 104~135 ====================
    #region Module-4

    /// <summary>右工位治具真空检测 | X140 bit104</summary>
    [Description("右工位治具真空检测")] Right_FixtureVacuum = 104,
    /// <summary>右工位左耦合模组真空检测 | X141 bit105</summary>
    [Description("右工位左耦合模组真空检测")] Right_LCouplingVacuum = 105,
    /// <summary>右工位右耦合模组真空检测 | X142 bit106</summary>
    [Description("右工位右耦合模组真空检测")] Right_RCouplingVacuum = 106,

    /// <summary>右工位夹爪UV控制器状态信号 | X150 bit112</summary>
    [Description("右工位夹爪UV状态")] Right_GripperUVStatus = 112,
    /// <summary>右工位夹爪UV控制器报警信号 | X151 bit113</summary>
    [Description("右工位夹爪UV报警")] Right_GripperUVAlarm = 113,
    /// <summary>右工位相机UV控制器状态信号 | X152 bit114</summary>
    [Description("右工位相机UV状态")] Right_CameraUVStatus = 114,
    /// <summary>右工位相机UV控制器报警信号 | X153 bit115</summary>
    [Description("右工位相机UV报警")] Right_CameraUVAlarm = 115,
    /// <summary>右工位夹具加热温度到达设定 | X154 bit116</summary>
    [Description("右工位夹具加热到达")] Right_FixtureHeatReached = 116,

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
    [Description("三色灯红")] TowerRed = 8,
    /// <summary>三色灯黄 | Y01 bit9</summary>
    [Description("三色灯黄")] TowerYellow = 9,
    /// <summary>三色灯绿 | Y02 bit10</summary>
    [Description("三色灯绿")] TowerGreen = 10,
    /// <summary>蜂鸣器 | Y03 bit11</summary>
    [Description("蜂鸣器")] Buzzer = 11,
    /// <summary>左工位上相机光源 | Y04 bit12</summary>
    [Description("左工位上相机光源")] Left_CamUpLight = 12,
    /// <summary>左工位侧相机光源 | Y05 bit13</summary>
    [Description("左工位侧相机光源")] Left_CamSideLight = 13,

    /// <summary>左工位OE电口插拔气缸缩回 | Y10 bit16</summary>
    [Description("左工位OE电口插拔气缸缩回")] Left_OEPlugRetract = 16,
    /// <summary>左工位OE电口插拔气缸伸出 | Y11 bit17</summary>
    [Description("左工位OE电口插拔气缸伸出")] Left_OEPlugExtend = 17,
    /// <summary>左工位擦胶伸缩气缸缩回 | Y12 bit18</summary>
    [Description("左工位擦胶伸缩气缸缩回")] Left_WipeTelescopeRetract = 18,
    /// <summary>左工位擦胶伸缩气缸伸出 | Y13 bit19</summary>
    [Description("左工位擦胶伸缩气缸伸出")] Left_WipeTelescopeExtend = 19,
    /// <summary>左工位治具夹紧气缸松开 | Y14 bit20</summary>
    [Description("左工位治具夹紧气缸松开")] Left_JigClampRelease = 20,
    /// <summary>左工位治具夹紧气缸夹紧 | Y15 bit21</summary>
    [Description("左工位治具夹紧气缸夹紧")] Left_JigClampClamp = 21,

    /// <summary>左工位夹具左前FA气缸松开 | Y20 bit24</summary>
    [Description("左工位夹具左前FA松开")] Left_FixtureLeftFrontFARelease = 24,
    /// <summary>左工位夹具左前FA气缸夹紧 | Y21 bit25</summary>
    [Description("左工位夹具左前FA夹紧")] Left_FixtureLeftFrontFAClamp = 25,
    /// <summary>左工位夹具左FA气缸松开 | Y22 bit26</summary>
    [Description("左工位夹具左FA松开")] Left_FixtureLeftFARelease = 26,
    /// <summary>左工位夹具左FA气缸夹紧 | Y23 bit27</summary>
    [Description("左工位夹具左FA夹紧")] Left_FixtureLeftFAClamp = 27,
    /// <summary>左工位夹具右FA气缸松开 | Y24 bit28</summary>
    [Description("左工位夹具右FA松开")] Left_FixtureRightFARelease = 28,
    /// <summary>左工位夹具右FA气缸夹紧 | Y25 bit29</summary>
    [Description("左工位夹具右FA夹紧")] Left_FixtureRightFAClamp = 29,

    /// <summary>左工位UV升降气缸缩回 | Y30 bit32</summary>
    [Description("左工位UV升降气缸缩回")] Left_UVLiftRetract = 32,
    /// <summary>左工位UV升降气缸伸出 | Y31 bit33</summary>
    [Description("左工位UV升降气缸伸出")] Left_UVLiftExtend = 33,
    /// <summary>左工位UV伸缩气缸缩回 | Y32 bit34</summary>
    [Description("左工位UV伸缩气缸缩回")] Left_UVTelescopeRetract = 34,
    /// <summary>左工位UV伸缩气缸伸出 | Y33 bit35</summary>
    [Description("左工位UV伸缩气缸伸出")] Left_UVTelescopeExtend = 35,
    /// <summary>左工位点胶升降气缸缩回 | Y34 bit36</summary>
    [Description("左工位点胶升降气缸缩回")] Left_DispenseLiftRetract = 36,
    /// <summary>左工位点胶升降气缸伸出 | Y35 bit37</summary>
    [Description("左工位点胶升降气缸伸出")] Left_DispenseLiftExtend = 37,
    /// <summary>左工位点胶伸缩气缸缩回 | Y36 bit38</summary>
    [Description("左工位点胶伸缩气缸缩回")] Left_DispenseTelescopeRetract = 38,
    /// <summary>左工位点胶伸缩气缸伸出 | Y37 bit39</summary>
    [Description("左工位点胶伸缩气缸伸出")] Left_DispenseTelescopeExtend = 39,

    #endregion

    // ==================== Module-6: R3-0032-N-2（左工位真空/UV/加热）bits 40~71 ====================
    #region Module-6

    /// <summary>左工位治具真空 | Y40 bit40</summary>
    [Description("左工位治具真空")] Left_FixtureVacuum = 40,
    /// <summary>左工位左耦合夹爪真空 | Y41 bit41</summary>
    [Description("左工位左耦合夹爪真空")] Left_LCouplingGripperVacuum = 41,
    /// <summary>左工位右耦合夹爪真空 | Y42 bit42</summary>
    [Description("左工位右耦合夹爪真空")] Left_RCouplingGripperVacuum = 42,
    /// <summary>左工位上相机UV光源 | Y47 bit47</summary>
    [Description("左工位上相机UV光源")] Left_CamUpUVLight = 47,

    /// <summary>左工位左夹爪UV光源 | Y50 bit48</summary>
    [Description("左工位左夹爪UV光源")] Left_LGripperUVLight = 48,
    /// <summary>左工位右夹爪UV光源 | Y51 bit49</summary>
    [Description("左工位右夹爪UV光源")] Left_RGripperUVLight = 49,
    /// <summary>左工位夹具加热 | Y52 bit50</summary>
    [Description("左工位夹具加热")] Left_FixtureHeat = 50,
    /// <summary>左工位点胶控制器 | Y53 bit51</summary>
    [Description("左工位点胶控制器")] Left_DispenseController = 51,

    #endregion

    // ==================== Module-7: R3-0032-N-3（右工位通用输出）bits 72~103 ====================
    #region Module-7

    /// <summary>右工位上相机光源 | Y104 bit76</summary>
    [Description("右工位上相机光源")] Right_CamUpLight = 76,
    /// <summary>右工位侧相机光源 | Y105 bit77</summary>
    [Description("右工位侧相机光源")] Right_CamSideLight = 77,

    /// <summary>右工位OE电口气缸缩回 | Y110 bit80</summary>
    [Description("右工位OE电口气缸缩回")] Right_OEPlugRetract = 80,
    /// <summary>右工位OE电口气缸伸出 | Y111 bit81</summary>
    [Description("右工位OE电口气缸伸出")] Right_OEPlugExtend = 81,
    /// <summary>右工位擦胶伸缩气缸缩回 | Y112 bit82</summary>
    [Description("右工位擦胶伸缩气缸缩回")] Right_WipeTelescopeRetract = 82,
    /// <summary>右工位擦胶伸缩气缸伸出 | Y113 bit83</summary>
    [Description("右工位擦胶伸缩气缸伸出")] Right_WipeTelescopeExtend = 83,
    /// <summary>右工位治具夹紧气缸夹紧 | Y114 bit84</summary>
    [Description("右工位治具夹紧气缸夹紧")] Right_JigClampClamp = 84,
    /// <summary>右工位治具夹紧气缸夹紧 | Y115 bit85</summary>
    [Description("右工位治具夹紧气缸夹紧2")] Right_JigClampClamp2 = 85,

    /// <summary>右工位夹具左前FA气缸松开 | Y120 bit88</summary>
    [Description("右工位夹具左前FA松开")] Right_FixtureLeftFrontFARelease = 88,
    /// <summary>右工位夹具左前FA气缸夹紧 | Y121 bit89</summary>
    [Description("右工位夹具左前FA夹紧")] Right_FixtureLeftFrontFAClamp = 89,
    /// <summary>右工位夹具左FA气缸松开 | Y122 bit90</summary>
    [Description("右工位夹具左FA松开")] Right_FixtureLeftFARelease = 90,
    /// <summary>右工位夹具左FA气缸夹紧 | Y123 bit91</summary>
    [Description("右工位夹具左FA夹紧")] Right_FixtureLeftFAClamp = 91,
    /// <summary>右工位夹具右FA气缸松开 | Y124 bit92</summary>
    [Description("右工位夹具右FA松开")] Right_FixtureRightFARelease = 92,
    /// <summary>右工位夹具右FA气缸夹紧 | Y125 bit93</summary>
    [Description("右工位夹具右FA夹紧")] Right_FixtureRightFAClamp = 93,

    /// <summary>右工位UV升降气缸缩回 | Y130 bit96</summary>
    [Description("右工位UV升降气缸缩回")] Right_UVLiftRetract = 96,
    /// <summary>右工位UV升降气缸伸出 | Y131 bit97</summary>
    [Description("右工位UV升降气缸伸出")] Right_UVLiftExtend = 97,
    /// <summary>右工位UV伸缩气缸缩回 | Y132 bit98</summary>
    [Description("右工位UV伸缩气缸缩回")] Right_UVTelescopeRetract = 98,
    /// <summary>右工位UV伸缩气缸伸出 | Y133 bit99</summary>
    [Description("右工位UV伸缩气缸伸出")] Right_UVTelescopeExtend = 99,
    /// <summary>右工位点胶升降气缸缩回 | Y134 bit100</summary>
    [Description("右工位点胶升降气缸缩回")] Right_DispenseLiftRetract = 100,
    /// <summary>右工位点胶升降气缸伸出 | Y135 bit101</summary>
    [Description("右工位点胶升降气缸伸出")] Right_DispenseLiftExtend = 101,
    /// <summary>右工位点胶伸缩气缸缩回 | Y136 bit102</summary>
    [Description("右工位点胶伸缩气缸缩回")] Right_DispenseTelescopeRetract = 102,
    /// <summary>右工位点胶伸缩气缸伸出 | Y137 bit103</summary>
    [Description("右工位点胶伸缩气缸伸出")] Right_DispenseTelescopeExtend = 103,

    #endregion

    // ==================== Module-8: R3-0032-N-4（右工位真空/UV/加热）bits 104~135 ====================
    #region Module-8

    /// <summary>右工位治具真空 | Y140 bit104</summary>
    [Description("右工位治具真空")] Right_FixtureVacuum = 104,
    /// <summary>右工位左耦合夹爪真空 | Y141 bit105</summary>
    [Description("右工位左耦合夹爪真空")] Right_LCouplingGripperVacuum = 105,
    /// <summary>右工位右耦合夹爪真空 | Y142 bit106</summary>
    [Description("右工位右耦合夹爪真空")] Right_RCouplingGripperVacuum = 106,
    /// <summary>右工位上相机UV光源 | Y147 bit111</summary>
    [Description("右工位上相机UV光源")] Right_CamUpUVLight = 111,

    /// <summary>右工位左夹爪UV光源 | Y150 bit112</summary>
    [Description("右工位左夹爪UV光源")] Right_LGripperUVLight = 112,
    /// <summary>右工位右夹爪UV光源 | Y151 bit113</summary>
    [Description("右工位右夹爪UV光源")] Right_RGripperUVLight = 113,
    /// <summary>右工位夹具加热 | Y152 bit114</summary>
    [Description("右工位夹具加热")] Right_FixtureHeat = 114,
    /// <summary>右工位点胶控制器 | Y153 bit115</summary>
    [Description("右工位点胶控制器")] Right_DispenseController = 115,

    #endregion
}