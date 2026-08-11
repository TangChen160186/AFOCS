#include "IspBoard.h"

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <windows.h>
// ============================================================================
// LabVIEW Handle 转换辅助
// ============================================================================

static LStrHandle CreateLStr(const char *str, int len)
{
    LStr *flat = (LStr*)CoTaskMemAlloc(sizeof(int32_t) + len);
    flat->cnt = len;
    if (len > 0)
        memcpy(flat->str, str, len);

    LStrHandle h = (LStrHandle)CoTaskMemAlloc(sizeof(LStr*));
    *h = flat;
    return h;
}

static char *ReadLStr(LStrHandle h, uint16_t *outSize)
{
    if (!h || !*h)
    {
        if (outSize) *outSize = 0;
        return nullptr;
    }
    LStr *lstr = *h;
    int cnt = lstr->cnt;
    char *buf = (char*)CoTaskMemAlloc(cnt + 1);
    memcpy(buf, lstr->str, cnt);
    buf[cnt] = '\0';
    if (outSize) *outSize = (uint16_t)(cnt + 1);
    return buf;
}

static char **ReadTD1Strings(TD1Hdl h, uint32_t *outCount)
{
    if (!h || !*h) { if (outCount) *outCount = 0; return nullptr; }
    TD1 *td1 = *h;
    int32_t count = td1->dimSize;
    if (count <= 0) { if (outCount) *outCount = 0; return nullptr; }

    char **arr = (char**)CoTaskMemAlloc(count * sizeof(char*));
    for (int32_t i = 0; i < count; i++)
    {
        LStrHandle lh = td1->String[i];
        if (lh && *lh)
        {
            int cnt = (*lh)->cnt;
            arr[i] = (char*)CoTaskMemAlloc(cnt + 1);
            memcpy(arr[i], (*lh)->str, cnt);
            arr[i][cnt] = '\0';
        }
        else
        {
            arr[i] = (char*)CoTaskMemAlloc(1);
            arr[i][0] = '\0';
        }
    }
    if (outCount) *outCount = (uint32_t)count;
    return arr;
}

static char **ReadTD6Strings(TD6Hdl h, uint32_t *outCount)
{
    if (!h || !*h) { if (outCount) *outCount = 0; return nullptr; }
    return ReadTD1Strings((TD1Hdl)h, outCount);
}

static TD2Hdl CreateTD2(const uint16_t *data, int count)
{
    if (count <= 0 || !data) return nullptr;
    int size = sizeof(int32_t) + count * sizeof(uint16_t);
    TD2 *td2 = (TD2*)CoTaskMemAlloc(size);
    td2->dimSize = count;
    memcpy(td2->Numeric, data, count * sizeof(uint16_t));

    TD2Hdl h = (TD2Hdl)CoTaskMemAlloc(sizeof(TD2*));
    *h = td2;
    return h;
}

static int CopyTD2ToBuf(TD2Hdl h, uint16_t *buf, int bufMax)
{
    if (!h || !*h || !buf || bufMax <= 0) return 0;
    TD2 *td2 = *h;
    int n = td2->dimSize;
    if (n > bufMax) n = bufMax;
    memcpy(buf, td2->Numeric, n * sizeof(uint16_t));
    return n;
}

static int CopyTD5ToBuf(TD5Hdl h, uint16_t *buf, int bufMax)
    { return CopyTD2ToBuf((TD2Hdl)h, buf, bufMax); }

static TD3Hdl CreateTD3(const double *data, int count)
{
    if (count <= 0 || !data) return nullptr;
    int size = sizeof(int32_t) + count * sizeof(double);
    TD3 *td3 = (TD3*)CoTaskMemAlloc(size);
    td3->dimSize = count;
    memcpy(td3->Numeric, data, count * sizeof(double));

    TD3Hdl h = (TD3Hdl)CoTaskMemAlloc(sizeof(TD3*));
    *h = td3;
    return h;
}

// ============================================================================
// 释放（仅我们自己的 CoTaskMemAlloc）
// ============================================================================
static void FreeLStrHandle(LStrHandle h)
{
    if (!h) return;
    if (*h) CoTaskMemFree(*h);
    CoTaskMemFree(h);
}
static void FreeTD2(TD2Hdl h)
{
    if (!h) return;
    if (*h) CoTaskMemFree(*h);
    CoTaskMemFree(h);
}
static void FreeTD3(TD3Hdl h)
{
    if (!h) return;
    if (*h) CoTaskMemFree(*h);
    CoTaskMemFree(h);
}

