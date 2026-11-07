import ctypes as C
import pathlib
import sys

root = pathlib.Path(sys.argv[1]).resolve()
api = C.CDLL(str(root / 'SlopNative.dll'))
class Info(C.Structure):
    _fields_=[('id',C.c_char*256),('name',C.c_char*256),('vendor',C.c_char*128),('format',C.c_int32),('instrument',C.c_int32)]
class Parameter(C.Structure):
    _fields_=[('id',C.c_uint32),('name',C.c_char*128),('value',C.c_double),('minimum',C.c_double),('maximum',C.c_double),('steps',C.c_int32)]
class Midi(C.Structure):
    _fields_=[('offset',C.c_int32),('status',C.c_int32),('data1',C.c_int32),('data2',C.c_int32)]
api.sxp_scan.argtypes=[C.c_wchar_p,C.POINTER(Info),C.c_int,C.c_char_p,C.c_int]
api.sxp_create.argtypes=[C.c_wchar_p,C.c_char_p,C.c_double,C.c_int,C.c_char_p,C.c_int]; api.sxp_create.restype=C.c_void_p
api.sxp_process.argtypes=[C.c_void_p,C.POINTER(C.c_float),C.c_int,C.POINTER(Midi),C.c_int,C.c_double,C.c_double,C.c_int,C.c_int,C.c_int]
api.sxp_param_count.argtypes=[C.c_void_p]
api.sxp_param_info.argtypes=[C.c_void_p,C.c_int,C.POINTER(Parameter)]
api.sxp_param_set.argtypes=[C.c_void_p,C.c_uint32,C.c_double]
api.sxp_state_save.argtypes=[C.c_void_p,C.c_void_p,C.c_int]
api.sxp_state_load.argtypes=[C.c_void_p,C.c_void_p,C.c_int]
api.sxp_destroy.argtypes=[C.c_void_p]

def process(handle, value=0.2, events=None):
    audio=(C.c_float*512)(*[value]*512)
    es=(Midi*len(events))(*events) if events else None
    assert api.sxp_process(handle,audio,256,es,len(events or []),120,0,1,4,4)==0
    return list(audio)

for extension in ['vst3','clap']:
    path=str(root / ('TestFixtures/FixtureGain.'+extension) if (root/'TestFixtures').exists() else root / ('FixtureGain.'+extension))
    info=(Info*8)(); error=C.create_string_buffer(2048)
    count=api.sxp_scan(path,info,8,error,len(error))
    assert count==1, error.value
    handle=api.sxp_create(path,info[0].id,48000,256,error,len(error))
    assert handle, error.value
    try:
        assert api.sxp_param_count(handle)==1
        param=Parameter(); assert api.sxp_param_info(handle,0,C.byref(param))==1
        assert param.id==1000
        assert abs(process(handle)[0]-0.2)<1e-5
        assert api.sxp_param_set(handle,1000,0.25)==1
        assert abs(process(handle)[0]-0.1)<1e-5
        size=api.sxp_state_save(handle,None,0); assert size>0
        state=C.create_string_buffer(size); assert api.sxp_state_save(handle,state,size)==size
        assert api.sxp_param_set(handle,1000,0.9)==1
        assert abs(process(handle)[0]-0.36)<1e-5
        assert api.sxp_state_load(handle,state,size)==1
        assert abs(process(handle)[0]-0.1)<1e-5
        tone=process(handle,0,[Midi(0,0x90,60,100)])
        assert max(abs(v) for v in tone)>0.01, 'note event did not reach plugin'
        silence=process(handle,0,[Midi(0,0x80,60,0)])
        assert max(abs(v) for v in silence)<1e-6, 'note off did not reach plugin'
        second=api.sxp_create(path,info[0].id,48000,256,error,len(error))
        assert second, error.value
        try:
            assert abs(process(second)[0]-0.2)<1e-5
        finally:
            api.sxp_destroy(second)
        assert abs(process(handle)[0]-0.1)<1e-5
        print('PASS',extension,'real discovery, instantiate, float processing, parameter change, state restore, partial stream reads, note on/off, multiple instances and unload')
    finally:
        api.sxp_destroy(handle)
error=C.create_string_buffer(2048); items=(Info*4)()
assert api.sxp_scan(str(root/'not-a-plugin.dll'),items,4,error,len(error))<0
assert error.value
print('PASS failed plugin load returns a meaningful error')
