
#pragma once
#include <stdint.h>

#define OFF_LiveKeyArchive_WriteAllCategories 0x1101340
#define OFF_g_keyArchive                     0x9D22E90
#define KA_STRIDE                            9280

typedef void (*LiveKeyArchive_WriteAll_t)(int controller, bool immediate);
