using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace SlopStudio
{
 public sealed class McpServer
 {
  private readonly Func<string,IDictionary<string,object>,object> _dispatch;private readonly JavaScriptSerializer _json=new JavaScriptSerializer{MaxJsonLength=1024*1024};private bool _initialized;
  public McpServer(Func<string,IDictionary<string,object>,object> dispatch){_dispatch=dispatch;}
  private static object Schema(params string[] fields){var properties=new Dictionary<string,object>();foreach(string field in fields){string[] parts=field.Split(':');properties[parts[0]]=new Dictionary<string,object>{{"type",parts.Length>1?parts[1]:"string"}};}return new Dictionary<string,object>{{"type","object"},{"properties",properties},{"additionalProperties",false}};}
  public static object Tools(){var list=new List<object>();Action<string,string,object> add=delegate(string name,string description,object schema){list.Add(new {name=name,description=description,inputSchema=schema});};add("get_project","Get current .slopdaw project, tracks, regions, MIDI and supported capabilities.",Schema());add("create_track","Create an audio or MIDI track using the same project command history as the GUI.",Schema("name:string","type:string"));add("add_midi_note","Insert a note in a MIDI region. Missing regionId creates a region.",Schema("trackId:string","regionId:string","start:number","duration:number","pitch:integer","velocity:integer"));add("transport","Start, pause, stop or seek the real audio transport.",Schema("action:string","beat:number"));add("set_tempo","Set project tempo in BPM.",Schema("bpm:number"));add("set_track_mix","Set gain, pan, mute, solo, arm.",Schema("trackId:string","gainDb:number","pan:number","mute:boolean","solo:boolean","arm:boolean"));add("save_project","Save project and actual plug-in state to an absolute .slopdaw path.",Schema("path:string"));add("open_project","Open an existing .slopdaw after current changes have been saved.",Schema("path:string"));add("export_audio","Render the actual project to stereo WAV.",Schema("path:string","start:number","end:number","sampleRate:integer"));add("launch_cell","Queue a real launcher cell on a quantized beat boundary.",Schema("trackId:string","regionId:string","quantum:number"));add("set_automation","Set one gain/pan/mute automation point in project history.",Schema("trackId:string","target:string","beat:number","value:number"));return new {tools=list};}
  public object Handle(IDictionary<string,object> request){
   object id=request.ContainsKey("id")?request["id"]:null;string method=request.ContainsKey("method")?Convert.ToString(request["method"]):"";if(!request.ContainsKey("jsonrpc")||Convert.ToString(request["jsonrpc"])!="2.0")return Error(id,-32600,"Invalid JSON-RPC request.");if(id==null){if(method=="notifications/initialized")_initialized=true;return null;}
   try{object result;switch(method){case "initialize":result=new {protocolVersion="2025-11-25",capabilities=new {tools=new {listChanged=false}},serverInfo=new {name="Slop Studio",version="0.10.0"},instructions="This process owns a visible Windows DAW session. Save changes before opening another project. Unsupported instruments are never substituted."};break;case "ping":result=new object();break;case "tools/list":if(!_initialized)throw new InvalidOperationException("Initialize the session first.");result=Tools();break;case "tools/call":if(!_initialized)throw new InvalidOperationException("Initialize the session first.");var parameters=(IDictionary<string,object>)request["params"];string name=Convert.ToString(parameters["name"]);var args=parameters.ContainsKey("arguments")?(IDictionary<string,object>)parameters["arguments"]:new Dictionary<string,object>();try{object value=_dispatch(name,args);result=new {content=new[]{new {type="text",text=_json.Serialize(value)}},isError=false};}catch(Exception ex){result=new {content=new[]{new {type="text",text=ex.Message}},isError=true};}break;default:return Error(id,-32601,"Method not found: "+method);}
   return new Dictionary<string,object>{{"jsonrpc","2.0"},{"id",id},{"result",result}};
   }catch(Exception ex){return Error(id,-32602,ex.Message);}
  }
  private static object Error(object id,int code,string message){return new Dictionary<string,object>{{"jsonrpc","2.0"},{"id",id},{"error",new {code=code,message=message}}};}
  public void Run(Stream input,Stream output){
   using(var reader=new StreamReader(input,new UTF8Encoding(false,true)))using(var writer=new StreamWriter(output,new UTF8Encoding(false)){AutoFlush=true}){
    while(true){var text=new StringBuilder();int c;while((c=reader.Read())>=0&&c!='\n'){if(c!='\r')text.Append((char)c);if(text.Length>1024*1024)throw new InvalidDataException("MCP line exceeds 1 MiB.");}if(c<0&&text.Length==0)break;if(text.Length==0)continue;object response;try{response=Handle((IDictionary<string,object>)_json.DeserializeObject(text.ToString()));}catch(Exception ex){response=Error(null,-32700,ex.Message);}if(response!=null)writer.WriteLine(_json.Serialize(response));}
   }
  }
 }
}
