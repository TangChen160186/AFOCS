#pragma once

#ifdef ISPBORAD_EXPORTS
#define ISPBORAD_API __declspec(dllexport)
#else
#define ISPBORAD_API __declspec(dllimport)
#endif

#include "RdOpticalDutInterface_LV2019_64b.h"
