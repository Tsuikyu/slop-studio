// MIT. Windows system WASAPI only; no GPL dependency.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <functiondiscoverykeys_devpkey.h>
#include <propvarutil.h>
#include <vector>
#include <thread>
#include <mutex>
#include <condition_variable>
#include <atomic>
#include <string>
#include <algorithm>
#include <cmath>
#include <cstdio>
#include "AudioPcm.h"
using Callback=void(__cdecl*)(float*,int,int,long long);
template<class T>void release(T*& p){if(p){p->Release();p=nullptr;}}
static void check(HRESULT hr,const char* task){if(FAILED(hr)){char b[160];std::snprintf(b,sizeof b,"%s (WASAPI HRESULT 0x%08lX)",task,(unsigned long)hr);throw std::runtime_error(b);}}
static IMMDevice* device(IMMDeviceEnumerator* e,const wchar_t* id,bool capture){IMMDevice* d=nullptr;if(id&&*id)check(e->GetDevice(id,&d),"Open audio endpoint");else check(e->GetDefaultAudioEndpoint(capture?eCapture:eRender,eConsole,&d),"Find default audio endpoint");return d;}
extern "C" __declspec(dllexport) int sxa_device(int capture,int index,wchar_t* id,int idcap,wchar_t* name,int namecap){
 HRESULT co=CoInitializeEx(nullptr,COINIT_MULTITHREADED);IMMDeviceEnumerator* e=nullptr;IMMDeviceCollection* c=nullptr;IMMDevice* d=nullptr;IPropertyStore* p=nullptr;int result=0;
 try{check(CoCreateInstance(__uuidof(MMDeviceEnumerator),nullptr,CLSCTX_ALL,IID_PPV_ARGS(&e)),"Audio enumerator");check(e->EnumAudioEndpoints(capture?eCapture:eRender,DEVICE_STATE_ACTIVE,&c),"Enumerate endpoints");UINT n=0;c->GetCount(&n);if(index<(int)n&&index>=0){check(c->Item(index,&d),"Endpoint");LPWSTR s=nullptr;d->GetId(&s);wcsncpy_s(id,idcap,s,_TRUNCATE);CoTaskMemFree(s);check(d->OpenPropertyStore(STGM_READ,&p),"Endpoint properties");PROPVARIANT v;PropVariantInit(&v);p->GetValue(PKEY_Device_FriendlyName,&v);wcsncpy_s(name,namecap,v.vt==VT_LPWSTR?v.pwszVal:L"Audio endpoint",_TRUNCATE);PropVariantClear(&v);result=1;}}catch(...){result=-1;}release(p);release(d);release(c);release(e);if(SUCCEEDED(co))CoUninitialize();return result;
}
struct AudioStream{
 std::thread thread;std::atomic<bool> stop{false};std::mutex mutex;std::condition_variable ready;bool initialized=false;std::string error;std::atomic<long long> frames{0},played{0};int channels=2,rate=48000,block=512,capacityFrames=0,formatBits=32;bool floatFormat=true;bool capture=false,exclusive=false;std::wstring id;Callback callback;std::atomic<int> failure{0};
 void run(){
 CoInitializeEx(nullptr,COINIT_MULTITHREADED);IMMDeviceEnumerator* e=nullptr;IMMDevice* d=nullptr;IAudioClient* client=nullptr;IAudioRenderClient* render=nullptr;IAudioCaptureClient* input=nullptr;HANDLE event=CreateEventW(nullptr,FALSE,FALSE,nullptr);
 try{
  check(CoCreateInstance(__uuidof(MMDeviceEnumerator),nullptr,CLSCTX_ALL,IID_PPV_ARGS(&e)),"Audio enumerator");d=device(e,id.c_str(),capture);check(d->Activate(__uuidof(IAudioClient),CLSCTX_ALL,nullptr,(void**)&client),"Activate audio client");
  WAVEFORMATEX* mix=nullptr;check(client->GetMixFormat(&mix),"Read device format");channels=capture?std::clamp((int)mix->nChannels,1,32):2;std::vector<BYTE> mixBytes(sizeof(WAVEFORMATEX)+mix->cbSize);std::memcpy(mixBytes.data(),mix,mixBytes.size());CoTaskMemFree(mix);
  WAVEFORMATEX simple{};simple.wFormatTag=WAVE_FORMAT_IEEE_FLOAT;simple.nChannels=channels;simple.nSamplesPerSec=rate;simple.wBitsPerSample=32;simple.nBlockAlign=channels*4;simple.nAvgBytesPerSec=rate*simple.nBlockAlign;
  std::vector<BYTE> selected(sizeof(simple));std::memcpy(selected.data(),&simple,sizeof(simple));
  if(exclusive&&client->IsFormatSupported(AUDCLNT_SHAREMODE_EXCLUSIVE,(WAVEFORMATEX*)selected.data(),nullptr)!=S_OK){bool supported=false;HRESULT lastSupport=AUDCLNT_E_UNSUPPORTED_FORMAT;auto* preferred=(WAVEFORMATEX*)mixBytes.data();preferred->nSamplesPerSec=rate;preferred->nChannels=channels;preferred->nBlockAlign=channels*preferred->wBitsPerSample/8;preferred->nAvgBytesPerSec=rate*preferred->nBlockAlign;if(preferred->wFormatTag==WAVE_FORMAT_EXTENSIBLE&&preferred->cbSize>=22)((WAVEFORMATEXTENSIBLE*)preferred)->dwChannelMask=channels==2?3:channels==1?4:0;
   try{slop_pcm::validate(preferred);if(client->IsFormatSupported(AUDCLNT_SHAREMODE_EXCLUSIVE,preferred,nullptr)==S_OK){selected=mixBytes;supported=true;}}catch(...){}
   if(!supported)for(int bits:{32,24,16}){WAVEFORMATEXTENSIBLE pcm{};pcm.Format.wFormatTag=WAVE_FORMAT_EXTENSIBLE;pcm.Format.nChannels=channels;pcm.Format.nSamplesPerSec=rate;pcm.Format.wBitsPerSample=bits;pcm.Format.nBlockAlign=channels*bits/8;pcm.Format.nAvgBytesPerSec=rate*pcm.Format.nBlockAlign;pcm.Format.cbSize=22;pcm.Samples.wValidBitsPerSample=bits;pcm.dwChannelMask=channels==2?3:channels==1?4:0;pcm.SubFormat=slop_pcm::PcmSubtype;lastSupport=client->IsFormatSupported(AUDCLNT_SHAREMODE_EXCLUSIVE,&pcm.Format,nullptr);if(lastSupport==S_OK){selected.resize(sizeof(pcm));std::memcpy(selected.data(),&pcm,sizeof(pcm));supported=true;break;}WAVEFORMATEX plain=pcm.Format;plain.wFormatTag=WAVE_FORMAT_PCM;plain.cbSize=0;lastSupport=client->IsFormatSupported(AUDCLNT_SHAREMODE_EXCLUSIVE,&plain,nullptr);if(lastSupport==S_OK){selected.resize(sizeof(plain));std::memcpy(selected.data(),&plain,sizeof(plain));supported=true;break;}}
   if(!supported){char reason[224];std::snprintf(reason,sizeof reason,"Exclusive WASAPI format negotiation rejected by the selected endpoint; choose WASAPI shared or another rate (WASAPI HRESULT 0x%08lX)",(unsigned long)lastSupport);throw std::runtime_error(reason);}
  }
  auto* format=(WAVEFORMATEX*)selected.data();slop_pcm::validate(format);formatBits=format->wBitsPerSample;floatFormat=slop_pcm::floating(format);
  DWORD flags=AUDCLNT_STREAMFLAGS_EVENTCALLBACK;if(!exclusive)flags|=AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM|AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY;
  REFERENCE_TIME duration=(REFERENCE_TIME)std::llround((double)block*10000000/rate);if(exclusive){REFERENCE_TIME normal=0,minimum=0;check(client->GetDevicePeriod(&normal,&minimum),"Read device period");duration=std::max(duration,minimum);}
  HRESULT hr=client->Initialize(exclusive?AUDCLNT_SHAREMODE_EXCLUSIVE:AUDCLNT_SHAREMODE_SHARED,flags,exclusive?duration:0,exclusive?duration:0,format,nullptr);
  if(exclusive&&hr==AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED){UINT aligned=0;check(client->GetBufferSize(&aligned),"Read aligned buffer size");duration=(REFERENCE_TIME)std::llround((double)aligned*10000000/rate);release(client);check(d->Activate(__uuidof(IAudioClient),CLSCTX_ALL,nullptr,(void**)&client),"Reactivate aligned audio client");hr=client->Initialize(AUDCLNT_SHAREMODE_EXCLUSIVE,flags,duration,duration,format,nullptr);}
  check(hr,"Initialize selected sample rate / mode");check(client->SetEventHandle(event),"Set audio event");UINT capacity=0;check(client->GetBufferSize(&capacity),"Read device buffer size");capacityFrames=(int)capacity;
  if(capture)check(client->GetService(IID_PPV_ARGS(&input)),"Capture service");else check(client->GetService(IID_PPV_ARGS(&render)),"Render service");
  std::vector<float> samples(std::max((int)capacity,block)*channels);
  check(client->Start(),"Start audio endpoint");{std::lock_guard<std::mutex> lock(mutex);initialized=true;ready.notify_one();}
  while(!stop){
   if(WaitForSingleObject(event,100)!=WAIT_OBJECT_0)continue;
   if(capture){UINT pending=0;check(input->GetNextPacketSize(&pending),"Capture packet");while(pending){BYTE* bytes=nullptr;UINT n=0;DWORD f=0;check(input->GetBuffer(&bytes,&n,&f,nullptr,nullptr),"Capture buffer");if(samples.size()<n*channels)samples.resize(n*channels);if(f&AUDCLNT_BUFFERFLAGS_SILENT)std::fill(samples.begin(),samples.begin()+n*channels,0);else slop_pcm::decode(bytes,samples.data(),n*channels,format);input->ReleaseBuffer(n);callback(samples.data(),n,channels,frames);frames+=n;played=frames.load();check(input->GetNextPacketSize(&pending),"Capture next packet");}}
   else{UINT padding=0;if(!exclusive)check(client->GetCurrentPadding(&padding),"Render padding");played=std::max(0LL,frames.load()-(long long)padding);UINT available=capacity-padding;while(available){UINT n=exclusive?capacity:std::min(available,(UINT)block);std::fill(samples.begin(),samples.begin()+n*channels,0);for(UINT at=0;at<n;at+=block){UINT part=std::min((UINT)block,n-at);callback(samples.data()+at*channels,part,channels,frames+at);}BYTE* bytes=nullptr;check(render->GetBuffer(n,&bytes),"Render buffer");slop_pcm::encode(samples.data(),bytes,n*channels,format);check(render->ReleaseBuffer(n,0),"Submit audio");frames+=n;available-=n;}}
  }
 }catch(const std::exception& ex){error=ex.what();failure=1;std::lock_guard<std::mutex> lock(mutex);initialized=true;ready.notify_one();}
 if(client)client->Stop();release(input);release(render);release(client);release(d);release(e);CloseHandle(event);CoUninitialize();
 }
 ~AudioStream(){stop=true;if(thread.joinable())thread.join();}
};
extern "C" __declspec(dllexport) void* sxa_open(const wchar_t* id,int capture,int exclusive,int rate,int block,Callback callback,char* error,int cap){
 AudioStream* s=new AudioStream();s->id=id?id:L"";s->capture=capture!=0;s->exclusive=exclusive!=0;s->rate=rate;s->block=block;s->callback=callback;s->thread=std::thread([s]{s->run();});
 {std::unique_lock<std::mutex> lock(s->mutex);s->ready.wait_for(lock,std::chrono::seconds(8),[s]{return s->initialized;});}
 if(!s->initialized||s->failure){strncpy_s(error,cap,s->error.empty()?"Audio endpoint startup timed out.":s->error.c_str(),_TRUNCATE);delete s;return nullptr;}return s;
}
extern "C" __declspec(dllexport) long long sxa_frames(void* h){return h?((AudioStream*)h)->played.load():0;}
extern "C" __declspec(dllexport) int sxa_channels(void* h){return h?((AudioStream*)h)->channels:0;}
extern "C" __declspec(dllexport) int sxa_status(void* h,char* error,int cap){auto s=(AudioStream*)h;if(!s)return -1;if(s->failure){strncpy_s(error,cap,s->error.c_str(),_TRUNCATE);return -1;}return 0;}
extern "C" __declspec(dllexport) void sxa_close(void* h){delete (AudioStream*)h;}

extern "C" __declspec(dllexport) int sxa_format(void* h){auto s=(AudioStream*)h;return s?s->formatBits|(s->floatFormat?65536:0):0;}
extern "C" __declspec(dllexport) int sxa_bufferframes(void* h){return h?((AudioStream*)h)->capacityFrames:0;}
