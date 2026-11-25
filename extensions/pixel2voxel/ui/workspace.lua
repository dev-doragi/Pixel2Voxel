local V=require('core.volume');local G=require('core.generate');local R=require('render.raster');local H=require('host.images');local M={}
local modes={'Extrude','Layer stack','Six views','Six views (6 frames)'}
local names={'Front','Right','Back','Left','Top','Bottom'}
local function clamp(x,a,b) return math.max(a,math.min(b,x)) end
local function errmsg(e) return tostring(e):gsub('^.-:%d+: ','') end
function M.open(plugin,onclose)
  local source,frame=app.activeSprite,app.activeFrame and app.activeFrame.frameNumber or 1
  local dlg,timer,listener,eventSource,job,jobDone,jobKind,volume,faces,preview,buffer
  local closed,valid,dirty,live=false,false,false,false
  local align=G.defaultAlignment();local yaw,pitch=-45,-30;local zoom,panX,panY=0,0,0;local drag
  local debounce=0;local pendingRender=false;local prefs=plugin.preferences
  local previewSpan,fitScale=1,1
  local light={azimuth=-45,elevation=45,r=255,g=255,b=255}
  local function status(s) if not closed then dlg:modify{id='status',text=s} end end
  local function buttons() local ready=valid and not job and not dirty and not pendingRender;dlg:modify{id='bake',enabled=ready};dlg:modify{id='stackcopy',enabled=ready and volume and not volume.directional or false};dlg:modify{id='cancel',enabled=job~=nil} end
  local function cancel() job,jobDone,jobKind=nil,nil,nil;pendingRender=false end
  local function fail(e) valid=false;status(errmsg(e));buttons();dlg:repaint() end
  local function detach() if listener then pcall(function() eventSource:off(listener) end);listener,eventSource=nil,nil end end
  local function alive() for _,s in ipairs(app.sprites) do if s==source then return true end end;return false end
  local function markDirty() if jobKind=='bake' then cancel() end;dirty=true;debounce=5;valid=false;status('Source changed - refresh required');buttons() end
  local function attach() detach();if source then local ok,id=pcall(function() eventSource=source.events;return eventSource:on('change',markDirty) end);if ok then listener=id end end end
  local function label()
    local name=source and app.fs.fileName(source.filename) or 'No source'
    if name=='' then name='Untitled' end
    if source then
      name=name..(dlg.data.mode==modes[4] and ' | frames 1-6' or ' | frame '..frame)
    end
    dlg:modify{id='source',text=name}
  end
  local function config() local d=dlg.data;return {mode=d.mode,depth=d.depth,align=align,density=tonumber(d.density),lit=d.lighting and light,selection=d.selection} end
  local function begin(kind,fn,done)
    cancel();jobKind=kind;local deadline
    job=coroutine.create(function() return fn(function() if os.clock()>=deadline then coroutine.yield() end end) end)
    jobDone=done;status(kind=='bake' and 'Baking sprites...' or kind=='build' and 'Building voxels...' or 'Rendering...');buttons()
    return function() deadline=os.clock()+0.009 end
  end
  local setDeadline
  local function display(buf)
    previewSpan=buf.previewSpan
    buffer=buf;preview=H.image(buf);valid=true;local clip=volume.clipped or 0;status(string.format('%d x %d x %d | %d voxels%s',volume.w,volume.h,volume.d,volume.count,clip>0 and (' | '..clip..' clipped pixels') or ''));dlg:repaint();buttons()
  end
  local function render()
    if not volume or not faces or dirty then return end
    local c=config();local view={yaw=yaw,pitch=pitch};local v,f=volume,faces;valid=false
    setDeadline=begin('render',function(step)
      local layout=R.layout(v,{view},c.density)
      local buf=R.render(v,f,view,layout,c.lit,step)
      buf.previewSpan=math.ceil(math.sqrt(v.w*v.w+v.h*v.h+v.d*v.d)*c.density)+6
      return buf
    end,display)
  end
  local function build()
    cancel();dirty=false;valid=false;volume,faces=nil,nil;buttons()
    if not alive() then source=nil;detach();label();return fail('Open a sprite, then click Use active.') end
    local c=config();local ok,snap=pcall(H.capture,source,frame,c.mode,c.selection and c.mode~='Layer stack');if not ok then return fail(snap) end
    local view={yaw=yaw,pitch=pitch};setDeadline=begin('build',function(step)
      local v;if c.mode=='Extrude' then v=G.extrude(snap,c,step) elseif c.mode=='Layer stack' then v=G.stack(snap,c,step) else v=G.hull(snap,c,step) end
      local ok,f=pcall(V.surface,v,step);return {v=v,f=ok and f or nil,err=not ok and f or nil}
    end,function(result) volume,faces=result.v,result.f;if result.err then fail(result.err) else render() end end)
  end
  local function pose(a,b)
    if jobKind=='bake' then return end
    a=(a+180)%360-180
    b=clamp(b,-90,90)
    if dlg.data.snap then a,b=R.snapToFace(a,b,dlg.data.snapAngle) end
    yaw,pitch=a,b
    if jobKind=='build' then return end
    pendingRender=true;valid=false;buttons()
  end
  local function fit() zoom,panX,panY=0,0,0;dlg:repaint() end
  local function lightingDialog()
    local d=Dialog{title='Directional lighting',parent=dlg}
    d:slider{id='azimuth',label='Azimuth',min=-180,max=180,value=light.azimuth}
    d:slider{id='elevation',label='Elevation',min=-90,max=90,value=light.elevation}
    d:color{id='color',label='Light color',color=Color{r=light.r,g=light.g,b=light.b}}
    d:button{text='Cancel'}
    d:button{text='Apply',onclick=function()
      local settings=d.data
      local color=settings.color
      light={azimuth=settings.azimuth,elevation=settings.elevation,r=color.red,g=color.green,b=color.blue}
      d:close()
      dlg:modify{id='lighting',selected=true}
      pose(yaw,pitch)
    end}
    d:show()
  end
  local function alignDialog()
    local draft={};for i,a in ipairs(align) do draft[i]={slot=a.slot,dx=a.dx,dy=a.dy,fh=a.fh,fv=a.fv} end
    local d=Dialog{title='Align six views',parent=dlg};local selected=1
    local function sync() local a=draft[selected];d:modify{id='slot',option=tostring(a.slot)};d:modify{id='dx',text=tostring(a.dx)};d:modify{id='dy',text=tostring(a.dy)};d:modify{id='fh',selected=a.fh};d:modify{id='fv',selected=a.fv} end
    local function save() local a=draft[selected];a.dx=math.floor(d.data.dx or 0);a.dy=math.floor(d.data.dy or 0);a.fh=d.data.fh;a.fv=d.data.fv end
    d:label{text='Slot order: F / R / B / L / T / D'}
    d:combobox{id='face',label='Face',options=names,option=names[1],onchange=function() save();for i,n in ipairs(names) do if n==d.data.face then selected=i end end;sync() end}
    d:combobox{id='slot',label='Slot',options={'1','2','3','4','5','6'},option=tostring(draft[1].slot),onchange=function() local a=draft[selected];local slot=tonumber(d.data.slot);for i,b in ipairs(draft) do if i~=selected and b.slot==slot then b.slot=a.slot;break end end;a.slot=slot end}
    d:number{id='dx',label='Offset X / Y',text=tostring(draft[1].dx),decimals=0};d:number{id='dy',text=tostring(draft[1].dy),decimals=0}
    d:check{id='fh',text='Flip H',selected=draft[1].fh};d:check{id='fv',text='Flip V',selected=draft[1].fv}
    d:button{text='Reset',onclick=function() draft=G.defaultAlignment();sync() end};d:button{text='Cancel'};d:button{text='Apply',onclick=function() save();align=draft;d:close();build() end};d:show()
  end
  local function bakeDialog()
    if not valid or job or dirty then return end
    local d=Dialog{title='Bake pixel art',parent=dlg};d:combobox{id='type',label='Output',options={'Current view','4 directions','8 directions','16 directions','Turntable'},option='Current view'}
    d:slider{id='count',label='Turntable frames',min=4,max=64,value=16};d:slider{id='fps',label='FPS',min=1,max=60,value=12};d:label{text='Creates a new RGB sprite. Original stays untouched.'};d:button{text='Cancel'}
    d:button{text='Bake',onclick=function()
      local b=d.data;d:close();if not valid or dirty then return end;local c=config();local v,f=volume,faces;local views={};local n=tonumber(b.type:match('^%d+')) or (b.type=='Turntable' and b.count or 1);local fps=b.fps
      if n==1 then local ok,e=pcall(H.bake,{buffer},fps,'view');if not ok then fail(e) end;return end
      for i=0,n-1 do views[#views+1]={yaw=(b.type=='Turntable' and yaw or 0)+360*i/n,pitch=pitch} end
      setDeadline=begin('bake',function(step) local layout=R.layout(v,views,c.density);assert(layout.w*layout.h*n<=4194304,'E_LIMIT_OUTPUT: too many output pixels');local out={};for i,view in ipairs(views) do out[i]=R.render(v,f,view,layout,c.lit,step) end;return out end,function(out) H.bake(out,fps,b.type=='Turntable' and 'rotation' or 'directions');status('Baked '..#out..' frames');buttons() end)
    end};d:show()
  end
  local function cleanup() if closed then return end;closed=true;cancel();detach();if timer then timer:stop() end;pcall(function() prefs.depth=dlg.data.depth;prefs.density=dlg.data.density;prefs.lighting=dlg.data.lighting;local b=dlg.bounds;prefs.w=b.width;prefs.h=b.height;prefs.x=b.x;prefs.y=b.y end);onclose() end
  dlg=Dialog{title='Pixel2Voxel | Prototype',resizeable=true,onclose=cleanup}
  dlg:label{id='source',label='Source',text='No source'};dlg:button{text='Use active',onclick=function() source=app.activeSprite;frame=app.activeFrame and app.activeFrame.frameNumber or 1;align=G.defaultAlignment();attach();label();build() end};dlg:button{text='Refresh',onclick=build}
  dlg:combobox{id='mode',label='Method',options=modes,option=modes[1],onchange=function()
    local m=dlg.data.mode
    dlg:modify{id='depth',visible=m=='Extrude'}
    dlg:modify{id='align',visible=m==modes[3] or m==modes[4]}
    dlg:modify{id='selection',visible=m~='Layer stack' and m~=modes[4]}
    label()
    build()
  end}
  dlg:slider{id='depth',label='Depth',min=1,max=64,value=clamp(tonumber(prefs.depth) or 3,1,64),onchange=function() markDirty();debounce=3;dirty='settings' end}
  dlg:button{id='align',text='Align views...',visible=false,onclick=alignDialog};dlg:check{id='selection',text='Selection only',selected=false,onclick=build}
  dlg:canvas{id='viewport',width=440,height=260,onpaint=function(ev)
    local gc=ev.context;local w,h=gc.width,gc.height;gc.color=Color{r=39,g=42,b=49,a=255};gc:fillRect(Rectangle(0,0,w,h));gc.color=Color{r=46,g=50,b=58,a=255}
    for y=0,h-1,24 do for x=0,w-1,24 do if (x//24+y//24)%2==0 then gc:fillRect(Rectangle(x,y,math.min(24,w-x),math.min(24,h-y))) end end end
    if preview then
      fitScale=math.max(1,math.min(w,h)-16)/previewSpan
      if fitScale>=1 then fitScale=math.floor(fitScale) end
      local k=zoom==0 and fitScale or zoom
      local dw,dh=math.max(1,math.floor(preview.width*k)),math.max(1,math.floor(preview.height*k))
      gc:drawImage(preview,0,0,preview.width,preview.height,math.floor((w-dw)/2+panX),math.floor((h-dh)/2+panY),dw,dh)
    end
    gc.color=Color{r=205,g=213,b=222,a=255};gc:fillText(string.format('Yaw %d  Pitch %d',math.floor(yaw),math.floor(pitch)),8,7);if not valid and preview then gc:fillText('Previous preview',8,h-20) end
  end,onmousedown=function(e)
    drag={x=e.x,y=e.y,yaw=yaw,pitch=pitch,pan=e.button==MouseButton.MIDDLE or e.shiftKey}
  end,onmouseup=function() drag=nil end,onmousemove=function(e)
    if not drag then return end
    local dx,dy=e.x-drag.x,e.y-drag.y
    drag.x,drag.y=e.x,e.y
    if drag.pan then
      panX,panY=panX+dx,panY+dy
      dlg:repaint()
    else
      drag.yaw=(drag.yaw-dx*0.7+180)%360-180
      drag.pitch=clamp(drag.pitch-dy*0.7,-90,90)
      pose(drag.yaw,drag.pitch)
    end
  end,onwheel=function(e)
    if not preview or e.deltaY==0 then return end
    local k=zoom==0 and fitScale or zoom
    zoom=clamp(k*(e.deltaY<0 and 1.25 or 0.8),math.min(0.125,fitScale),math.max(32,fitScale))
    dlg:repaint()
  end,ondblclick=fit}
  dlg:button{text='Front',onclick=function() pose(0,0) end};dlg:button{text='Right',onclick=function() pose(90,0) end};dlg:button{text='Back',onclick=function() pose(180,0) end};dlg:button{text='Left',onclick=function() pose(-90,0) end}
  dlg:button{text='Top',onclick=function() pose(0,-90) end};dlg:button{text='Bottom',onclick=function() pose(0,90) end};dlg:button{text='ISO',onclick=function() pose(-45,-30) end};dlg:button{text='Fit',onclick=fit}
  dlg:check{id='snap',text='Snap to sheet faces',selected=true,onclick=function() pose(yaw,pitch) end}
  dlg:slider{id='snapAngle',label='Snap angle',min=1,max=30,value=2,onchange=function() pose(yaw,pitch) end}
  dlg:combobox{id='density',label='Render pixels / voxel',options={'1','2','4'},option=({['1']=true,['2']=true,['4']=true})[prefs.density] and prefs.density or '2',onchange=function() pose(yaw,pitch) end};dlg:check{id='lighting',text='Shading',selected=prefs.lighting==true,onclick=function() pose(yaw,pitch) end}
  dlg:button{text='Light settings...',onclick=lightingDialog}
  dlg:check{id='live',text='Live (experimental)',selected=false,onclick=function() live=dlg.data.live;if live and dirty then debounce=1 end end}
  dlg:label{id='status',text='Open a sprite to begin.'};dlg:button{id='stackcopy',text='Editable stack',enabled=false,onclick=function() local ok,e=pcall(H.stackCopy,volume);if not ok then fail(e) end end};dlg:button{id='cancel',text='Cancel job',enabled=false,onclick=function() cancel();valid=false;status('Cancelled - Refresh to rebuild');buttons() end};dlg:button{id='bake',text='Bake sprite...',enabled=false,onclick=bakeDialog}
  dlg:show{wait=false,autoscrollbars=true};if type(prefs.w)=='number' and type(prefs.h)=='number' then pcall(function() local b=dlg.bounds;dlg.bounds=Rectangle(b.x,b.y,clamp(prefs.w,320,1200),clamp(prefs.h,300,1000)) end) end;dlg:modify{id='align',visible=false};label();attach()
  timer=Timer{interval=0.035,ontick=function()
    if closed then return end
    if source and not alive() then source=nil;detach();cancel();dirty=true;valid=false;label();status('Source closed - Use active');buttons();return end
    if dirty and (live or dirty=='settings') then debounce=debounce-1;if debounce<=0 then build() end end
    if pendingRender and not dirty and jobKind~='build' and jobKind~='bake' then pendingRender=false;render() end
    if job then local current,done=job,jobDone;setDeadline();local ok,result=coroutine.resume(current);if not ok then cancel();fail(result) elseif coroutine.status(current)=='dead' then cancel();if dirty then buttons() else local success,e=pcall(done,result);if not success then fail(e) end end end end
  end};timer:start();if source then build() else buttons() end
  return {close=function() if not closed then dlg:close();cleanup() end end}
end
return M
