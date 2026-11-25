local workspace
function init(plugin)
  plugin:newCommand{id='Pixel2VoxelWorkspace',title='Pixel2Voxel...',group='edit_menu',onclick=function()
    if not app.isUIAvailable then return end
    if workspace then app.alert('Pixel2Voxel is already open.');return end
    local ok,result=pcall(function() return require('ui.workspace').open(plugin,function() workspace=nil end) end)
    if ok then workspace=result else app.alert('Pixel2Voxel: '..tostring(result)) end
  end}
end
function exit(plugin) if workspace then workspace.close();workspace=nil end end
