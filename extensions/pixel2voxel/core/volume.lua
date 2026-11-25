local M={MAX=1048576,MAX_FACES=500000}
function M.check(w,h,d)
  assert(w>0 and h>0 and d>0 and w*h*d<=M.MAX,'E_LIMIT_VOLUME: maximum 1,048,576 candidate cells')
end
function M.new(w,h,d,sample)
  M.check(w,h,d)
  return {w=w,h=h,d=d,words={},count=0,sample=sample}
end
function M.index(v,x,y,z) return (z*v.h+y)*v.w+x end
function M.xyz(v,i) return i%v.w,(i//v.w)%v.h,i//(v.w*v.h) end
function M.has(v,x,y,z)
  if x<0 or x>=v.w or y<0 or y>=v.h or z<0 or z>=v.d then return false end
  local i=M.index(v,x,y,z); return ((v.words[i//32] or 0)&(1<<(i%32)))~=0
end
function M.set(v,x,y,z)
  local i=M.index(v,x,y,z); local k,b=i//32,1<<(i%32); local old=v.words[k] or 0
  if old&b==0 then v.words[k]=old|b; v.count=v.count+1 end
end
M.normals={{0,0,1},{1,0,0},{0,0,-1},{-1,0,0},{0,1,0},{0,-1,0}}
function M.surface(v,step)
  local faces={}; local n=0
  for i=0,v.w*v.h*v.d-1 do
    local x,y,z=M.xyz(v,i)
    if M.has(v,x,y,z) then
      for f,k in ipairs(M.normals) do
        if not M.has(v,x+k[1],y+k[2],z+k[3]) then
          n=n+1; assert(n<=M.MAX_FACES,'E_LIMIT_SURFACE: too many exposed faces')
          faces[n]=i*8+f
        end
      end
    end
    if step and i%1024==0 then step() end
  end
  return faces
end
return M
