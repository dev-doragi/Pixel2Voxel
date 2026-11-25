local M={}; local V=require('core.volume')
local function pixel(im,u,v)
  if u<0 or v<0 or u>=im.w or v>=im.h then return 0 end
  return string.unpack('<I4',im.rgba,(v*im.w+u)*4+1)
end
M.pixel=pixel
local function alpha(c) return (c>>24)&255 end
local function validate(im,step)
  assert(#im.rgba==im.w*im.h*4,'E_LAYOUT: invalid pixel buffer')
  local n=0
  for i=4,#im.rgba,4 do local a=im.rgba:byte(i); assert(a==0 or a==255,'E_ALPHA: use fully opaque or transparent pixels'); if a==255 then n=n+1 end; if step and i%4096==0 then step() end end
  return n
end
local function bounds(im)
  local l,t,r,b=im.w,im.h,-1,-1
  for y=0,im.h-1 do for x=0,im.w-1 do if alpha(pixel(im,x,y))==255 then l=math.min(l,x);r=math.max(r,x);t=math.min(t,y);b=math.max(b,y) end end end
  return l,t,r,b
end
function M.extrude(im,c,step)
  assert(validate(im,step)>0,'E_EMPTY_MODEL: source is empty')
  local d=c.depth or 3; assert(d>=1 and d<=64 and d%1==0,'E_LAYOUT: depth must be 1..64')
  local l,t,r,b=bounds(im); local w,h=r-l+1,b-t+1
  local v=V.new(w,h,d,function(x,y,z,f) return pixel(im,x+l,b-y) end)
  for y=0,h-1 do for x=0,w-1 do if alpha(v.sample(x,y,0))==255 then for z=0,d-1 do V.set(v,x,y,z) end end; if step and x%64==0 then step() end end end
  return v
end
function M.stack(images,c,step)
  assert(#images>0,'E_LAYOUT: no visible image layers')
  local w,d=images[1].w,images[1].h; V.check(w,#images,d)
  local v=V.new(w,#images,d,function(x,y,z,f) return pixel(images[y+1],x,z) end)
  for y,im in ipairs(images) do
    assert(im.w==w and im.h==d,'E_LAYOUT: layer sizes differ'); validate(im,step)
    for z=0,d-1 do for x=0,w-1 do if alpha(pixel(im,x,z))==255 then V.set(v,x,y-1,z) end end; if step then step() end end
  end
  assert(v.count>0,'E_EMPTY_MODEL: layers are empty'); return v
end
-- Front, Right, Back, Left, Top, Bottom; axes X,Y,Z = 1,2,3
local axes={{1,2,false,true},{3,2,true,true},{1,2,true,true},{3,2,false,true},{1,3,false,false},{1,3,false,true}}
function M.defaultAlignment() local a={}; for i=1,6 do a[i]={slot=i,dx=0,dy=0,fh=false,fv=false} end; return a end
function M.aligned(im,align,step)
  assert(im.w%6==0,'E_LAYOUT: sheet width must be divisible by 6')
  local w,h=im.w//6,im.h; local faces,seen={},{}; local clipped=0
  validate(im,step)
  for f=1,6 do
    local a=align[f]; assert(a.slot>=1 and a.slot<=6 and not seen[a.slot],'E_DUP_FACE: each slot must be unique');seen[a.slot]=true
    local p={}; for y=0,h-1 do for x=0,w-1 do
      local u,v=x-a.dx,y-a.dy; local col=0
      if u>=0 and u<w and v>=0 and v<h then if a.fh then u=w-1-u end;if a.fv then v=h-1-v end;col=pixel(im,(a.slot-1)*w+u,v) end
      p[#p+1]=string.pack('<I4',col)
    end; if step then step() end end
    local face={w=w,h=h,rgba=table.concat(p)}; local n=validate(face,step); assert(n>0,'E_EMPTY_FACE: face '..f..' is empty')
    local before=0; for y=0,h-1 do for x=0,w-1 do if alpha(pixel(im,(a.slot-1)*w+x,y))==255 then before=before+1 end end end
    clipped=clipped+before-n;faces[f]=face
  end
  return faces,clipped
end
function M.hull(im,c,step)
  local faces,clipped=M.aligned(im,c.align or M.defaultAlignment(),step)
  local lo,hi={math.huge,math.huge,math.huge},{-1,-1,-1}; local w,h=faces[1].w,faces[1].h
  for f,face in ipairs(faces) do local a=axes[f]; for y=0,h-1 do for x=0,w-1 do
    if alpha(pixel(face,x,y))==255 then local u=a[3] and w-1-x or x;local v=a[4] and h-1-y or y;lo[a[1]]=math.min(lo[a[1]],u);hi[a[1]]=math.max(hi[a[1]],u);lo[a[2]]=math.min(lo[a[2]],v);hi[a[2]]=math.max(hi[a[2]],v) end
  end; if step then step() end end end
  local function sample(x,y,z,f)
    local a=axes[f]; local u= a[1]==1 and x+lo[1] or z+lo[3]; local v=a[2]==2 and y+lo[2] or z+lo[3]
    return pixel(faces[f],a[3] and w-1-u or u,a[4] and h-1-v or v)
  end
  local vol=V.new(hi[1]-lo[1]+1,hi[2]-lo[2]+1,hi[3]-lo[3]+1,sample); vol.clipped=clipped;vol.directional=true
  for z=0,vol.d-1 do for y=0,vol.h-1 do for x=0,vol.w-1 do local ok=true; for f=1,6 do if alpha(sample(x,y,z,f))~=255 then ok=false;break end end; if ok then V.set(vol,x,y,z) end end; if step then step() end end end
  assert(vol.count>0,'E_EMPTY_MODEL: silhouettes have no common volume');return vol
end
return M
