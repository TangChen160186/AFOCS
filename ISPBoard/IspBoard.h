#pragma once

#ifdef ISPBORAD_EXPORTS
#define ISPBORAD_API __declspec(dllexport)
#else
#define ISPBORAD_API __declspec(dllimport)
#endif

#include "RdOpticalDutInterface_LV2019_64b.h"


extern "C" ISPBORAD_API void
IspInterfaceInitialEx_c(const char* productCfgFile, char*** appNames,
    uint32_t* appNameCount, char*** deviceVisa,
    uint32_t* deviceVisaCount, char** errorInfo, 
    uint16_t* errorSize);




extern "C" ISPBORAD_API void IspDutReadWriteEx(uint32_t devIndex,
                                               uint8_t dutSlot,uint8_t dutChannel,const char* appName,uint8_t operation,
                                               uint16_t* dataIn,uint16_t dataInCount,uint16_t* dataOut,
                                               uint16_t* dataOutCount,char** errorInfo,uint16_t* errorSize);

extern "C" ISPBORAD_API void IspFormularCalc(const char* appName,
    double* dataIn, 
    uint16_t dataInCount, 
    double* result, 
    char** errorInfo, 
    uint16_t* errorSize);


extern "C" ISPBORAD_API void IspDutHeaterScanEx(uint32_t devIndex,
    uint8_t dutSlot, uint8_t dutChannel, const char* appName, 
    uint16_t* dataIn, uint16_t dataInCount, 
    uint16_t* mpdOutAdc,uint16_t* mpdOutAdcCount, 
    uint16_t* mpdInAdc,uint16_t* mpdInAdcCount, 
    char** errorInfo, uint16_t* errorSize);
