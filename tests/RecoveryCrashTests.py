import json, os, pathlib, subprocess, sys, time, wave
app=pathlib.Path(sys.argv[1]).resolve();root=pathlib.Path(sys.argv[2]).resolve();root.mkdir(parents=True,exist_ok=True)
profile=root/"profile";profile.mkdir(exist_ok=True)
(profile/"settings.json").write_text(json.dumps({"schema":"slopx-settings","version":2,"language":"en-US","timelineZoom":8,"audioBackend":"WASAPI","integration":{"RecoverySeconds":10,"QuickStart":False}}))
env=os.environ.copy();env["SLOPX_PROFILE"]=str(profile)
def launch():return subprocess.Popen([str(app),"--mcp"],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding="utf-8",env=env)
def request(p,i,method,params=None):
 p.stdin.write(json.dumps({"jsonrpc":"2.0","id":i,"method":method,"params":params or {}})+"\n");p.stdin.flush()
 result=json.loads(p.stdout.readline())["result"]
 return result
def init(p):
 request(p,1,"initialize",{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"Recovery test","version":"1"}})
 p.stdin.write(json.dumps({"jsonrpc":"2.0","method":"notifications/initialized"})+"\n");p.stdin.flush()
def tool(p,i,name,args):
 result=request(p,i,"tools/call",{"name":name,"arguments":args})
 if result.get("isError"):raise RuntimeError(result)
 return json.loads(result["content"][0]["text"])
p=launch()
try:
 init(p);tid=tool(p,2,"create_track",{"name":"Unclean recovery note","type":"midi"})["trackId"]
 tool(p,3,"add_midi_note",{"trackId":tid,"pitch":71,"velocity":96,"start":0,"duration":1})
 deadline=time.monotonic()+12;snapshot=None
 while time.monotonic()<deadline:
  for session in (profile/"Recovery").glob("*/session.json"):
   meta=json.loads(session.read_text(encoding="utf-8-sig"));file=pathlib.Path(meta["Latest"])
   if file.exists():
    project=json.loads(file.read_text(encoding="utf-8-sig"))
    if any(t["name"]=="Unclean recovery note" for t in project["tracks"]):snapshot=file;break
  if snapshot:break
  time.sleep(.1)
 assert snapshot,"Automatic recovery snapshot missing"
 p.kill();p.wait()
 assert not (snapshot.parent/"closed").exists(),"Session unexpectedly marked clean"
 p=launch();init(p)
 tool(p,4,"open_project",{"path":str(snapshot)})
 reopened=tool(p,5,"get_project",{})["project"]
 assert reopened["tracks"][-1]["regions"][0]["notes"][0]["pitch"]==71
 tool(p,6,"save_project",{"path":str(root/"Recovered.slopdaw")})
 print("PASS actual forced application termination leaves recoverable automatic .slopdaw snapshot")
 print("PASS new Windows process reopens the recovery snapshot with MIDI intact")
 (root/"recovery-crash.txt").write_text("PASS forced Windows application termination; automatic snapshot survived; second process restored actual MIDI content.\n")
finally:
 if p.poll() is None:p.kill();p.wait()
