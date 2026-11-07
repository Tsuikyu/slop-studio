using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace SlopStudio
{
 public sealed class OscMessage{public string Address;public object[] Arguments;}
 public static class OscCodec
 {
  private static void String(BinaryWriter w,string value){byte[] b=Encoding.UTF8.GetBytes(value);w.Write(b);w.Write((byte)0);while(w.BaseStream.Position%4!=0)w.Write((byte)0);}
  private static void Int(BinaryWriter w,int value){w.Write(IPAddress.HostToNetworkOrder(value));}
  public static byte[] Encode(string address,params object[] args){if(!address.StartsWith("/"))throw new InvalidDataException("OSC address must start with '/'.");using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){String(w,address);string tags=",";foreach(var a in args)tags+=a is bool?((bool)a?"T":"F"):a is int?"i":a is string?"s":"f";String(w,tags);foreach(var a in args){if(a is int)Int(w,(int)a);else if(a is string)String(w,(string)a);else if(!(a is bool))Int(w,BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(a)),0));}return m.ToArray();}}
  private static string ReadString(BinaryReader r){long start=r.BaseStream.Position;var bytes=new List<byte>();byte b;while((b=r.ReadByte())!=0){if(bytes.Count>=4096)throw new InvalidDataException("OSC string too long.");bytes.Add(b);}while((r.BaseStream.Position-start)%4!=0)if(r.ReadByte()!=0)throw new InvalidDataException("OSC padding invalid.");return new UTF8Encoding(false,true).GetString(bytes.ToArray());}
  private static int ReadInt(BinaryReader r){return IPAddress.NetworkToHostOrder(r.ReadInt32());}
  public static List<OscMessage> Decode(byte[] packet){var messages=new List<OscMessage>();Decode(packet,messages,0);return messages;}
  private static void Decode(byte[] packet,List<OscMessage> messages,int depth){if(depth>8||packet.Length>65507)throw new InvalidDataException("OSC packet limits exceeded.");using(var m=new MemoryStream(packet))using(var r=new BinaryReader(m)){string address=ReadString(r);if(address=="#bundle"){r.ReadInt64();while(m.Position<m.Length){int size=ReadInt(r);if(size<4||size>m.Length-m.Position)throw new InvalidDataException("Invalid OSC bundle size.");Decode(r.ReadBytes(size),messages,depth+1);}return;}if(!address.StartsWith("/"))throw new InvalidDataException("Invalid OSC address.");string tags=ReadString(r);if(!tags.StartsWith(","))throw new InvalidDataException("Invalid OSC type tags.");var args=new List<object>();foreach(char tag in tags.Substring(1)){switch(tag){case 'i':args.Add(ReadInt(r));break;case 'f':args.Add(BitConverter.ToSingle(BitConverter.GetBytes(ReadInt(r)),0));break;case 's':args.Add(ReadString(r));break;case 'T':args.Add(true);break;case 'F':args.Add(false);break;default:throw new InvalidDataException("Unsupported OSC tag: "+tag);}}messages.Add(new OscMessage{Address=address,Arguments=args.ToArray()});}}
 }
 public sealed class OscConnection:IDisposable
 {
  private UdpClient _receive,_send;private Thread _thread;private volatile bool _closed;private IPEndPoint _destination;public event Action<OscMessage> Message;public event Action<Exception> Failed;
  public OscConnection(IntegrationSettings settings){IPAddress address;if(String.Equals(settings.OscHost,"localhost",StringComparison.OrdinalIgnoreCase))address=IPAddress.Loopback;else if(!IPAddress.TryParse(settings.OscHost,out address)){var addresses=Dns.GetHostAddresses(settings.OscHost);address=Array.Find(addresses,x=>x.AddressFamily==AddressFamily.InterNetwork)??addresses[0];}_destination=new IPEndPoint(address,settings.OscSendPort);_receive=new UdpClient(new IPEndPoint(IPAddress.Loopback,settings.OscReceivePort));_send=new UdpClient(_destination.AddressFamily);_thread=new Thread(delegate(){while(!_closed){try{IPEndPoint remote=null;byte[] data=_receive.Receive(ref remote);foreach(var msg in OscCodec.Decode(data)){var h=Message;if(h!=null)h(msg);}}catch(SocketException){if(!_closed&&Failed!=null)Failed(new IOException("OSC receive socket failed."));}catch(ObjectDisposedException){break;}catch(Exception ex){if(Failed!=null)Failed(ex);}}}){IsBackground=true,Name="OSC receive"};_thread.Start();}
  public void Send(string address,params object[] values){byte[] bytes=OscCodec.Encode(address,values);_send.Send(bytes,bytes.Length,_destination);}
  public void Dispose(){_closed=true;if(_receive!=null)_receive.Close();if(_send!=null)_send.Close();if(_thread!=null)_thread.Join(1000);}
 }
}
