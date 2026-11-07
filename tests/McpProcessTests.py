"""Exercise the real stdio server and its default read-only permission boundary."""
import ctypes, json, os, pathlib, queue, subprocess, sys, threading

app = pathlib.Path(sys.argv[1]).resolve()
out = pathlib.Path(sys.argv[2]).resolve()
out.mkdir(parents=True, exist_ok=True)
env = os.environ.copy()
env["SLOPSTUDIO_PROFILE"] = str(out / "profile")
startup = subprocess.STARTUPINFO()
startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
startup.wShowWindow = 0
p = subprocess.Popen([str(app), "--mcp"], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                     stderr=subprocess.PIPE, text=True, encoding="utf-8", env=env,
                     startupinfo=startup, creationflags=subprocess.CREATE_NO_WINDOW)
responses = queue.Queue()
def read_stdout():
    for line in p.stdout:
        responses.put(line)
threading.Thread(target=read_stdout, daemon=True).start()

def send(obj):
    p.stdin.write(json.dumps(obj) + "\n")
    p.stdin.flush()

def request(mid, method, params=None):
    obj = {"jsonrpc": "2.0", "id": mid, "method": method}
    if params is not None:
        obj["params"] = params
    send(obj)
    response = json.loads(responses.get(timeout=20))
    if "error" in response:
        raise RuntimeError(response["error"])
    return response["result"]

def tool(mid, name, args):
    return request(mid, "tools/call", {"name": name, "arguments": args})

try:
    init = request(1, "initialize", {"protocolVersion":"2025-11-25", "capabilities":{},
                                      "clientInfo":{"name":"Permission fixture","version":"1"}})
    send({"jsonrpc":"2.0","method":"notifications/initialized"})
    assert init["serverInfo"]["name"] == "Slop Studio"
    tools = request(2, "tools/list")["tools"]
    assert len(tools) == 11
    before = tool(3, "get_project", {})
    assert not before.get("isError")
    state = json.loads(before["content"][0]["text"])
    denied = tool(4, "create_track", {"name":"Blocked", "type":"midi"})
    assert denied.get("isError") and "disabled" in denied["content"][0]["text"].lower()
    target = out / "blocked.slopdaw"
    denied_file = tool(5, "save_project", {"path":str(target)})
    assert denied_file.get("isError") and not target.exists()
    denied_transport = tool(6, "transport", {"action":"play"})
    assert denied_transport.get("isError")
    after = json.loads(tool(7, "get_project", {})["content"][0]["text"])
    assert len(after["project"]["tracks"]) == len(state["project"]["tracks"])
    lines = ["PASS real MCP stdio initialization and 11 advertised tools",
             "PASS default read-only policy rejects track creation, file writes and transport",
             "PASS denied requests preserve project and do not create a file"]
    (out / "mcp-process.txt").write_text("\n".join(lines)+"\n",encoding="utf-8")
    print("\n".join(lines))
finally:
    if p.poll() is None:
        enum = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
        def close(hwnd, _):
            pid = ctypes.c_ulong()
            ctypes.windll.user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
            if pid.value == p.pid:
                ctypes.windll.user32.PostMessageW(hwnd, 0x0010, 0, 0)
            return True
        ctypes.windll.user32.EnumWindows(enum(close), 0)
        try:
            p.wait(timeout=5)
        except subprocess.TimeoutExpired:
            p.kill()
            p.wait()