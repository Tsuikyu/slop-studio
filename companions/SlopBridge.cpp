// SPDX-License-Identifier: GPL-3.0-only
// Independent optional executable. Its SDKs are never linked into the MIT application.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <objbase.h>
#include <iasiodrv.h>
#include <ableton/Link.hpp>
#include <vector>
#include <string>
#include <stdexcept>
#include <algorithm>
#include <cmath>
#include <iostream>
#include <thread>
#include <cstring>
static constexpr int InOffset=4096,OutOffset=InOffset+8192*32*4;
static char* memory=nullptr;static HANDLE request=nullptr,response=nullptr,ready=nullptr;static IASIO* driver=nullptr;static int block=512,inputs=0,outputs=0;static std::vector<ASIOBufferInfo> buffers;static std::vector<ASIOChannelInfo> infos;
template<class T>T& field(int offset){return *reinterpret_cast<T*>(memory+offset);}
static void check(ASIOError e,const char* text){if(e!=ASE_OK&&e!=ASE_SUCCESS)throw std::runtime_error(std::string(text)+" (ASIO "+std::to_string(e)+")");}
static int sampleBytes(int t){if(t==ASIOSTInt16LSB)return 2;if(t==ASIOSTInt24LSB)return 3;if(t==ASIOSTFloat64LSB)return 8;if(t==ASIOSTInt32LSB||t==ASIOSTFloat32LSB||(t>=24&&t<=27))return 4;throw std::runtime_error("Unsupported ASIO channel sample type (only little endian PCM/float).");}
static float readSample(const char* p,int type){switch(type){case ASIOSTInt16LSB:return *(const short*)p/32768.f;case ASIOSTInt24LSB:{int v=(unsigned char)p[0]|(unsigned char)p[1]<<8|(unsigned char)p[2]<<16;if(v&0x800000)v|=0xff000000;return v/8388608.f;}case ASIOSTFloat32LSB:return *(const float*)p;case ASIOSTFloat64LSB:return *(const double*)p;default:{int bits=type>=24?16+(type-24)*2:32;if(type==27)bits=24;return (float)(*(const int*)p/std::pow(2.,bits-1));}}}
static void writeSample(char* p,int type,float value){double v=std::clamp((double)value,-1.,0.9999999);switch(type){case ASIOSTInt16LSB:*(short*)p=(short)(v*32767);break;case ASIOSTInt24LSB:{int n=(int)(v*8388607);p[0]=n;p[1]=n>>8;p[2]=n>>16;break;}case ASIOSTFloat32LSB:*(float*)p=value;break;case ASIOSTFloat64LSB:*(double*)p=value;break;default:{int bits=type>=24?16+(type-24)*2:32;if(type==27)bits=24;*(int*)p=(int)(v*(std::pow(2.,bits-1)-1));}}}
static void switched(long index,ASIOBool){
 float* in=(float*)(memory+InOffset);float* out=(float*)(memory+OutOffset);
 for(int c=0;c<inputs;c++){auto p=(char*)buffers[c].buffers[index];int bytes=sampleBytes(infos[c].type);for(int f=0;f<block;f++)in[f*inputs+c]=readSample(p+f*bytes,infos[c].type);}
 int sequence=InterlockedIncrement((LONG*)(memory+88));SetEvent(request);bool ok=false;ULONGLONG deadline=GetTickCount64()+80;
 while(true){ULONGLONG now=GetTickCount64();if(now>=deadline)break;if(WaitForSingleObject(response,(DWORD)(deadline-now))!=WAIT_OBJECT_0)break;MemoryBarrier();if(field<int>(92)==sequence){ok=true;break;}}
 if(!ok)InterlockedIncrement((LONG*)(memory+48));
 for(int c=0;c<outputs;c++){int n=inputs+c;auto p=(char*)buffers[n].buffers[index];int bytes=sampleBytes(infos[n].type);for(int f=0;f<block;f++)writeSample(p+f*bytes,infos[n].type,ok?out[f*2+c]:0);}
 InterlockedExchangeAdd64((LONG64*)(memory+32),block);driver->outputReady();
}
static ASIOTime* switchedTime(ASIOTime* t,long index,ASIOBool direct){switched(index,direct);return t;}
static void rateChanged(ASIOSampleRate rate){field<double>(56)=rate;if(field<int>(16)>0&&std::abs(rate-field<int>(16))>0.5)InterlockedExchange((LONG*)(memory+52),2);}
static long message(long selector,long value,void*,double*){if(selector==kAsioSelectorSupported)return value==kAsioEngineVersion||value==kAsioSupportsTimeInfo||value==kAsioResetRequest||value==kAsioResyncRequest||value==kAsioLatenciesChanged;if(selector==kAsioEngineVersion)return 2;if(selector==kAsioSupportsTimeInfo)return 1;if(selector==kAsioResetRequest){InterlockedExchange((LONG*)(memory+52),1);SetEvent(request);return 1;}if(selector==kAsioResyncRequest){InterlockedIncrement((LONG*)(memory+96));return 1;}if(selector==kAsioLatenciesChanged){InterlockedExchange((LONG*)(memory+100),1);return 1;}return 0;}
static void asioPanel(const wchar_t* clsid){
 CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);CLSID id;HRESULT h=CLSIDFromString(clsid,&id);if(FAILED(h))throw std::runtime_error("Invalid registered ASIO CLSID.");
 h=CoCreateInstance(id,nullptr,CLSCTX_INPROC_SERVER,id,(void**)&driver);if(FAILED(h)||!driver)throw std::runtime_error("Cannot load the selected 64-bit ASIO driver.");
 HWND owner=CreateWindowExW(0,L"STATIC",L"Slop Studio ASIO",WS_OVERLAPPEDWINDOW,0,0,320,180,nullptr,nullptr,GetModuleHandleW(nullptr),nullptr);
 if(!driver->init(owner)){char error[256]{};driver->getErrorMessage(error);throw std::runtime_error(std::string("ASIO driver initialization failed: ")+error);}
 field<int>(0)=1;SetEvent(ready);check(driver->controlPanel(),"Open ASIO control panel");driver->Release();driver=nullptr;DestroyWindow(owner);CoUninitialize();
}
static void runAsio(const wchar_t* clsid,int rate,int frames){
 CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);CLSID id;HRESULT h=CLSIDFromString(clsid,&id);if(FAILED(h))throw std::runtime_error("Invalid registered ASIO CLSID.");h=CoCreateInstance(id,nullptr,CLSCTX_INPROC_SERVER,id,(void**)&driver);if(FAILED(h)||!driver)throw std::runtime_error("Cannot load the selected 64-bit ASIO driver.");
 HWND owner=CreateWindowExW(0,L"STATIC",L"Slop ASIO Companion",WS_OVERLAPPEDWINDOW,0,0,320,180,nullptr,nullptr,GetModuleHandleW(nullptr),nullptr);if(!driver->init(owner)){char error[256]{};driver->getErrorMessage(error);throw std::runtime_error(std::string("ASIO driver initialization failed: ")+error);}
 field<int>(16)=rate;check(driver->canSampleRate(rate),"Requested sample rate is unsupported");check(driver->setSampleRate(rate),"Set sample rate");long in,out;check(driver->getChannels(&in,&out),"Get channels");inputs=std::min(32L,in);outputs=std::min(2L,out);if(outputs<2)throw std::runtime_error("This output route requires two ASIO output channels.");
 long min,max,preferred,granularity;check(driver->getBufferSize(&min,&max,&preferred,&granularity),"Get buffer range");if(frames<min||frames>max||(granularity==-1&&(frames&(frames-1)))||(granularity>0&&(frames-min)%granularity))throw std::runtime_error("Requested buffer size unsupported; preferred="+std::to_string(preferred));block=frames;
 buffers.resize(inputs+outputs);infos.resize(inputs+outputs);for(int c=0;c<inputs+outputs;c++){buffers[c].isInput=c<inputs?ASIOTrue:ASIOFalse;buffers[c].channelNum=c<inputs?c:c-inputs;infos[c].channel=buffers[c].channelNum;infos[c].isInput=buffers[c].isInput;check(driver->getChannelInfo(&infos[c]),"Get channel format");sampleBytes(infos[c].type);}
 ASIOCallbacks callbacks{switched,rateChanged,message,switchedTime};check(driver->createBuffers(buffers.data(),buffers.size(),block,&callbacks),"Create ASIO buffers");long il,ol;driver->getLatencies(&il,&ol);
 field<int>(4)=inputs;field<int>(8)=outputs;field<int>(12)=block;field<int>(16)=rate;field<int>(20)=il;field<int>(24)=ol;field<int>(0)=1;SetEvent(ready);
 check(driver->start(),"Start ASIO");while(!field<int>(28)){MSG m;while(PeekMessage(&m,nullptr,0,0,PM_REMOVE)){TranslateMessage(&m);DispatchMessage(&m);}if(field<int>(44)){field<int>(44)=0;driver->controlPanel();}if(InterlockedExchange((LONG*)(memory+100),0)){long il=0,ol=0;if(driver->getLatencies(&il,&ol)==ASE_OK){field<int>(20)=il;field<int>(24)=ol;}}Sleep(3);}
 driver->stop();driver->disposeBuffers();driver->Release();driver=nullptr;CoUninitialize();
}
static void link(){
 ableton::Link link(120.);link.enableStartStopSync(true);link.enable(true);field<int>(0)=1;SetEvent(ready);
 while(!field<int>(28)){auto state=link.captureAppSessionState();auto now=link.clock().micros();if(field<int>(44)){field<int>(44)=0;double tempo=field<double>(64);if(tempo>=20&&tempo<=400)state.setTempo(tempo,now);state.setIsPlaying(field<int>(72)!=0,now);link.commitAppSessionState(state);}
 state=link.captureAppSessionState();field<double>(8)=state.tempo();field<double>(16)=state.beatAtTime(now,4.);field<int>(4)=(int)link.numPeers();field<int>(40)=state.isPlaying();Sleep(10);}
 link.enable(false);
}
int wmain(int argc,wchar_t** argv){
 if(argc<3)return 2;std::wstring key=argv[2];HANDLE map=OpenFileMappingW(FILE_MAP_ALL_ACCESS,FALSE,key.c_str());if(!map)return 3;memory=(char*)MapViewOfFile(map,FILE_MAP_ALL_ACCESS,0,0,0);if(!memory)return 4;HANDLE parent=OpenProcess(SYNCHRONIZE,FALSE,field<int>(80));if(parent)std::thread([parent]{WaitForSingleObject(parent,INFINITE);CloseHandle(parent);TerminateProcess(GetCurrentProcess(),75);}).detach();request=OpenEventW(EVENT_ALL_ACCESS,FALSE,(key+L"-request").c_str());response=OpenEventW(EVENT_ALL_ACCESS,FALSE,(key+L"-response").c_str());ready=OpenEventW(EVENT_ALL_ACCESS,FALSE,(key+L"-ready").c_str());
 try{if(std::wstring(argv[1])==L"--asio"&&argc==6)runAsio(argv[3],_wtoi(argv[4]),_wtoi(argv[5]));else if(std::wstring(argv[1])==L"--asio-panel"&&argc==4)asioPanel(argv[3]);else if(std::wstring(argv[1])==L"--link")link();else throw std::runtime_error("Invalid companion arguments.");}
 catch(const std::exception& ex){strncpy_s(memory+128,1900,ex.what(),_TRUNCATE);field<int>(0)=-1;SetEvent(ready);SetEvent(request);}
 UnmapViewOfFile(memory);CloseHandle(map);CloseHandle(request);CloseHandle(response);CloseHandle(ready);return 0;
}