// ============================================================================
// IspInterfaceInitialEx_c
// ============================================================================
void IspInterfaceInitialEx_c(const char *productCfgFile,
    char ***appNames, uint32_t *appNameCount,
    char ***deviceVisa, uint32_t *deviceVisaCount,
    char **errorInfo, uint16_t *errorSize)
{
    *appNames = nullptr;    *appNameCount = 0;
    *deviceVisa = nullptr;  *deviceVisaCount = 0;
    *errorInfo = nullptr;   *errorSize = 0;

    LStrHandle cfgLStr = CreateLStr(productCfgFile, (int)strlen(productCfgFile));
    TD1Hdl tdAppNames = nullptr;
    TD6Hdl tdDeviceVisa = nullptr;
    LStrHandle errLStr = nullptr;
    printf("%s", productCfgFile);
    InterfaceInitialEx_C(&cfgLStr, &tdAppNames, &tdDeviceVisa, &errLStr);
    
    if (errLStr && *errLStr && (*errLStr)->cnt > 0)
        *errorInfo = ReadLStr(errLStr, errorSize);

    if (tdAppNames && *tdAppNames)
        *appNames = ReadTD1Strings(tdAppNames, appNameCount);

    if (tdDeviceVisa && *tdDeviceVisa)
        *deviceVisa = ReadTD6Strings(tdDeviceVisa, deviceVisaCount);

    FreeLStrHandle(cfgLStr);
}

// ============================================================================
// IspDutReadWriteEx
// ============================================================================
void IspDutReadWriteEx(uint32_t devIndex,
    uint8_t dutSlot, uint8_t dutChannel, const char *appName,
    uint8_t operation,
    uint16_t *dataIn, uint16_t dataInCount,
    uint16_t *dataOut, uint16_t *dataOutCount,
    char **errorInfo, uint16_t *errorSize)
{
    *errorInfo = nullptr; *errorSize = 0;

    LStrHandle appLStr = CreateLStr(appName, (int)strlen(appName));
    TD2Hdl tdDataIn = (dataIn && dataInCount > 0)
        ? CreateTD2(dataIn, dataInCount) : nullptr;
    TD2Hdl tdDataOut = nullptr;
    LStrHandle errLStr = nullptr;

    DutReadWriteEx(devIndex, dutSlot, dutChannel,
        &appLStr, operation, &tdDataIn, &tdDataOut, &errLStr);

    if (errLStr && *errLStr && (*errLStr)->cnt > 0)
        *errorInfo = ReadLStr(errLStr, errorSize);

    if (dataOut && dataOutCount)
        *dataOutCount = (uint16_t)CopyTD2ToBuf(tdDataOut, dataOut, *dataOutCount);

    FreeTD2(tdDataIn);
    FreeLStrHandle(appLStr);
}

// ============================================================================
// IspFormularCalc
// ============================================================================
void IspFormularCalc(const char *appName,
    double *dataIn, uint16_t dataInCount,
    double *result,
    char **errorInfo, uint16_t *errorSize)
{
    *errorInfo = nullptr; *errorSize = 0;
    if (result) *result = 0.0;

    LStrHandle appLStr = CreateLStr(appName, (int)strlen(appName));
    TD3Hdl tdDataIn = (dataIn && dataInCount > 0)
        ? CreateTD3(dataIn, dataInCount) : nullptr;
    double r = 0.0;
    LStrHandle errLStr = nullptr;

    FormularCalc(&appLStr, &tdDataIn, &r, &errLStr);

    if (errLStr && *errLStr && (*errLStr)->cnt > 0)
        *errorInfo = ReadLStr(errLStr, errorSize);
    else if (result)
        *result = r;

    // 不释放 tdDataIn 和 appLStr —— LabVIEW FormularCalc 内部接管了这些 handle，
    // 再用 CoTaskMemFree 释放会导致 double-free 堆损坏，第二次调用必崩。
}

// ============================================================================
// IspDutHeaterScanEx
// ============================================================================
void IspDutHeaterScanEx(uint32_t devIndex,
    uint8_t dutSlot, uint8_t dutChannel, const char *appName,
    uint16_t *dataIn, uint16_t dataInCount,
    uint16_t *mpdOutAdc, uint16_t *mpdOutAdcCount,
    uint16_t *mpdInAdc, uint16_t *mpdInAdcCount,
    char **errorInfo, uint16_t *errorSize)
{
    *errorInfo = nullptr; *errorSize = 0;

    LStrHandle appLStr = CreateLStr(appName, (int)strlen(appName));
    TD2Hdl tdDataIn = (dataIn && dataInCount > 0)
        ? CreateTD2(dataIn, dataInCount) : nullptr;
    TD5Hdl tdMpdOut = nullptr;
    TD2Hdl tdMpdIn = nullptr;
    LStrHandle errLStr = nullptr;

    DutHeaterScanEx(devIndex, dutSlot, dutChannel,
        &appLStr, &tdDataIn, &tdMpdOut, &tdMpdIn, &errLStr);

    if (errLStr && *errLStr && (*errLStr)->cnt > 0)
        *errorInfo = ReadLStr(errLStr, errorSize);

    if (mpdOutAdc && mpdOutAdcCount)
        *mpdOutAdcCount = (uint16_t)CopyTD5ToBuf(tdMpdOut, mpdOutAdc, *mpdOutAdcCount);
    if (mpdInAdc && mpdInAdcCount)
        *mpdInAdcCount = (uint16_t)CopyTD2ToBuf(tdMpdIn, mpdInAdc, *mpdInAdcCount);

    FreeTD2(tdDataIn);
    FreeLStrHandle(appLStr);
}
