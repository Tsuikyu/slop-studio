#define NOMINMAX
#include <cstring>
#include <cmath>
#include <algorithm>
#include <atomic>
#include <thread>
#include <chrono>
#include "pluginterfaces/base/ipluginbase.h"
#include "pluginterfaces/base/ibstream.h"
#include "pluginterfaces/vst/ivstcomponent.h"
#include "pluginterfaces/vst/ivstaudioprocessor.h"
#include "pluginterfaces/vst/ivsteditcontroller.h"
#include "pluginterfaces/vst/ivsthostapplication.h"
#include "pluginterfaces/vst/ivstparameterchanges.h"
#include "pluginterfaces/vst/ivstevents.h"
#include "pluginterfaces/vst/vstspeaker.h"
#include "pluginterfaces/gui/iplugview.h"
using namespace Steinberg;using namespace Steinberg::Vst;
static const TUID FixtureId=INLINE_UID(0x73B59081,0x5D8945C0,0xBA214F3E,0xF5D16A29);
static bool eq(const TUID a,const TUID b){return !std::memcmp(a,b,16);}static void name16(TChar* d,const char* s){int i=0;for(;s[i]&&i<127;i++)d[i]=s[i];d[i]=0;}
static FUnknown* factoryContext=nullptr;
#if defined(SLOP_FIXTURE_CONCURRENT_EDITOR)
static std::atomic<bool> editorAttached{false};
class FixtureView:public IPlugView{uint32 refs=1;
public:
 tresult PLUGIN_API queryInterface(const TUID id,void** p)override{*p=nullptr;if(eq(id,INLINE_UID_OF(IPlugView))||eq(id,INLINE_UID_OF(FUnknown))){*p=static_cast<IPlugView*>(this);addRef();return kResultOk;}return kNoInterface;}
 uint32 PLUGIN_API addRef()override{return ++refs;}uint32 PLUGIN_API release()override{auto n=--refs;if(!n)delete this;return n;}
 tresult PLUGIN_API isPlatformTypeSupported(FIDString type)override{return type&&!std::strcmp(type,kPlatformTypeHWND)?kResultOk:kResultFalse;}
 tresult PLUGIN_API attached(void*,FIDString)override{editorAttached=true;return kResultOk;}
 tresult PLUGIN_API removed()override{editorAttached=false;return kResultOk;}
 tresult PLUGIN_API onWheel(float)override{return kResultFalse;}tresult PLUGIN_API onKeyDown(char16,int16,int16)override{return kResultFalse;}tresult PLUGIN_API onKeyUp(char16,int16,int16)override{return kResultFalse;}
 tresult PLUGIN_API getSize(ViewRect* r)override{if(!r)return kInvalidArgument;*r=ViewRect(0,0,320,200);return kResultOk;}tresult PLUGIN_API onSize(ViewRect*)override{return kResultOk;}
 tresult PLUGIN_API onFocus(TBool)override{return kResultOk;}tresult PLUGIN_API setFrame(IPlugFrame*)override{return kResultOk;}tresult PLUGIN_API canResize()override{return kResultFalse;}tresult PLUGIN_API checkSizeConstraint(ViewRect*)override{return kResultFalse;}
};
#endif
class Fixture:public IComponent,public IAudioProcessor,public IEditController,public IMidiMapping{uint32 refs=1;double gain=0.5,rate=48000,phase=0;bool note=false;bool handlerReady=false;int stateWrites=0;
public:
 ~Fixture(){if(factoryContext){IHostApplication* app=nullptr;if(factoryContext->queryInterface(INLINE_UID_OF(IHostApplication),(void**)&app)==kResultOk&&app){String128 name{};app->getName(name);app->release();}}}
 tresult PLUGIN_API queryInterface(const TUID id,void** p)override{*p=nullptr;if(eq(id,INLINE_UID_OF(FUnknown))||eq(id,INLINE_UID_OF(IPluginBase))||eq(id,INLINE_UID_OF(IComponent)))*p=static_cast<IComponent*>(this);else if(eq(id,INLINE_UID_OF(IAudioProcessor)))*p=static_cast<IAudioProcessor*>(this);else if(eq(id,INLINE_UID_OF(IEditController)))*p=static_cast<IEditController*>(this);
#if defined(SLOP_FIXTURE_MIDI_ORDER)
 else if(eq(id,INLINE_UID_OF(IMidiMapping)))*p=static_cast<IMidiMapping*>(this);
#endif
 if(*p){addRef();return kResultOk;}return kNoInterface;}
 tresult PLUGIN_API getMidiControllerAssignment(int32 bus,int16,CtrlNumber number,ParamID& id)override{if(handlerReady&&bus==0&&number==1){id=1000;return kResultOk;}return kResultFalse;}
 uint32 PLUGIN_API addRef()override{return ++refs;}uint32 PLUGIN_API release()override{uint32 n=--refs;if(!n)delete this;return n;}
 tresult PLUGIN_API initialize(FUnknown*)override{return kResultOk;}tresult PLUGIN_API terminate()override{return kResultOk;}
 tresult PLUGIN_API getControllerClassId(TUID id)override{std::memcpy(id,FixtureId,16);return kResultOk;}tresult PLUGIN_API setIoMode(IoMode)override{return kResultOk;}
 int32 PLUGIN_API getBusCount(MediaType type,BusDirection dir)override{return type==kAudio?1:(dir==kInput?1:0);}
 tresult PLUGIN_API getBusInfo(MediaType type,BusDirection dir,int32 index,BusInfo& b)override{if(index)return kInvalidArgument;b={};b.mediaType=type;b.direction=dir;b.channelCount=type==kAudio?2:16;b.busType=kMain;b.flags=BusInfo::kDefaultActive;name16(b.name,type==kAudio?"Stereo":"Notes");return kResultOk;}
 tresult PLUGIN_API getRoutingInfo(RoutingInfo&,RoutingInfo&)override{return kNotImplemented;}tresult PLUGIN_API activateBus(MediaType,BusDirection,int32,TBool)override{return kResultOk;}tresult PLUGIN_API setActive(TBool)override{note=false;return kResultOk;}
 tresult PLUGIN_API setState(IBStream* s)override{int32 n=0;uint8_t bytes[256]{};if(s->read(bytes,sizeof(bytes),&n)!=kResultOk||n<sizeof(gain))return kResultFalse;std::memcpy(&gain,bytes,sizeof(gain));return kResultOk;}
 tresult PLUGIN_API getState(IBStream* s)override{int32 n=0;auto result=s->write(&gain,sizeof(gain),&n);
#if defined(SLOP_FIXTURE_VARIABLE_STATE)
  uint8_t padding[8]{};if(result==kResultOk)result=s->write(padding,(stateWrites++)%7,&n);
#endif
  return result;
 }
 tresult PLUGIN_API setBusArrangements(SpeakerArrangement* in,int32 ni,SpeakerArrangement* out,int32 no)override{return ni==1&&no==1&&in[0]==SpeakerArr::kStereo&&out[0]==SpeakerArr::kStereo?kResultOk:kResultFalse;}
 tresult PLUGIN_API getBusArrangement(BusDirection,int32 index,SpeakerArrangement& a)override{a=SpeakerArr::kStereo;return index==0?kResultOk:kInvalidArgument;}tresult PLUGIN_API canProcessSampleSize(int32 s)override{return s==kSample32?kResultOk:kResultFalse;}uint32 PLUGIN_API getLatencySamples()override{return 0;}tresult PLUGIN_API setupProcessing(ProcessSetup& s)override{rate=s.sampleRate;return kResultOk;}
 tresult PLUGIN_API setProcessing(TBool)override{
#if defined(SLOP_FIXTURE_NO_PROCESSING_NOTIFICATION)
  return kNotImplemented;
#elif defined(SLOP_FIXTURE_REJECT_PROCESSING)
  return kResultFalse;
#else
  return kResultOk;
#endif
 }
 uint32 PLUGIN_API getTailSamples()override{return 0;}
 tresult PLUGIN_API process(ProcessData& d)override{
#if defined(SLOP_FIXTURE_CONCURRENT_EDITOR)
  for(int i=0;i<200&&!editorAttached;i++)std::this_thread::sleep_for(std::chrono::milliseconds(10));
  if(!editorAttached)return kResultFalse;
#endif
  IParamValueQueue* automation=nullptr;int parameterIndex=0;if(d.inputParameterChanges)for(int j=0;j<d.inputParameterChanges->getParameterCount();j++){auto q=d.inputParameterChanges->getParameterData(j);if(q&&q->getParameterId()==1000)automation=q;}int next=0;Event event{};bool has=d.inputEvents&&d.inputEvents->getEventCount()>0;if(has)d.inputEvents->getEvent(0,event);for(int i=0;i<d.numSamples;i++){while(automation&&parameterIndex<automation->getPointCount()){int32 offset;double value;if(automation->getPoint(parameterIndex,offset,value)!=kResultOk||offset>i)break;gain=value;parameterIndex++;}while(has&&event.sampleOffset<=i){if(event.type==Event::kNoteOnEvent)note=true;else if(event.type==Event::kNoteOffEvent)note=false;next++;has=next<d.inputEvents->getEventCount();if(has)d.inputEvents->getEvent(next,event);}float tone=note?0.05f*std::sin(phase):0;phase+=6.28318530718*440/rate;for(int c=0;c<2;c++){float v=d.numInputs&&d.inputs[0].numChannels>c?d.inputs[0].channelBuffers32[c][i]:0;d.outputs[0].channelBuffers32[c][i]=v*float(gain*2)+tone;}}return kResultOk;}
 tresult PLUGIN_API setComponentState(IBStream* s)override{return setState(s);}int32 PLUGIN_API getParameterCount()override{return 1;}tresult PLUGIN_API getParameterInfo(int32 i,ParameterInfo& p)override{if(i)return kInvalidArgument;p={};p.id=1000;name16(p.title,"Fixture Gain");p.defaultNormalizedValue=0.5;p.flags=ParameterInfo::kCanAutomate;return kResultOk;}tresult PLUGIN_API getParamStringByValue(ParamID,double,String128 s)override{name16(s,"Gain");return kResultOk;}tresult PLUGIN_API getParamValueByString(ParamID,TChar*,ParamValue&)override{return kNotImplemented;}ParamValue PLUGIN_API normalizedParamToPlain(ParamID,ParamValue v)override{return v*2;}ParamValue PLUGIN_API plainParamToNormalized(ParamID,ParamValue v)override{return v/2;}ParamValue PLUGIN_API getParamNormalized(ParamID)override{return gain;}tresult PLUGIN_API setParamNormalized(ParamID,ParamValue v)override{gain=v;return kResultOk;}tresult PLUGIN_API setComponentHandler(IComponentHandler* handler)override{handlerReady=handler!=nullptr;return kResultOk;}
 IPlugView* PLUGIN_API createView(FIDString)override{
#if defined(SLOP_FIXTURE_CONCURRENT_EDITOR)
  return new FixtureView();
#else
  return nullptr;
#endif
 }
};
class Factory:public IPluginFactory3{public:tresult PLUGIN_API queryInterface(const TUID id,void** p)override{if(eq(id,INLINE_UID_OF(IPluginFactory))||eq(id,INLINE_UID_OF(IPluginFactory2))||eq(id,INLINE_UID_OF(IPluginFactory3))||eq(id,INLINE_UID_OF(FUnknown))){*p=this;return kResultOk;}*p=nullptr;return kNoInterface;}uint32 PLUGIN_API addRef()override{return 1;}uint32 PLUGIN_API release()override{return 1;}tresult PLUGIN_API getFactoryInfo(PFactoryInfo* p)override{*p={};std::strcpy(p->vendor,"Slop Host Verification Fixture");return kResultOk;}int32 PLUGIN_API countClasses()override{return 1;}tresult PLUGIN_API getClassInfo(int32 i,PClassInfo* p)override{if(i)return kInvalidArgument;*p={};std::memcpy(p->cid,FixtureId,16);p->cardinality=PClassInfo::kManyInstances;std::strcpy(p->category,kVstAudioEffectClass);std::strcpy(p->name,"Host Fixture Gain");return kResultOk;}tresult PLUGIN_API getClassInfo2(int32 index,PClassInfo2* info)override{if(index)return kInvalidArgument;*info={};std::memcpy(info->cid,FixtureId,16);info->cardinality=PClassInfo::kManyInstances;std::strcpy(info->category,kVstAudioEffectClass);std::strcpy(info->name,"Host Fixture Gain");return kResultOk;}tresult PLUGIN_API getClassInfoUnicode(int32,PClassInfoW*)override{return kNotImplemented;}tresult PLUGIN_API setHostContext(FUnknown* context)override{factoryContext=context;return kResultOk;}tresult PLUGIN_API createInstance(FIDString cid,FIDString iid,void** p)override{if(!eq(cid,FixtureId))return kNoInterface;auto plugin=new Fixture();auto result=plugin->queryInterface(iid,p);plugin->release();return result;}};
extern "C" __declspec(dllexport) IPluginFactory* GetPluginFactory(){static Factory factory;return &factory;}
extern "C" __declspec(dllexport) bool InitDll(){return true;}
extern "C" __declspec(dllexport) bool ExitDll(){return true;}
