local M={};local V=require('core.volume')
function M.pack(im,rect,mask)
  local w,h=rect.width,rect.height;V.check(w,h,1)
  local rows={};local raw,stride
  if not mask then local ok,b,s,p=pcall(function() return im.bytes,im.rowStride,im.bytesPerPixel end);if ok and p==4 and #b==s*im.height then raw,stride=b,s end end
  for y=0,h-1 do
    if raw then local i=(rect.y+y)*stride+rect.x*4+1;rows[y+1]=raw:sub(i,i+w*4-1)
    else local row={};for x=0,w-1 do local u,v=rect.x+x,rect.y+y;row[x+1]=string.pack('<I4',(not mask or mask:contains(Point(u,v))) and im:getPixel(u,v) or 0) end;rows[y+1]=table.concat(row) end
  end
  return {w=w,h=h,rgba=table.concat(rows)}
end
function M.image(buf)
  local im=Image(buf.w,buf.h,ColorMode.RGB)
  local ok=pcall(function() local stride=im.rowStride;assert(im.bytesPerPixel==4 and stride>=buf.w*4);local rows={};for y=0,buf.h-1 do rows[y+1]=buf.rgba:sub(y*buf.w*4+1,(y+1)*buf.w*4)..string.rep('\0',stride-buf.w*4) end;im.bytes=table.concat(rows) end)
  if not ok then for y=0,buf.h-1 do for x=0,buf.w-1 do im:drawPixel(x,y,(string.unpack('<I4',buf.rgba,(y*buf.w+x)*4+1))) end end end
  return im
end
function M.capture(sprite,frame,mode,useSelection)
  assert(sprite,'E_NO_SOURCE: open a sprite')
  if mode~='Six views (6 frames)' then
    assert(frame and frame>=1 and frame<=#sprite.frames,'E_NO_SOURCE: choose a valid frame')
  end
  -- Restrict host formats until compositing across color modes is verified.
  assert(sprite.colorMode==ColorMode.RGB,'E_LAYOUT: prototype requires RGB (Sprite > Color Mode > RGB)')
  local w,h=sprite.width,sprite.height;V.check(w,h,1)
  if mode=='Six views (6 frames)' then
    assert(#sprite.frames==6,'E_LAYOUT: expected exactly six frames (Front, Right, Back, Left, Top, Bottom)')
    local sheetWidth=6*w
    V.check(sheetWidth,h,1)
    local sheet=Image(sheetWidth,h,ColorMode.RGB)
    for i=1,6 do
      sheet:drawSprite(sprite,i,Point((i-1)*w,0))
    end
    return M.pack(sheet,Rectangle(0,0,sheetWidth,h))
  end
  if mode=='Layer stack' then
    local layers={};for _,layer in ipairs(sprite.layers) do
      if layer.isVisible then assert(not layer.isGroup and not layer.isTilemap and not layer.isReference,'E_UNSUPPORTED_LAYER: use visible flat image layers');assert(layer.opacity==255 and layer.blendMode==BlendMode.NORMAL,'E_UNSUPPORTED_LAYER: stack requires normal, opaque layers');layers[#layers+1]=layer end
    end
    table.sort(layers,function(a,b) return a.stackIndex<b.stackIndex end);V.check(w,#layers,h);local out={}
    for _,layer in ipairs(layers) do local im=Image(w,h,ColorMode.RGB);local cel=layer:cel(frame);if cel then assert(cel.opacity==255,'E_UNSUPPORTED_LAYER: stack cel opacity must be 255');im:drawImage(cel.image,cel.position) end;out[#out+1]=M.pack(im,Rectangle(0,0,w,h)) end
    return out
  end
  local im=Image(w,h,ColorMode.RGB);im:drawSprite(sprite,frame)
  local selection=useSelection and sprite.selection or nil
  if selection and not selection.isEmpty then return M.pack(im,selection.bounds,selection) end
  return M.pack(im,Rectangle(0,0,w,h))
end
function M.bake(buffers,fps,name)
  local first=buffers[1];local sprite=Sprite(first.w,first.h,ColorMode.RGB)
  local ok,err=pcall(function() app.transaction('Pixel2Voxel output',function()
    local layer=sprite.layers[1];layer.name='Pixel2Voxel'
    for i,buf in ipairs(buffers) do local frame=i==1 and sprite.frames[1] or sprite:newEmptyFrame(i);frame.duration=1/fps;sprite:newCel(layer,frame,M.image(buf),Point(0,0)) end
    if #buffers>1 then sprite:newTag(1,#buffers).name=name end
  end) end)
  if not ok then pcall(function() sprite:close() end);error(err,0) end
  app.refresh();return sprite
end
function M.stackCopy(v)
  assert(not v.directional,'Six-view directional colors cannot round-trip through plain layers')
  assert(v.h<=128,'E_LIMIT_OUTPUT: maximum 128 stack layers')
  local sprite=Sprite(v.w,v.d,ColorMode.RGB);local ok,err=pcall(function() app.transaction('Pixel2Voxel editable stack',function()
    for y=0,v.h-1 do local layer=y==0 and sprite.layers[1] or sprite:newLayer();layer.name=string.format('Slice %03d',y);local rows={};for z=0,v.d-1 do local row={};for x=0,v.w-1 do row[x+1]=string.pack('<I4',V.has(v,x,y,z) and v.sample(x,y,z,1) or 0) end;rows[z+1]=table.concat(row) end;sprite:newCel(layer,1,M.image{w=v.w,h=v.d,rgba=table.concat(rows)},Point(0,0)) end
  end) end);if not ok then pcall(function() sprite:close() end);error(err,0) end;app.refresh();return sprite
end
return M
