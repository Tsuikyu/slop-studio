"""Original Slop Studio DJ/cat-ear robot silhouette, no face or proprietary assets."""
import math,struct,zlib,pathlib
root=pathlib.Path(__file__).resolve().parents[1]/"resources";root.mkdir(parents=True,exist_ok=True)
poly=[(68,104),(61,39),(109,76),(147,76),(195,39),(188,104),(188,158),(175,184),(148,196),(108,196),(81,184),(68,158)]
def inside(x,y):
    result=False
    for i,(ax,ay) in enumerate(poly):
        bx,by=poly[i-1]
        if (ay>y)!=(by>y) and x<(bx-ax)*(y-ay)/(by-ay)+ax:result=not result
    return result
def color(x,y):
    if x<4 or x>=252 or y<4 or y>=252:return (0,0,0,0)
    cx=max(56,min(200,x));cy=max(56,min(200,y))
    if (x-cx)**2+(y-cy)**2>52**2:return (0,0,0,0)
    c=(23,28,38,255)
    if inside(x,y):
        t=max(0,min(1,(y-35)/170));c=(round(177-114*t),round(221-99*t),round(247-76*t),255)
    # Outside headphone arch, with no eyes or facial details.
    a=math.atan2((y-139.5)/77.5,(x-128)/86)
    d=math.sqrt(((x-128)/86)**2+((y-139.5)/77.5)**2)
    if y<110 and .93<d<1.07:c=(116,186,224,255)
    if (39<=x<61 or 195<=x<217) and 113<=y<167:c=(108,175,216,255)
    if 112<=x<144 and 197<=y<212:c=(108,175,216,255)
    if 88<=x<168 and 212<=y<225:c=(79,132,175,255)
    return c
def png(n):
    raw=bytearray()
    for y in range(n):
        raw.append(0)
        for x in range(n):
            samples=[color((x+(sx+.5)/3)*256/n,(y+(sy+.5)/3)*256/n) for sy in range(3) for sx in range(3)]
            for ch in range(4):raw.append(round(sum(p[ch] for p in samples)/9))
    def chunk(kind,data):return struct.pack(">I",len(data))+kind+data+struct.pack(">I",zlib.crc32(kind+data)&0xffffffff)
    return b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",n,n,8,6,0,0,0))+chunk(b"IDAT",zlib.compress(bytes(raw),9))+chunk(b"IEND",b"")
sizes=[16,24,32,48,64,128,256];images=[png(n) for n in sizes];offset=6+16*len(sizes)
header=struct.pack("<HHH",0,1,len(sizes));entries=bytearray()
for n,image in zip(sizes,images):
    entries+=struct.pack("<BBBBHHII",n%256,n%256,0,0,1,32,len(image),offset);offset+=len(image)
(root/"SlopStudio.ico").write_bytes(header+entries+b"".join(images))
(root/"slop-robot.png").write_bytes(images[-1])
print("Original 64-bit application icon: 7 PNG resolutions, 16..256 px.")
