// Original MIT PCM conversion used by the WASAPI bridge.
#pragma once
#include <windows.h>
#include <mmreg.h>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <algorithm>
#include <stdexcept>
namespace slop_pcm {
inline constexpr GUID FloatSubtype{3,0,0x0010,{0x80,0x00,0x00,0xaa,0x00,0x38,0x9b,0x71}};
inline constexpr GUID PcmSubtype{1,0,0x0010,{0x80,0x00,0x00,0xaa,0x00,0x38,0x9b,0x71}};
inline bool floating(const WAVEFORMATEX* f){return f->wFormatTag==WAVE_FORMAT_IEEE_FLOAT||(f->wFormatTag==WAVE_FORMAT_EXTENSIBLE&&f->cbSize>=22&&IsEqualGUID(((const WAVEFORMATEXTENSIBLE*)f)->SubFormat,FloatSubtype));}
inline void validate(const WAVEFORMATEX* f){bool fp=floating(f);if(!fp&&f->wFormatTag!=WAVE_FORMAT_PCM&&!(f->wFormatTag==WAVE_FORMAT_EXTENSIBLE&&f->cbSize>=22&&IsEqualGUID(((const WAVEFORMATEXTENSIBLE*)f)->SubFormat,PcmSubtype)))throw std::runtime_error("WASAPI endpoint format is neither PCM nor IEEE float.");if(f->nChannels<1||f->nChannels>32||f->nBlockAlign!=f->nChannels*f->wBitsPerSample/8||(fp?f->wBitsPerSample!=32&&f->wBitsPerSample!=64:f->wBitsPerSample!=8&&f->wBitsPerSample!=16&&f->wBitsPerSample!=24&&f->wBitsPerSample!=32))throw std::runtime_error("Unsupported WASAPI sample format.");}
inline void decode(const BYTE* source,float* dest,int samples,const WAVEFORMATEX* f){int bytes=f->wBitsPerSample/8;bool fp=floating(f);for(int i=0;i<samples;i++,source+=bytes){double v=0;if(fp){if(bytes==4){float sample;std::memcpy(&sample,source,4);v=sample;}else std::memcpy(&v,source,8);}else if(bytes==1)v=(source[0]-128)/128.0;else if(bytes==2){int16_t n;std::memcpy(&n,source,2);v=n/32768.0;}else if(bytes==3){int32_t n=source[0]|source[1]<<8|source[2]<<16;if(n&0x800000)n|=(int32_t)0xff000000;v=n/8388608.0;}else{int32_t n;std::memcpy(&n,source,4);v=n/2147483648.0;}dest[i]=std::isfinite(v)?(float)v:0;}}
inline void encode(const float* source,BYTE* dest,int samples,const WAVEFORMATEX* f){int bytes=f->wBitsPerSample/8;bool fp=floating(f);for(int i=0;i<samples;i++,dest+=bytes){double v=std::isfinite(source[i])?source[i]:0;if(fp){if(bytes==4){float sample=(float)v;std::memcpy(dest,&sample,4);}else std::memcpy(dest,&v,8);}else{v=std::clamp(v,-1.0,1.0);if(bytes==1)dest[0]=(BYTE)std::clamp((int)std::lround(v*127)+128,0,255);else{int64_t scale=bytes==2?32767:bytes==3?8388607:2147483647;int32_t n=(int32_t)std::llround(v*scale);for(int k=0;k<bytes;k++)dest[k]=(BYTE)((uint32_t)n>>(8*k));}}}}
}
