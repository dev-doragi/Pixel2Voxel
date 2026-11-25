"""Development-only Lua 5.4 runner using installed shared library (no extension dependency)."""
import ctypes as c, ctypes.util, sys
lib=c.CDLL(ctypes.util.find_library('lua5.4') or 'liblua5.4.so.0')
def bind(name,result,args):
    fn=getattr(lib,name);fn.restype=result;fn.argtypes=args;return fn
P=c.c_void_p;I=c.c_int;S=c.c_char_p
new=bind('luaL_newstate',P,[]);openlibs=bind('luaL_openlibs',None,[P]);load=bind('luaL_loadfilex',I,[P,S,S]);call=bind('lua_pcallk',I,[P,I,I,I,c.c_longlong,P]);msg=bind('lua_tolstring',S,[P,I,P]);close=bind('lua_close',None,[P]);state=new();openlibs(state)
try:
    r=load(state,sys.argv[1].encode(),None)
    if not r:r=call(state,0,0,0,0,None)
    if r:raise RuntimeError(msg(state,-1,None).decode(errors='replace'))
finally:close(state)
