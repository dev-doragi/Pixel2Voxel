package.path='extensions/pixel2voxel/?.lua;'..package.path
local G=require('core.generate');local V=require('core.volume');local R=require('render.raster')
-- Original synthetic 15x23 sword artwork, no third-party asset.
local w,h=15,23;local rows={};local colors={steel=0xffdec6a0,light=0xfff9f1db,edge=0xff84766b,guard=0xff479fd9,gold=0xff83d9fa,grip=0xff583d59,wrap=0xff9b708d}
for y=0,h-1 do local row={};for x=0,w-1 do local c=0;local dx=x-7
  if y<=2 and math.abs(dx)<=y then c=dx==0 and colors.light or colors.edge
  elseif y>=3 and y<=15 and math.abs(dx)<=2 then c=dx==-2 or dx==2 and colors.edge or colors.steel;if dx==-2 or dx==2 then c=colors.edge elseif dx==0 then c=colors.light else c=colors.steel end
  elseif y>=16 and y<=17 and math.abs(dx)<=5 then c=y==16 and colors.gold or colors.guard
  elseif y>=18 and y<=21 and math.abs(dx)<=1 then c=y%2==0 and colors.wrap or colors.grip
  elseif y==22 and math.abs(dx)<=2 then c=colors.gold end
  row[x+1]=string.pack('<I4',c)
end;rows[y+1]=table.concat(row) end
local im={w=w,h=h,rgba=table.concat(rows)};local v=G.extrude(im,{depth=4});local f=V.surface(v);local views={};for i=0,7 do views[#views+1]={yaw=i*45,pitch=-30} end;local l=R.layout(v,views,2)
local function save(name,b) local file=assert(io.open(name,'wb'));file:write(string.pack('<I4I4',b.w,b.h),b.rgba);file:close() end
save('/tmp/p2v-demo-input.rgba',im);for i,p in ipairs(views) do save('/tmp/p2v-demo-'..i..'.rgba',R.render(v,f,p,l,true)) end
