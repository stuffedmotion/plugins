
-- ----------------------------------------------------------------------------
-- Check Ceiling                                      Version 1.4.22
-- ----------------------------------------------------------------------------
-- Filename:          checkCeiling.lua
-- ----------------------------------------------------------------------------
-- Description:
-- Checks if ground level for event plugin
-- ----------------------------------------------------------------------------

PLUGIN.Title       = "Ceiling Larry"
PLUGIN.Description = "Checks if ground level for event plugin"
PLUGIN.Version     = V( 1, 0, 5)
PLUGIN.HasConfig   = false
PLUGIN.Author      = "larry"

local RaycastAll      = UnityEngine.Physics.RaycastAll["methodarray"][7]
local Raycast         = UnityEngine.Physics.Raycast.methodarray[12]



function PLUGIN:Init()

   -- command.AddConsoleCommand("teleport.topos", self.Plugin, "ccmdTeleport")
end

function PLUGIN:isCeiling(player)

-- Modify the local position, add 2 to the y coordinate.
    local position = player.transform.position
    position.y = position.y + 1


    -- Create a local variable to store the BuildingBlock.
    local ceiling = false
    local firstHit = true

    -- Create a Ray from the player to the ground to detect what the player is standing on
    local ray = new( UnityEngine.Ray._type, util.TableToArray { player.transform.position, UnityEngine.Vector3.get_down() } )
             
    local arr = util.TableToArray { ray, new( UnityEngine.RaycastHit._type, nil ), 1.5, -5 }
    util.ConvertAndSetOnArray(arr, 2, 1.5, System.Int64._type)
    util.ConvertAndSetOnArray(arr, 3, -5, System.Int32._type)
     
    if Raycast:Invoke( nil, arr ) then
        local hitEntity = global.RaycastHitEx.GetEntity(arr[1])
        if hitEntity then
            if hitEntity:GetComponentInParent(global.BuildingBlock._type) then
                local buildingBlock = hitEntity:GetComponentInParent(global.BuildingBlock._type)
                --rust.BroadcastChat(buildingBlock.name)
                if buildingBlock.name:find( "floor", 1, true) then
                    return true
                end
                if buildingBlock.name:find( "stair", 1, true) then
                    return true
                end
            end              
        end      
    end     
    return false

end
