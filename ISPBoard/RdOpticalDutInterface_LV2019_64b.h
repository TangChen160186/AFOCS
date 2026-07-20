#pragma once
#include "extcode.h"
#ifdef __cplusplus
extern "C" {
#endif
typedef struct {
	int32_t dimSize;
	LStrHandle String[1];
	} TD1;
typedef TD1 **TD1Hdl;

typedef struct {
	int32_t dimSize;
	uint16_t Numeric[1];
	} TD2;
typedef TD2 **TD2Hdl;

typedef struct {
	int32_t dimSize;
	double Numeric[1];
	} TD3;
typedef TD3 **TD3Hdl;

typedef struct {
	int32_t dimSize;
	uint32_t Numeric[1];
	} TD4;
typedef TD4 **TD4Hdl;

typedef struct {
	int32_t dimSize;
	uint16_t _[1];
	} TD5;
typedef TD5 **TD5Hdl;

typedef struct {
	int32_t dimSize;
	LStrHandle DeviceVisa[1];
	} TD6;
typedef TD6 **TD6Hdl;

typedef struct {
	int32_t dimSize;
	double _[1];
	} TD7;
typedef TD7 **TD7Hdl;

typedef struct {
	int32_t dimSize;
	LStrHandle _[1];
	} TD8;
typedef TD8 **TD8Hdl;

typedef struct {
	int32_t dimSize;
	uint8_t _[1];
	} TD9;
typedef TD9 **TD9Hdl;


/*!
 * InterfaceInitial
 */
void __stdcall InterfaceInitial(Path *I_ProductCfgFile, TD1Hdl *O_AppNames, 
	LStrHandle *O_ErrInfo);
/*!
 * DutReadWrite
 */
void __stdcall DutReadWrite(uint8_t DutSlot, uint8_t DutChannel, 
	LStrHandle *AppName, uint16_t Operation, TD2Hdl *DataIn, TD2Hdl *DataOut, 
	LStrHandle *ErrInfo);
/*!
 * FormularCalc
 */
void __stdcall FormularCalc(LStrHandle *I_AppName, TD3Hdl *I_DataIn, 
	double *O_Result, LStrHandle *O_ErrInfo);
/*!
 * DutRegRead
 */
void __stdcall DutRegRead(uint8_t I_DutSlot, uint8_t I_DutChannel, 
	LStrHandle *I_AppName, TD4Hdl *O_DataOut, LStrHandle *O_ErrInfo);
/*!
 * DutRegReadWrite
 */
void __stdcall DutRegReadWrite(uint8_t I_DutSlot, uint8_t I_DutChannel, 
	LStrHandle *I_AppName, uint16_t I_Operation, TD4Hdl *I_DataIn, 
	TD4Hdl *O_DataOut, LStrHandle *O_ErrInfo);
/*!
 * DutHeaterScan
 */
void __stdcall DutHeaterScan(uint8_t I_DutSlot, uint8_t I_DutChannel, 
	LStrHandle *I_AppName, TD2Hdl *I_DataIn, TD5Hdl *O_MpdOutADC, 
	TD2Hdl *O_MpdInADC, LStrHandle *O_ErrInfo);
/*!
 * DutHeaterScanEx
 */
void __stdcall DutHeaterScanEx(uint32_t DevIndex, uint8_t DutSlot, 
	uint8_t DutChannel, LStrHandle *AppName, TD2Hdl *DataIn, TD5Hdl *MpdOutADC, 
	TD2Hdl *MpdInADC, LStrHandle *ErrInfo);
/*!
 * DutReadWriteEx
 */
void __stdcall DutReadWriteEx(uint32_t DevIndex, uint8_t DutSlot, 
	uint8_t DutChannel, LStrHandle *AppName, uint16_t Operation, TD2Hdl *DataIn, 
	TD2Hdl *DataOut, LStrHandle *ErrInfo);
/*!
 * InterfaceInitialEx
 */
void __stdcall InterfaceInitialEx(Path *ProductCfgFile, TD1Hdl *AppNames, 
	TD6Hdl *DeviceVisa, LStrHandle *ErrInfo);
/*!
 * Debug_Dbl_AddCalc
 */
void __stdcall Debug_Dbl_AddCalc(double a, double b, double *DataOut);
/*!
 * Debug_DblArray_InOut
 */
void __stdcall Debug_DblArray_InOut(TD7Hdl *DataIn, TD7Hdl *DataOut);
/*!
 * Debug_DblArray_Out
 */
void __stdcall Debug_DblArray_Out(TD7Hdl *DataOut);
/*!
 * Debug_FilePath_InOut
 */
void __stdcall Debug_FilePath_InOut(Path *PathIn, Path *PathOut);
/*!
 * Debug_FilePath_Out
 */
void __stdcall Debug_FilePath_Out(Path *FilePathOut);
/*!
 * Debug_String_InOut
 */
void __stdcall Debug_String_InOut(LStrHandle *DataIn, LStrHandle *DataOut);
/*!
 * Debug_String_Out
 */
void __stdcall Debug_String_Out(LStrHandle *DataOut);
/*!
 * Debug_StringArray_InOut
 */
void __stdcall Debug_StringArray_InOut(TD8Hdl *DataIn, TD8Hdl *DataOut);
/*!
 * Debug_StringArray_Out
 */
void __stdcall Debug_StringArray_Out(TD8Hdl *DataOut);
/*!
 * Debug_U16Array_InOut
 */
void __stdcall Debug_U16Array_InOut(TD5Hdl *DataIn, TD5Hdl *DataOut);
/*!
 * Debug_U16Array_Out
 */
void __stdcall Debug_U16Array_Out(TD5Hdl *DataOut);
/*!
 * InterfaceInitial_C
 */
void __stdcall InterfaceInitial_C(LStrHandle *ProductCfgFile, 
	TD1Hdl *AppNames, LStrHandle *ErrInfo);
/*!
 * InterfaceInitialEx_C
 */
void __stdcall InterfaceInitialEx_C(LStrHandle *ProductCfgFile, 
	TD1Hdl *AppNames, TD6Hdl *DeviceVisa, LStrHandle *ErrInfo);
/*!
 * DutEnterEngEx
 */
void __stdcall DutEnterEngEx(uint32_t DevIndex, uint8_t EnterEng, 
	TD9Hdl *EngStatus, LStrHandle *ErrInfo);

MgErr __cdecl LVDLLStatus(char *errStr, int errStrLen, void *module);

void __cdecl SetExecuteVIsInPrivateExecutionSystem(Bool32 value);

#ifdef __cplusplus
} // extern "C"
#endif

