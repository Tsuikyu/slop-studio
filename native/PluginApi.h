#pragma once
#include <stdint.h>
#define SXP_API extern "C" __declspec(dllexport)
struct SxpPluginInfo { char id[256]; char name[256]; char vendor[128]; int32_t format; int32_t instrument; };
struct SxpParameter { uint32_t id; char name[128]; double value; double minimum; double maximum; int32_t steps; };
struct SxpMidiEvent { int32_t offset; int32_t status; int32_t data1; int32_t data2; };
SXP_API int sxp_scan(const wchar_t*,SxpPluginInfo*,int,char*,int);
SXP_API void* sxp_create(const wchar_t*,const char*,double,int,char*,int);
SXP_API int sxp_process(void*,float*,int,const SxpMidiEvent*,int,double,double,int,int,int);
SXP_API int sxp_param_count(void*);
SXP_API int sxp_param_info(void*,int,SxpParameter*);
SXP_API int sxp_param_set(void*,uint32_t,double);
SXP_API double sxp_param_get(void*,uint32_t);
SXP_API int sxp_editor_open(void*,void*,int*,int*);
SXP_API void sxp_editor_close(void*);
SXP_API void sxp_idle(void*);
SXP_API int sxp_state_save(void*,uint8_t*,int);
SXP_API int sxp_state_load(void*,const uint8_t*,int);
SXP_API void sxp_destroy(void*);

SXP_API int sxp_latency(void*);

struct SxpParamEvent{int32_t offset;uint32_t id;double value;};
SXP_API int sxp_param_enqueue(void*,uint32_t,double,int);
