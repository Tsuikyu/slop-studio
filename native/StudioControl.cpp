// Original MIT host glue. Lua is the official MIT-licensed Lua 5.4.8 implementation.
#define WIN32_LEAN_AND_MEAN
#define DIRECTINPUT_VERSION 0x0800
#include <windows.h>
#include <dinput.h>
#include <xinput.h>
#include <string>
#include <cstdlib>
#include <cstdio>
#include <cmath>
#include <algorithm>
extern "C" {
#include "lua.h"
#include "lauxlib.h"
#include "lualib.h"
}
#define API extern "C" __declspec(dllexport)
struct ControllerState { int kind,index;unsigned buttons;float x,y,rx,ry,leftTrigger,rightTrigger; };
static IDirectInput8W* directInput=nullptr;static IDirectInputDevice8W* joystick=nullptr;static HWND owner=nullptr;static ULONGLONG nextEnumeration=0;
static BOOL CALLBACK chooseJoystick(const DIDEVICEINSTANCEW* instance,void*){
 if(SUCCEEDED(directInput->CreateDevice(instance->guidInstance,&joystick,nullptr))){
  if(FAILED(joystick->SetDataFormat(&c_dfDIJoystick2))||FAILED(joystick->SetCooperativeLevel(owner,DISCL_BACKGROUND|DISCL_NONEXCLUSIVE))){joystick->Release();joystick=nullptr;return DIENUM_CONTINUE;}
  DIPROPRANGE range{};range.diph.dwSize=sizeof range;range.diph.dwHeaderSize=sizeof range.diph;range.diph.dwHow=DIPH_DEVICE;range.lMin=-32768;range.lMax=32767;joystick->SetProperty(DIPROP_RANGE,&range.diph);joystick->Acquire();return DIENUM_STOP;
 }return DIENUM_CONTINUE;
}
API int slc_controller_mode(void* window,int preferred,ControllerState* out){
 if(!out)return 0;*out={};owner=(HWND)window;
 using GetState=DWORD(WINAPI*)(DWORD,XINPUT_STATE*);static HMODULE xmodule=LoadLibraryW(L"xinput1_4.dll");static GetState state=xmodule?(GetState)GetProcAddress(xmodule,"XInputGetState"):nullptr;
 if(preferred!=2&&state)for(DWORD i=0;i<4;i++){XINPUT_STATE s{};if(state(i,&s)==ERROR_SUCCESS){auto& g=s.Gamepad;out->kind=1;out->index=i;WORD flags[]={XINPUT_GAMEPAD_A,XINPUT_GAMEPAD_B,XINPUT_GAMEPAD_X,XINPUT_GAMEPAD_Y,XINPUT_GAMEPAD_LEFT_SHOULDER,XINPUT_GAMEPAD_RIGHT_SHOULDER,XINPUT_GAMEPAD_BACK,XINPUT_GAMEPAD_START,XINPUT_GAMEPAD_DPAD_LEFT,XINPUT_GAMEPAD_DPAD_RIGHT,XINPUT_GAMEPAD_DPAD_UP,XINPUT_GAMEPAD_DPAD_DOWN,XINPUT_GAMEPAD_LEFT_THUMB,XINPUT_GAMEPAD_RIGHT_THUMB};for(int bit=0;bit<14;bit++)if(g.wButtons&flags[bit])out->buttons|=1u<<bit;out->x=g.sThumbLX/32768.f;out->y=-g.sThumbLY/32768.f;out->rx=g.sThumbRX/32768.f;out->ry=-g.sThumbRY/32768.f;out->leftTrigger=g.bLeftTrigger/255.f;out->rightTrigger=g.bRightTrigger/255.f;return 1;}}
 if(preferred==1)return 0;
 if(!directInput&&FAILED(DirectInput8Create(GetModuleHandleW(nullptr),DIRECTINPUT_VERSION,IID_IDirectInput8W,(void**)&directInput,nullptr)))return 0;
 if(!joystick&&GetTickCount64()>nextEnumeration){nextEnumeration=GetTickCount64()+3000;directInput->EnumDevices(DI8DEVCLASS_GAMECTRL,chooseJoystick,nullptr,DIEDFL_ATTACHEDONLY);}
 if(joystick){DIJOYSTATE2 s{};HRESULT hr=joystick->Poll();if(FAILED(hr)){joystick->Acquire();hr=joystick->Poll();}if(SUCCEEDED(hr)&&SUCCEEDED(joystick->GetDeviceState(sizeof s,&s))){out->kind=2;for(int i=0;i<8;i++)if(s.rgbButtons[i]&128)out->buttons|=1u<<i;DWORD pov=s.rgdwPOV[0];if(pov!=0xffffffff){if(pov>4500&&pov<13500)out->buttons|=1u<<9;if(pov>22500&&pov<31500)out->buttons|=1u<<8;if(pov<4500||pov>31500)out->buttons|=1u<<10;if(pov>13500&&pov<22500)out->buttons|=1u<<11;}out->x=std::clamp(s.lX/32768.f,-1.f,1.f);out->y=std::clamp(s.lY/32768.f,-1.f,1.f);out->rx=std::clamp(s.lRx/32768.f,-1.f,1.f);out->ry=std::clamp(s.lRy/32768.f,-1.f,1.f);return 1;}
  joystick->Unacquire();joystick->Release();joystick=nullptr;
 }return 0;
}
API int slc_controller(void* window,ControllerState* out){return slc_controller_mode(window,0,out);}
API void slc_controller_close(){if(joystick){joystick->Unacquire();joystick->Release();joystick=nullptr;}if(directInput){directInput->Release();directInput=nullptr;}}
using ScriptCallback=int(__cdecl*)(const char*,char*,int);
struct ScriptContext {size_t memory=0;int instructions=0;ScriptCallback callback=nullptr;};
static void* allocate(void* ud,void* ptr,size_t old,size_t size){auto* c=(ScriptContext*)ud;if(!ptr)old=0;if(size==0){c->memory-=old;free(ptr);return nullptr;}if(size>old&&c->memory+size-old>32*1024*1024)return nullptr;void* next=realloc(ptr,size);if(next)c->memory=c->memory+size-old;return next;}
static ScriptContext* scriptContext(lua_State* L){void* ud=nullptr;lua_getallocf(L,&ud);return (ScriptContext*)ud;}
static void hook(lua_State* L,lua_Debug*){auto* c=scriptContext(L);if((c->instructions+=1000)>500000)luaL_error(L,"Lua instruction budget exceeded.");}
static std::string quote(const char* value){std::string out="\"";for(auto p=(const unsigned char*)value;*p;p++){unsigned char c=*p;if(c=='"'||c=='\\'){out+='\\';out+=(char)c;}else if(c<32){char b[7];std::snprintf(b,7,"\\u%04x",c);out+=b;}else out+=(char)c;}out+='"';return out;}
static std::string value(lua_State* L,int index,int depth){if(depth>6)luaL_error(L,"Lua command table is too deep.");int type=lua_type(L,index);if(type==LUA_TNIL)return "null";if(type==LUA_TBOOLEAN)return lua_toboolean(L,index)?"true":"false";if(type==LUA_TNUMBER){double n=lua_tonumber(L,index);if(!std::isfinite(n))luaL_error(L,"Non-finite command number.");char b[64];std::snprintf(b,64,"%.17g",n);return b;}if(type==LUA_TSTRING)return quote(lua_tostring(L,index));if(type==LUA_TTABLE){index=lua_absindex(L,index);std::string s="{";int count=0;lua_pushnil(L);while(lua_next(L,index)){if(++count>256)luaL_error(L,"Too many command fields.");if(lua_type(L,-2)!=LUA_TSTRING)luaL_error(L,"Command fields must be named.");if(count>1)s+=',';s+=quote(lua_tostring(L,-2))+":"+value(L,-1,depth+1);lua_pop(L,1);if(s.size()>16384)luaL_error(L,"Command exceeds 16 KiB.");}s+='}';return s;}luaL_error(L,"Unsupported command value.");return "";}
static int hostCall(lua_State* L){const char* name=luaL_checkstring(L,1);luaL_checktype(L,2,LUA_TTABLE);std::string request="{\"command\":"+quote(name)+",\"arguments\":"+value(L,2,0)+"}";char response[32768]{};auto* context=scriptContext(L);if(context->callback(request.c_str(),response,sizeof response)!=0)return luaL_error(L,"%s",response);int top=lua_gettop(L);if(luaL_loadbufferx(L,response,strlen(response),"host response","t")!=LUA_OK)return lua_error(L);if(lua_pcall(L,0,1,0)!=LUA_OK)return lua_error(L);return lua_gettop(L)>top?1:0;}
API int slc_lua(const char* source,ScriptCallback callback,char* error,int cap){
 ScriptContext context{};context.callback=callback;lua_State* L=lua_newstate(allocate,&context);if(!L){strncpy_s(error,cap,"Could not allocate Lua state.",_TRUNCATE);return -1;}
 luaL_requiref(L,"_G",luaopen_base,1);lua_pop(L,1);luaL_requiref(L,LUA_MATHLIBNAME,luaopen_math,1);lua_pop(L,1);luaL_requiref(L,LUA_TABLIBNAME,luaopen_table,1);lua_pop(L,1);luaL_requiref(L,LUA_STRLIBNAME,luaopen_string,1);lua_pop(L,1);luaL_requiref(L,LUA_UTF8LIBNAME,luaopen_utf8,1);lua_pop(L,1);
 for(const char* name:{"dofile","loadfile","load","print","collectgarbage"}){lua_pushnil(L);lua_setglobal(L,name);}
 lua_newtable(L);lua_pushcfunction(L,hostCall);lua_setfield(L,-2,"call");lua_setglobal(L,"slop");lua_sethook(L,hook,LUA_MASKCOUNT,1000);
 int result=luaL_loadbufferx(L,source,strlen(source),"Slop Lua Mod","t");if(result==LUA_OK)result=lua_pcall(L,0,0,0);if(result!=LUA_OK)strncpy_s(error,cap,lua_tostring(L,-1)?lua_tostring(L,-1):"Lua failed.",_TRUNCATE);
 lua_close(L);return result==LUA_OK?0:-1;
}
