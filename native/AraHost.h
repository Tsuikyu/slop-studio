// Original MIT ARA host implementation using the official Apache-2.0 ARA 2.3 API.
#pragma once
#include "ARA_API/ARAInterface.h"
#include "ARA_API/ARAVST3.h"
#include <filesystem>
#include <cstring>
#include <cmath>
#include <cwctype>
#include <cstdio>
#include <stdexcept>
#include <fstream>
#include <memory>
#include <map>
#include <atomic>
#include <vector>
#include <algorithm>
namespace slop_ara {
using namespace ARA;
struct AudioData {
 int rate=0,channels=0;std::vector<float> samples;
 static std::shared_ptr<AudioData> load(const std::wstring& path){
  std::ifstream file(std::filesystem::path(path),std::ios::binary);if(!file)throw std::runtime_error("ARA audio source could not be opened.");
  auto u16=[](const unsigned char* p){return unsigned(p[0])|(unsigned(p[1])<<8);};auto u32=[](const unsigned char* p){return uint32_t(p[0])|(uint32_t(p[1])<<8)|(uint32_t(p[2])<<16)|(uint32_t(p[3])<<24);};
  unsigned char head[12];file.read((char*)head,12);if(!file||memcmp(head,"RIFF",4)||memcmp(head+8,"WAVE",4))throw std::runtime_error("ARA sources must be WAV PCM or IEEE float.");
  auto data=std::make_shared<AudioData>();int format=0,bits=0;std::vector<unsigned char> audio;
  while(file){unsigned char chunk[8];file.read((char*)chunk,8);if(!file)break;uint32_t n=u32(chunk+4);if(n>256u*1024*1024)throw std::runtime_error("ARA WAV chunk exceeds 256 MiB.");
   if(!memcmp(chunk,"fmt ",4)){std::vector<unsigned char> f(n);file.read((char*)f.data(),n);if(n<16)throw std::runtime_error("Invalid ARA WAV format.");format=u16(f.data());data->channels=u16(f.data()+2);data->rate=u32(f.data()+4);bits=u16(f.data()+14);if(format==0xfffe&&n>=40)format=u16(f.data()+24);}
   else if(!memcmp(chunk,"data",4)){audio.resize(n);file.read((char*)audio.data(),n);}else file.seekg(n,std::ios::cur);if(n&1)file.seekg(1,std::ios::cur);
  }
  if(data->channels<1||data->channels>2||data->rate<8000||(format!=1&&format!=3)||(bits!=8&&bits!=16&&bits!=24&&bits!=32)||(format==3&&bits!=32))throw std::runtime_error("Unsupported ARA WAV format.");
  int bytes=bits/8;data->samples.resize(audio.size()/bytes);for(size_t i=0;i<data->samples.size();i++){auto p=audio.data()+i*bytes;float v=0;if(format==3)memcpy(&v,p,4);else if(bytes==1)v=(int(p[0])-128)/128.f;else if(bytes==2)v=(int16_t)u16(p)/32768.f;else if(bytes==3){int32_t n=p[0]|(p[1]<<8)|(p[2]<<16);if(n&0x800000)n|=0xff000000;v=n/8388608.f;}else v=(int32_t)u32(p)/2147483648.f;if(!std::isfinite(v))throw std::runtime_error("ARA WAV contains non-finite samples.");data->samples[i]=v;}return data;
 }
};
class Session {
 struct Source {std::wstring path;std::string name,id;std::shared_ptr<AudioData> data;ARAAudioSourceRef ref=nullptr;};
 struct Region {std::string id,name;Source* source=nullptr;ARAAudioModificationRef modification=nullptr;ARAPlaybackRegionRef ref=nullptr;bool seen=false;};
 struct Reader{Source* source;bool wide;};
 struct ContentReader{Session* session;ARAContentType type;};
 struct Archive{Session* session;std::vector<unsigned char> bytes;};
 const ARAFactory* factory=nullptr;const ARADocumentControllerInstance* document=nullptr;const ARAPlugInExtensionInstance* extension=nullptr;
 ARADocumentControllerHostInstance host{};ARAAudioAccessControllerInterface audio{};ARAArchivingControllerInterface archive{};ARAContentAccessControllerInterface content{};ARAModelUpdateControllerInterface model{};ARAPlaybackControllerInterface playback{};
 ARAMusicalContextRef context=nullptr;ARARegionSequenceRef sequence=nullptr;std::map<std::wstring,std::unique_ptr<Source>> sources;std::map<std::string,std::unique_ptr<Region>> regions;bool initialized=false,editing=false;
 double tempo=120;int numerator=4,denominator=4;ARAContentTempoEntry tempoData[2]{};ARAContentBarSignature signature{};
 const ARADocumentControllerInterface* api()const{return document->documentControllerInterface;}ARADocumentControllerRef doc()const{return document->documentControllerRef;}
 static Session* self(void* p){return (Session*)p;}
 static std::string stableID(const std::wstring& path){uint64_t h=1469598103934665603ull;for(wchar_t c:path){h^=(uint16_t)towlower(c);h*=1099511628211ull;}char b[64];std::snprintf(b,sizeof b,"source-%016llx",(unsigned long long)h);return b;}
 void destroyRegion(Region& r){if(extension&&extension->playbackRendererInterface)extension->playbackRendererInterface->removePlaybackRegion(extension->playbackRendererRef,r.ref);api()->destroyPlaybackRegion(doc(),r.ref);api()->destroyAudioModification(doc(),r.modification);}
 void configureHost(){
  audio.structSize=sizeof audio;
  audio.createAudioReaderForSource=[](ARAAudioAccessControllerHostRef,ARAAudioSourceHostRef source,ARABool wide)->ARAAudioReaderHostRef{return reinterpret_cast<ARAAudioReaderHostRef>(new Reader{(Source*)source,wide!=0});};
  audio.readAudioSamples=[](ARAAudioAccessControllerHostRef,ARAAudioReaderHostRef ref,ARASamplePosition position,ARASampleCount count,void* const buffers[])->ARABool{
   auto* reader=(Reader*)ref;const auto& data=*reader->source->data;long long frames=data.samples.size()/data.channels;
   if(count<0||count>16*1024*1024)return false;
   for(int c=0;c<data.channels;c++)for(long long i=0;i<count;i++){long long at=position+i;double value=at>=0&&at<frames?data.samples[(size_t)at*data.channels+c]:0;if(reader->wide)((double*)buffers[c])[i]=value;else ((float*)buffers[c])[i]=(float)value;}return true;
  };
  audio.destroyAudioReader=[](ARAAudioAccessControllerHostRef,ARAAudioReaderHostRef reader){delete (Reader*)reader;};
  archive.structSize=sizeof archive;
  archive.getArchiveSize=[](ARAArchivingControllerHostRef,ARAArchiveReaderHostRef reader)->ARASize{return ((Archive*)reader)->bytes.size();};
  archive.readBytesFromArchive=[](ARAArchivingControllerHostRef,ARAArchiveReaderHostRef reader,ARASize pos,ARASize size,ARAByte* data)->ARABool{auto* a=(Archive*)reader;if(pos+size>a->bytes.size())return false;memcpy(data,a->bytes.data()+pos,size);return true;};
  archive.writeBytesToArchive=[](ARAArchivingControllerHostRef,ARAArchiveWriterHostRef writer,ARASize pos,ARASize size,const ARAByte* data)->ARABool{auto* a=(Archive*)writer;if(pos+size>8*1024*1024)return false;if(pos+size>a->bytes.size())a->bytes.resize(pos+size);memcpy(a->bytes.data()+pos,data,size);return true;};
  archive.notifyDocumentArchivingProgress=[](ARAArchivingControllerHostRef,float){};
  archive.notifyDocumentUnarchivingProgress=[](ARAArchivingControllerHostRef,float){};
  archive.getDocumentArchiveID=[](ARAArchivingControllerHostRef ref,ARAArchiveReaderHostRef)->ARAPersistentID{return ((Session*)ref)->factory->documentArchiveID;};
  content.structSize=sizeof content;
  content.isMusicalContextContentAvailable=[](ARAContentAccessControllerHostRef,ARAMusicalContextHostRef,ARAContentType type)->ARABool{return type==kARAContentTypeTempoEntries||type==kARAContentTypeBarSignatures;};
  content.getMusicalContextContentGrade=[](ARAContentAccessControllerHostRef,ARAMusicalContextHostRef,ARAContentType)->ARAContentGrade{return kARAContentGradeInitial;};
  content.createMusicalContextContentReader=[](ARAContentAccessControllerHostRef ref,ARAMusicalContextHostRef,ARAContentType type,const ARAContentTimeRange*)->ARAContentReaderHostRef{return reinterpret_cast<ARAContentReaderHostRef>(new ContentReader{(Session*)ref,type});};
  content.isAudioSourceContentAvailable=[](ARAContentAccessControllerHostRef,ARAAudioSourceHostRef,ARAContentType)->ARABool{return false;};
  content.getAudioSourceContentGrade=[](ARAContentAccessControllerHostRef,ARAAudioSourceHostRef,ARAContentType)->ARAContentGrade{return kARAContentGradeInitial;};
  content.createAudioSourceContentReader=[](ARAContentAccessControllerHostRef,ARAAudioSourceHostRef,ARAContentType,const ARAContentTimeRange*)->ARAContentReaderHostRef{return nullptr;};
  content.getContentReaderEventCount=[](ARAContentAccessControllerHostRef,ARAContentReaderHostRef ref)->ARAInt32{return ((ContentReader*)ref)->type==kARAContentTypeTempoEntries?2:1;};
  content.getContentReaderDataForEvent=[](ARAContentAccessControllerHostRef,ARAContentReaderHostRef ref,ARAInt32 index)->const void*{auto* r=(ContentReader*)ref;if(r->type==kARAContentTypeTempoEntries)return index>=0&&index<2?static_cast<const void*>(&r->session->tempoData[index]):nullptr;return index==0?static_cast<const void*>(&r->session->signature):nullptr;};
  content.destroyContentReader=[](ARAContentAccessControllerHostRef,ARAContentReaderHostRef ref){delete (ContentReader*)ref;};
  model.structSize=sizeof model;
  model.notifyAudioSourceAnalysisProgress=[](ARAModelUpdateControllerHostRef ref,ARAAudioSourceHostRef,ARAAnalysisProgressState,float){((Session*)ref)->changed=true;};
  model.notifyAudioSourceContentChanged=[](ARAModelUpdateControllerHostRef ref,ARAAudioSourceHostRef,const ARAContentTimeRange*,ARAContentUpdateFlags){((Session*)ref)->changed=true;};
  model.notifyAudioModificationContentChanged=[](ARAModelUpdateControllerHostRef ref,ARAAudioModificationHostRef,const ARAContentTimeRange*,ARAContentUpdateFlags){((Session*)ref)->changed=true;};
  model.notifyPlaybackRegionContentChanged=[](ARAModelUpdateControllerHostRef ref,ARAPlaybackRegionHostRef,const ARAContentTimeRange*,ARAContentUpdateFlags){((Session*)ref)->changed=true;};
  model.notifyDocumentDataChanged=[](ARAModelUpdateControllerHostRef ref){((Session*)ref)->changed=true;};
  playback.structSize=sizeof playback;
  playback.requestStartPlayback=[](ARAPlaybackControllerHostRef ref){((Session*)ref)->requests|=1;};
  playback.requestStopPlayback=[](ARAPlaybackControllerHostRef ref){((Session*)ref)->requests|=2;};
  playback.requestSetPlaybackPosition=[](ARAPlaybackControllerHostRef ref,ARATimePosition position){auto* s=(Session*)ref;s->requestedPosition=position;s->requests|=4;};
  playback.requestSetCycleRange=[](ARAPlaybackControllerHostRef ref,ARATimePosition start,ARATimeDuration length){auto* s=(Session*)ref;s->requestedCycleStart=start;s->requestedCycleLength=length;s->requests|=8;};
  playback.requestEnableCycle=[](ARAPlaybackControllerHostRef ref,ARABool enable){auto* s=(Session*)ref;s->requestedCycle=enable!=0;s->requests|=16;};
  host={sizeof host,reinterpret_cast<ARAAudioAccessControllerHostRef>(this),&audio,reinterpret_cast<ARAArchivingControllerHostRef>(this),&archive,reinterpret_cast<ARAContentAccessControllerHostRef>(this),&content,reinterpret_cast<ARAModelUpdateControllerHostRef>(this),&model,reinterpret_cast<ARAPlaybackControllerHostRef>(this),&playback};
 }
public:
 bool changed=false;int requests=0;double requestedPosition=0,requestedCycleStart=0,requestedCycleLength=4;bool requestedCycle=false;
 explicit Session(Steinberg::Vst::IComponent* component){
  try{tempoData[0]={0,0};tempoData[1]={.5,1};signature={4,4,0};configureHost();IPlugInEntryPoint* entry=nullptr;if(component->queryInterface(INLINE_UID_OF(IPlugInEntryPoint),(void**)&entry)!=Steinberg::kResultOk||!entry)throw std::runtime_error("This VST3 component does not expose an ARA factory.");factory=entry->getFactory();entry->release();if(!factory)throw std::runtime_error("The ARA factory is unavailable.");
   ARAInterfaceConfiguration configuration{sizeof configuration,kARAAPIGeneration_2_0_Final,nullptr};if(factory->lowestSupportedApiGeneration>configuration.desiredApiGeneration||factory->highestSupportedApiGeneration<configuration.desiredApiGeneration)throw std::runtime_error("The plug-in requires an unsupported ARA API generation.");
   factory->initializeARAWithConfiguration(&configuration);initialized=true;ARADocumentProperties properties{sizeof properties,"Slop Studio"};document=factory->createDocumentControllerWithDocument(&host,&properties);if(!document)throw std::runtime_error("ARA document creation failed.");
   IPlugInEntryPoint2* roles=nullptr;if(component->queryInterface(INLINE_UID_OF(IPlugInEntryPoint2),(void**)&roles)!=Steinberg::kResultOk||!roles)throw std::runtime_error("ARA 2 role binding is not supported by this plug-in.");const auto known=kARAPlaybackRendererRole|kARAEditorRendererRole|kARAEditorViewRole;extension=roles->bindToDocumentControllerWithRoles(doc(),known,kARAPlaybackRendererRole|kARAEditorViewRole);roles->release();if(!extension||!extension->playbackRendererInterface)throw std::runtime_error("ARA playback renderer binding failed.");
   api()->beginEditing(doc());ARAMusicalContextProperties music{sizeof music,"Slop Project",0,nullptr};context=api()->createMusicalContext(doc(),reinterpret_cast<ARAMusicalContextHostRef>(this),&music);ARARegionSequenceProperties seq{sizeof seq,"Audio",0,context,nullptr};sequence=api()->createRegionSequence(doc(),reinterpret_cast<ARARegionSequenceHostRef>(this),&seq);setTempo(120,4,4);api()->endEditing(doc());
  }catch(...){prepareClose();if(initialized){factory->uninitializeARA();initialized=false;}throw;}
 }
 void setTempo(double value,int n,int d){tempo=value;numerator=n;denominator=d;tempoData[0]={0,0};tempoData[1]={60/tempo,1};signature={n,d,0};if(document&&context)api()->updateMusicalContextContent(doc(),context,nullptr,kARAContentUpdateEverythingChanged);}
 void begin(double bpm,int n,int d){if(!editing){api()->beginEditing(doc());editing=true;}for(auto& r:regions)r.second->seen=false;setTempo(bpm,n,d);}
 void add(const wchar_t* path,const char* id,const char* name,double start,double offset,double duration){
  if(!editing)throw std::runtime_error("ARA model update was not started.");std::wstring file(path);Source* source=nullptr;auto found=sources.find(file);
  if(found==sources.end()){auto s=std::make_unique<Source>();s->path=file;s->name=name;s->id=stableID(file);s->data=AudioData::load(file);ARAAudioSourceProperties p{};p.structSize=sizeof p;p.name=s->name.c_str();p.persistentID=s->id.c_str();p.sampleCount=s->data->samples.size()/s->data->channels;p.sampleRate=s->data->rate;p.channelCount=s->data->channels;p.merits64BitSamples=false;s->ref=api()->createAudioSource(doc(),reinterpret_cast<ARAAudioSourceHostRef>(s.get()),&p);if(!s->ref)throw std::runtime_error("ARA audio source creation failed.");api()->enableAudioSourceSamplesAccess(doc(),s->ref,true);source=s.get();sources[file]=std::move(s);}else source=found->second.get();
  auto it=regions.find(id);if(it!=regions.end()&&it->second->source!=source){destroyRegion(*it->second);regions.erase(it);it=regions.end();}
  if(it==regions.end()){auto r=std::make_unique<Region>();r->id=id;r->name=name;r->source=source;std::string mod="mod-"+r->id;ARAAudioModificationProperties props{sizeof props,r->name.c_str(),mod.c_str()};r->modification=api()->createAudioModification(doc(),source->ref,reinterpret_cast<ARAAudioModificationHostRef>(r.get()),&props);if(!r->modification)throw std::runtime_error("ARA audio modification creation failed.");regions[id]=std::move(r);it=regions.find(id);}
  Region& r=*it->second;r.seen=true;r.name=name;ARAPlaybackRegionProperties props{};props.structSize=sizeof props;props.startInModificationTime=offset;props.durationInModificationTime=duration;props.startInPlaybackTime=start;props.durationInPlaybackTime=duration;props.regionSequenceRef=sequence;props.name=r.name.c_str();
  if(!r.ref){r.ref=api()->createPlaybackRegion(doc(),r.modification,reinterpret_cast<ARAPlaybackRegionHostRef>(&r),&props);if(!r.ref)throw std::runtime_error("ARA playback region creation failed.");extension->playbackRendererInterface->addPlaybackRegion(extension->playbackRendererRef,r.ref);}else api()->updatePlaybackRegionProperties(doc(),r.ref,&props);
 }
 void end(){if(!editing)return;for(auto i=regions.begin();i!=regions.end();)if(!i->second->seen){destroyRegion(*i->second);i=regions.erase(i);}else ++i;api()->endEditing(doc());editing=false;if(extension&&extension->editorViewInterface){std::vector<ARAPlaybackRegionRef> selected;for(auto& r:regions)selected.push_back(r.second->ref);ARAViewSelection selection{};selection.structSize=sizeof selection;selection.playbackRegionRefsCount=selected.size();selection.playbackRegionRefs=selected.data();extension->editorViewInterface->notifySelection(extension->editorViewRef,&selection);}}
 void idle(){if(document&&!editing)api()->notifyModelUpdates(doc());}
 std::vector<unsigned char> save(){Archive data{this,{}};ARAStoreObjectsFilter filter{};filter.structSize=sizeof filter;filter.documentData=true;if(!api()->storeObjectsToArchive(doc(),reinterpret_cast<ARAArchiveWriterHostRef>(&data),nullptr))throw std::runtime_error("ARA document archiving failed.");return data.bytes;}
 bool restore(const unsigned char* data,int size){Archive a{this,std::vector<unsigned char>(data,data+size)};api()->beginEditing(doc());bool result=api()->restoreObjectsFromArchive(doc(),reinterpret_cast<ARAArchiveReaderHostRef>(&a),nullptr)!=0;api()->endEditing(doc());return result;}
 void prepareClose(){
  if(!document)return;if(editing){api()->endEditing(doc());editing=false;}
  api()->beginEditing(doc());for(auto& r:regions)destroyRegion(*r.second);regions.clear();for(auto& s:sources){api()->enableAudioSourceSamplesAccess(doc(),s.second->ref,false);api()->destroyAudioSource(doc(),s.second->ref);}sources.clear();if(sequence){api()->destroyRegionSequence(doc(),sequence);sequence=nullptr;}if(context){api()->destroyMusicalContext(doc(),context);context=nullptr;}api()->endEditing(doc());api()->destroyDocumentController(doc());document=nullptr;extension=nullptr;
 }
 ~Session(){prepareClose();if(initialized)factory->uninitializeARA();}
};
} // namespace slop_ara
