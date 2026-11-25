local V=require('core.volume');local M={}
local sin,cos,rad=math.sin,math.cos,math.rad
function M.camera(yaw,pitch)
  local a,b=rad(yaw),rad(pitch);local s,c=sin(a),cos(a);local p,q=sin(b),cos(b)
  return {c,0,-s,-s*p,-q,-c*p,s*q,-p,c*q}
end
-- projection about geometric center; depth larger = nearer
local function project(v,m,x,y,z,k)
  x,y,z=x-v.w/2,y-v.h/2,z-v.d/2
  return (m[1]*x+m[2]*y+m[3]*z)*k,(m[4]*x+m[5]*y+m[6]*z)*k,m[7]*x+m[8]*y+m[9]*z
end
function M.layout(v,views,k)
  assert(k==1 or k==2 or k==4,'invalid render density')
  local l,t,r,b=math.huge,math.huge,-math.huge,-math.huge
  for _,view in ipairs(views) do local m=M.camera(view.yaw,view.pitch)
    for _,x in ipairs({0,v.w}) do for _,y in ipairs({0,v.h}) do for _,z in ipairs({0,v.d}) do local u,s=project(v,m,x,y,z,k);l=math.min(l,u);t=math.min(t,s);r=math.max(r,u);b=math.max(b,s) end end end
  end
  l,t,r,b=math.floor(l)-2,math.floor(t)-2,math.ceil(r)+2,math.ceil(b)+2
  assert(r-l<=1024 and b-t<=1024 and (r-l)*(b-t)<=262144,'E_LIMIT_OUTPUT: reduce render density or model size')
  return {w=r-l,h=b-t,ox=-l,oy=-t,k=k}
end
local corners={{{0,0,1},{1,0,1},{1,1,1},{0,1,1}},{{1,0,1},{1,0,0},{1,1,0},{1,1,1}},{{1,0,0},{0,0,0},{0,1,0},{1,1,0}},{{0,0,0},{0,0,1},{0,1,1},{0,1,0}},{{0,1,1},{1,1,1},{1,1,0},{0,1,0}},{{0,0,0},{1,0,0},{1,0,1},{0,0,1}}}
local light={0.9,0.72,0.66,0.82,1.0,0.55}
local function shade(c,f)
  local k=light[f];return (c&0xff000000)|(math.floor(((c>>16)&255)*k+0.5)<<16)|(math.floor(((c>>8)&255)*k+0.5)<<8)|math.floor((c&255)*k+0.5)
end
local function edge(ax,ay,bx,by,x,y) return (bx-ax)*(y-ay)-(by-ay)*(x-ax) end
local function tri(a,b,c,col,layout,colors,depth,step)
  local ax,ay,az=a[1],a[2],a[3];local bx,by,bz=b[1],b[2],b[3];local cx,cy,cz=c[1],c[2],c[3]
  local area=edge(ax,ay,bx,by,cx,cy);if area==0 then return end
  if area<0 then bx,cx=cx,bx;by,cy=cy,by;bz,cz=cz,bz;area=-area end
  local l=math.max(0,math.ceil((math.min(ax,bx,cx)-128)/256));local r=math.min(layout.w-1,math.floor((math.max(ax,bx,cx)-128)/256))
  local t=math.max(0,math.ceil((math.min(ay,by,cy)-128)/256));local bot=math.min(layout.h-1,math.floor((math.max(ay,by,cy)-128)/256))
  local function topLeft(x1,y1,x2,y2) return y2<y1 or (y2==y1 and x2>x1) end
  local q1,q2,q3=topLeft(bx,by,cx,cy),topLeft(cx,cy,ax,ay),topLeft(ax,ay,bx,by)
  for y=t,bot do for x=l,r do local px,py=x*256+128,y*256+128;local e1=edge(bx,by,cx,cy,px,py);local e2=edge(cx,cy,ax,ay,px,py);local e3=edge(ax,ay,bx,by,px,py)
    if (e1>0 or e1==0 and q1) and (e2>0 or e2==0 and q2) and (e3>0 or e3==0 and q3) then local z=(e1*az+e2*bz+e3*cz)/area;local i=y*layout.w+x+1;if not depth[i] or z>depth[i]+1e-9 then colors[i]=col;depth[i]=z end end
  end end
end
function M.render(v,faces,view,layout,lit,step)
  local m=M.camera(view.yaw,view.pitch);local colors,depth={},{};local p={{},{},{},{}}
  for j,id in ipairs(faces) do local f=id%8;local n=V.normals[f]
    if n[1]*m[7]+n[2]*m[8]+n[3]*m[9]>1e-8 then
      local x,y,z=V.xyz(v,id//8);local col=v.sample(x,y,z,f);if lit then col=shade(col,f) end
      for i,o in ipairs(corners[f]) do local u,s,d=project(v,m,x+o[1],y+o[2],z+o[3],layout.k);p[i][1]=math.floor((u+layout.ox)*256+0.5);p[i][2]=math.floor((s+layout.oy)*256+0.5);p[i][3]=d end
      tri(p[1],p[2],p[3],col,layout,colors,depth);tri(p[1],p[3],p[4],col,layout,colors,depth)
    end
    if step and j%128==0 then step() end
  end
  local rows={};for y=0,layout.h-1 do local row={};for x=1,layout.w do row[x]=string.pack('<I4',colors[y*layout.w+x] or 0) end;rows[y+1]=table.concat(row);if step then step() end end
  return {w=layout.w,h=layout.h,rgba=table.concat(rows)}
end
return M
