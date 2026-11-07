#include <clap/clap.h>
#include <cstring>
#include <cmath>
#include <new>
struct Instance{clap_plugin_t api{};double gain=1,rate=48000,phase=0;bool note=false;};
static Instance* self(const clap_plugin_t* p){return (Instance*)p->plugin_data;}
static const char* features[]={CLAP_PLUGIN_FEATURE_AUDIO_EFFECT,CLAP_PLUGIN_FEATURE_STEREO,nullptr};
static clap_plugin_descriptor_t desc{CLAP_VERSION,"org.slop.host.fixture.gain","Host Fixture Gain","Slop Host Verification Fixture","https://github.com/free-audio/clap","","","1.0","Test-only gain, MIDI and state fixture",features};
static clap_plugin_params_t params{
 [](const clap_plugin_t*)->uint32_t{return 1;},
 [](const clap_plugin_t*,uint32_t i,clap_param_info_t* p){if(i)return false;*p={};p->id=1000;p->flags=CLAP_PARAM_IS_AUTOMATABLE;std::strcpy(p->name,"Fixture Gain");p->min_value=0;p->max_value=2;p->default_value=1;return true;},
 [](const clap_plugin_t* p,clap_id id,double* v){if(id!=1000)return false;*v=self(p)->gain;return true;},
 [](const clap_plugin_t*,clap_id,double,char* s,uint32_t size){if(size)std::strncpy(s,"Gain",size);return true;},
 [](const clap_plugin_t*,clap_id,const char*,double*){return false;},
 [](const clap_plugin_t* p,const clap_input_events_t* in,const clap_output_events_t*){for(uint32_t i=0;i<in->size(in);i++){auto e=in->get(in,i);if(e->type==CLAP_EVENT_PARAM_VALUE)self(p)->gain=((const clap_event_param_value_t*)e)->value;}}
};
static clap_plugin_audio_ports_t ports{[](const clap_plugin_t*,bool)->uint32_t{return 1;},[](const clap_plugin_t*,uint32_t index,bool,clap_audio_port_info_t* p){if(index)return false;*p={};p->id=0;p->flags=CLAP_AUDIO_PORT_IS_MAIN;p->channel_count=2;p->port_type=CLAP_PORT_STEREO;p->in_place_pair=0;std::strcpy(p->name,"Stereo");return true;}};
static clap_plugin_note_ports_t notes{[](const clap_plugin_t*,bool in)->uint32_t{return in?1:0;},[](const clap_plugin_t*,uint32_t i,bool in,clap_note_port_info_t* p){if(!in||i)return false;*p={};p->id=0;p->supported_dialects=CLAP_NOTE_DIALECT_CLAP|CLAP_NOTE_DIALECT_MIDI;p->preferred_dialect=CLAP_NOTE_DIALECT_CLAP;std::strcpy(p->name,"Notes");return true;}};
static clap_plugin_state_t state{[](const clap_plugin_t* p,const clap_ostream_t* s){double gain=self(p)->gain;return s->write(s,&gain,sizeof(gain))==sizeof(gain);},[](const clap_plugin_t* p,const clap_istream_t* s){double gain;if(s->read(s,&gain,sizeof(gain))!=sizeof(gain))return false;self(p)->gain=gain;return true;}};
static clap_plugin_factory_t factory{
 [](const clap_plugin_factory_t*)->uint32_t{return 1;},
 [](const clap_plugin_factory_t*,uint32_t i)->const clap_plugin_descriptor_t*{return i?nullptr:&desc;},
 [](const clap_plugin_factory_t*,const clap_host_t*,const char* id)->const clap_plugin_t*{if(std::strcmp(id,desc.id))return nullptr;auto n=new Instance();auto& p=n->api;p.desc=&desc;p.plugin_data=n;p.init=[](const clap_plugin_t*){return true;};p.destroy=[](const clap_plugin_t* p){delete self(p);};p.activate=[](const clap_plugin_t* p,double rate,uint32_t,uint32_t){self(p)->rate=rate;return true;};p.deactivate=[](const clap_plugin_t*){};p.start_processing=[](const clap_plugin_t*){return true;};p.stop_processing=[](const clap_plugin_t*){};p.reset=[](const clap_plugin_t* p){self(p)->note=false;};p.process=[](const clap_plugin_t* p,const clap_process_t* data)->clap_process_status{auto n=self(p);uint32_t eventIndex=0;auto count=data->in_events->size(data->in_events);for(uint32_t i=0;i<data->frames_count;i++){while(eventIndex<count){auto e=data->in_events->get(data->in_events,eventIndex);if(e->time>i)break;if(e->type==CLAP_EVENT_PARAM_VALUE)n->gain=((const clap_event_param_value_t*)e)->value;else if(e->type==CLAP_EVENT_NOTE_ON)n->note=true;else if(e->type==CLAP_EVENT_NOTE_OFF)n->note=false;eventIndex++;}float tone=n->note?0.05f*std::sin(n->phase):0;n->phase+=6.28318530718*440/n->rate;for(int c=0;c<2;c++){float v=data->audio_inputs_count?data->audio_inputs[0].data32[c][i]:0;data->audio_outputs[0].data32[c][i]=v*float(n->gain)+tone;}}return CLAP_PROCESS_CONTINUE;};p.get_extension=[](const clap_plugin_t*,const char* id)->const void*{if(!std::strcmp(id,CLAP_EXT_PARAMS))return &params;if(!std::strcmp(id,CLAP_EXT_AUDIO_PORTS))return &ports;if(!std::strcmp(id,CLAP_EXT_NOTE_PORTS))return &notes;if(!std::strcmp(id,CLAP_EXT_STATE))return &state;return nullptr;};p.on_main_thread=[](const clap_plugin_t*){};return &p;}
};
extern "C" __declspec(dllexport) const clap_plugin_entry_t clap_entry={CLAP_VERSION,[](const char*){return true;},[](){},[](const char* id)->const void*{return !std::strcmp(id,CLAP_PLUGIN_FACTORY_ID)?&factory:nullptr;}};
