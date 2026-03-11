# Snap Point Expansion Playtest Checklist

Prerequisites: Run `Tools > Slopworks > Add Snap Points to Prefabs` in the editor first. Start the HomeBase scene as host. Press B to enter build mode.

---

## Test 1: Foundation on terrain (baseline)

**Steps:** Press 1 (Foundation tool). Look at the ground. Left-click.

**Expected:** Foundation appears on the ground, snapped to 1m grid.

**Actual:**
GOOD


---

## Test 2: Foundation side-snap to foundation

**Steps:** Place a foundation (Test 1). Look at its north face (+Z side). Left-click.

**Expected:** Second foundation snaps flush edge-to-edge with the first. No gap, no overlap. Same Y height.

**Actual:**
GOOD

---

## Test 3: Foundation stacked on top

**Steps:** Place a foundation. Look at its top face. Left-click.

**Expected:** Second foundation appears directly on top. Its bottom face sits flush on the first's top face.

**Actual:**
GOOD


---

## Test 4: Foundation attached below

**Steps:** Place a foundation elevated off the ground (use PgUp to nudge up a few meters first). Look at the bottom face from below. Left-click.

**Expected:** New foundation hangs below the existing one. Its top face is flush with the existing bottom face.

**Actual:**
Visually this was placed higher and the transform shows this correctly. the Pladcement info shows that the surfac y is 1.121712. so the placement info didnt update. when i tried to place the foundation underneath it. placed it directly in the same spot as the one i was clicking on below. instead of attaching the top to the existing bottom. see log below:
build: nudge +1.0m
UnityEngine.Debug:Log (object)
NetworkBuildController:HandleNudge (UnityEngine.InputSystem.Keyboard) (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:405)
NetworkBuildController:Update () (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:147)

build: nudge +2.0m
UnityEngine.Debug:Log (object)
NetworkBuildController:HandleNudge (UnityEngine.InputSystem.Keyboard) (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:405)
NetworkBuildController:Update () (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:147)

build: nudge +3.0m
UnityEngine.Debug:Log (object)
NetworkBuildController:HandleNudge (UnityEngine.InputSystem.Keyboard) (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:405)
NetworkBuildController:Update () (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:147)

build: nudge +4.0m
UnityEngine.Debug:Log (object)
NetworkBuildController:HandleNudge (UnityEngine.InputSystem.Keyboard) (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:405)
NetworkBuildController:Update () (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:147)

build: nudge +5.0m
UnityEngine.Debug:Log (object)
NetworkBuildController:HandleNudge (UnityEngine.InputSystem.Keyboard) (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:405)
NetworkBuildController:Update () (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:147)

build: placed Foundation at (151,312) y=1.1
UnityEngine.Debug:Log (object)
NetworkBuildController:HandleBuildInput (UnityEngine.InputSystem.Mouse) (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:251)
NetworkBuildController:Update () (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:179)

grid: Foundation placed at (151,312) y=1.1 by 0
UnityEngine.Debug:Log (object)
GridManager:RpcLogic___CmdPlace___4195636941 (UnityEngine.Vector2Int,single,int,int,BuildingCategory,UnityEngine.Vector3,FishNet.Connection.NetworkConnection) (at Assets/_Slopworks/Scripts/Network/GridManager.cs:287)
GridManager:RpcReader___Server_CmdPlace___4195636941 (FishNet.Serializing.PooledReader,FishNet.Transporting.Channel,FishNet.Connection.NetworkConnection)
FishNet.Object.NetworkBehaviour:ReadServerRpc (int,bool,uint,FishNet.Serializing.PooledReader,FishNet.Connection.NetworkConnection,FishNet.Transporting.Channel) (at Assets/FishNet/Runtime/Object/NetworkBehaviour/NetworkBehaviour.RPCs.cs:281)
FishNet.Managing.Server.ServerObjects:ParseServerRpc (FishNet.Serializing.PooledReader,FishNet.Connection.NetworkConnection,FishNet.Transporting.Channel) (at Assets/FishNet/Runtime/Managing/Server/Object/ServerObjects.Parsing.cs:30)
FishNet.Managing.Server.ServerManager:ParseReceived (FishNet.Transporting.ServerReceivedDataArgs) (at Assets/FishNet/Runtime/Managing/Server/ServerManager.cs:824)
FishNet.Managing.Server.ServerManager:Transport_OnServerReceivedData (FishNet.Transporting.ServerReceivedDataArgs) (at Assets/FishNet/Runtime/Managing/Server/ServerManager.cs:703)
FishNet.Transporting.Tugboat.Tugboat:HandleServerReceivedDataArgs (FishNet.Transporting.ServerReceivedDataArgs) (at Assets/FishNet/Runtime/Transporting/Transports/Tugboat/Tugboat.cs:304)
FishNet.Transporting.Tugboat.Server.ServerSocket:IterateIncoming () (at Assets/FishNet/Runtime/Transporting/Transports/Tugboat/Core/ServerSocket.cs:469)
FishNet.Transporting.Tugboat.Tugboat:IterateIncoming (bool) (at Assets/FishNet/Runtime/Transporting/Transports/Tugboat/Tugboat.cs:235)
FishNet.Managing.Transporting.TransportManager:IterateIncoming (bool) (at Assets/FishNet/Runtime/Managing/Transporting/TransportManager.cs:750)
FishNet.Managing.Timing.TimeManager:TryIterateData (bool) (at Assets/FishNet/Runtime/Managing/Timing/TimeManager.cs:1111)
FishNet.Managing.Timing.TimeManager:IncreaseTick () (at Assets/FishNet/Runtime/Managing/Timing/TimeManager.cs:733)
FishNet.Managing.Timing.TimeManager:<TickUpdate>g__MethodLogic|113_0 () (at Assets/FishNet/Runtime/Managing/Timing/TimeManager.cs:392)
FishNet.Managing.Timing.TimeManager:TickUpdate () (at Assets/FishNet/Runtime/Managing/Timing/TimeManager.cs:380)
FishNet.Transporting.NetworkReaderLoop:Update () (at Assets/FishNet/Runtime/Transporting/NetworkReaderLoop.cs:29)

build: tool = Wall
UnityEngine.Debug:Log (object)
NetworkBuildController:SwitchTool (NetworkBuildController/BuildTool) (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:424)
NetworkBuildController:Update () (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:132)

build: tool = Foundation
UnityEngine.Debug:Log (object)
NetworkBuildController:SwitchTool (NetworkBuildController/BuildTool) (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:424)
NetworkBuildController:Update () (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:131)

build: placed Foundation at (151,312) y=0.1
UnityEngine.Debug:Log (object)
NetworkBuildController:HandleBuildInput (UnityEngine.InputSystem.Mouse) (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:251)
NetworkBuildController:Update () (at Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:179)

grid: Foundation placed at (151,312) y=0.1 by 0
UnityEngine.Debug:Log (object)
GridManager:RpcLogic___CmdPlace___4195636941 (UnityEngine.Vector2Int,single,int,int,BuildingCategory,UnityEngine.Vector3,FishNet.Connection.NetworkConnection) (at Assets/_Slopworks/Scripts/Network/GridManager.cs:287)
GridManager:RpcReader___Server_CmdPlace___4195636941 (FishNet.Serializing.PooledReader,FishNet.Transporting.Channel,FishNet.Connection.NetworkConnection)
FishNet.Object.NetworkBehaviour:ReadServerRpc (int,bool,uint,FishNet.Serializing.PooledReader,FishNet.Connection.NetworkConnection,FishNet.Transporting.Channel) (at Assets/FishNet/Runtime/Object/NetworkBehaviour/NetworkBehaviour.RPCs.cs:281)
FishNet.Managing.Server.ServerObjects:ParseServerRpc (FishNet.Serializing.PooledReader,FishNet.Connection.NetworkConnection,FishNet.Transporting.Channel) (at Assets/FishNet/Runtime/Managing/Server/Object/ServerObjects.Parsing.cs:30)
FishNet.Managing.Server.ServerManager:ParseReceived (FishNet.Transporting.ServerReceivedDataArgs) (at Assets/FishNet/Runtime/Managing/Server/ServerManager.cs:824)
FishNet.Managing.Server.ServerManager:Transport_OnServerReceivedData (FishNet.Transporting.ServerReceivedDataArgs) (at Assets/FishNet/Runtime/Managing/Server/ServerManager.cs:703)
FishNet.Transporting.Tugboat.Tugboat:HandleServerReceivedDataArgs (FishNet.Transporting.ServerReceivedDataArgs) (at Assets/FishNet/Runtime/Transporting/Transports/Tugboat/Tugboat.cs:304)
FishNet.Transporting.Tugboat.Server.ServerSocket:IterateIncoming () (at Assets/FishNet/Runtime/Transporting/Transports/Tugboat/Core/ServerSocket.cs:469)
FishNet.Transporting.Tugboat.Tugboat:IterateIncoming (bool) (at Assets/FishNet/Runtime/Transporting/Transports/Tugboat/Tugboat.cs:235)
FishNet.Managing.Transporting.TransportManager:IterateIncoming (bool) (at Assets/FishNet/Runtime/Managing/Transporting/TransportManager.cs:750)
FishNet.Managing.Timing.TimeManager:TryIterateData (bool) (at Assets/FishNet/Runtime/Managing/Timing/TimeManager.cs:1111)
FishNet.Managing.Timing.TimeManager:IncreaseTick () (at Assets/FishNet/Runtime/Managing/Timing/TimeManager.cs:733)
FishNet.Managing.Timing.TimeManager:<TickUpdate>g__MethodLogic|113_0 () (at Assets/FishNet/Runtime/Managing/Timing/TimeManager.cs:392)
FishNet.Managing.Timing.TimeManager:TickUpdate () (at Assets/FishNet/Runtime/Managing/Timing/TimeManager.cs:380)
FishNet.Transporting.NetworkReaderLoop:Update () (at Assets/FishNet/Runtime/Transporting/NetworkReaderLoop.cs:29)




---

## Test 5: Wall on foundation north edge

**Steps:** Press 2 (Wall tool). Place a foundation first. Look at the foundation's north face. Left-click.

**Expected:** Wall spawns flush against the foundation's north edge. Wall auto-rotates to face outward (north). Wall center Y is above the foundation surface.

**Actual:**
this is actually what happened but what should be happening is the wall should be placed with the bottom of the wall meeting the top face of the foundation. if you have multiple foundations joined together then the walls will be overlapping with the intersection of the foundations.


---

## Test 6: Wall on foundation east edge

**Steps:** Place a foundation. Look at its east face (+X side). Left-click with wall tool.

**Expected:** Wall spawns flush against east edge. Auto-rotates 90 degrees to face east. Same Y as Test 5.

**Actual:**
See above


---

## Test 7: Wall-to-wall horizontal snap

**Steps:** Place a wall on a foundation edge (Test 5). Look at the side of that wall (left or right edge). Left-click with wall tool.

**Expected:** Second wall snaps flush next to the first wall, extending the wall run. Same height, same facing direction.

**Actual:**
The walls are get placed perpendicular. pressing rotate somethinges does nothing to the placement and sometimes makes it work. either way this is buggy and should be further investigated. i dont think we should ever make a Tee so even if the wall is rotated perpendicular it should meet at a corner rather than connect in the middle of one of the walls.


---

## Test 8: Wall-to-wall vertical stack

**Steps:** Place a wall (Test 5). Look at the top edge of that wall. Left-click with wall tool.

**Expected:** Second wall appears directly on top of the first. Bottom of new wall flush with top of existing wall.

**Actual:**
This technically works but the threshold for the placement locking to top should be larger. if im standing on the ground looking at the 4m tall wall i want to be able to look up at the top edge and have the wall snap to the top of the wall like it does now. right now i have to be higher than the wall top and look exactly in the center of the top of the wall for the snap to work.

---

## Test 9: R-key rotation in snap mode

**Steps:** Place a foundation. Look at its north face with wall tool selected. Press R once. Left-click.

**Expected:** Wall rotates 90 degrees from the auto-aligned direction but still sits flush against the foundation face. The offset distance adjusts to account for the rotated depth.

**Actual:**
this only happens occasionaly. theres are some bugs here that we identified in test 7. sometimes it works sometimes it doesnt. in one instance it only went horizontal at 180 degrees but not at 0 which is weird considering they should look the same to the wall.

---

## Test 10: R-key rotation 180 degrees

**Steps:** Same as Test 9 but press R twice before clicking.

**Expected:** Wall faces the opposite direction (south instead of north) but still flush against the north face.

**Actual:**
See above


---

## Test 11: Ramp on foundation edge

**Steps:** Press 3 (Ramp tool). Place a foundation. Look at the foundation's north face. Left-click.

**Expected:** Ramp snaps to the foundation edge, flush. Auto-rotates to face outward.

**Actual:**
im not sure this is super clear from you're directions. i think you accidently said place foundation and look at foundations north face and Ramp on foundation edge when you meant Ramp on Ramp edge? either way theres no auto rotation happening with either one of these situations i just tried. if this is supposed to be ramp on ramp to extend the ramp up what actually happens is the ramp just connects the lower point to the front bottom middle.


---

## Test 12: Ramp with R-key rotation

**Steps:** Same as Test 11 but press R before clicking.

**Expected:** Ramp rotates but stays flush against the edge. Offset distance adjusts for rotated extents.

**Actual:**
i guess this works? i can rotate when placing ramps against foundations and i found one instance where i couldnt rotate the ramp on ramp side to match the same orientation. i got it to work on other ramp sides but im not sure this is what you're testing.

---

## Test 13: Multi-height snap selection

**Steps:** Place a tall wall. Aim at the very top edge of the wall. Note what snaps. Then aim at the very bottom edge. Note what snaps. Then aim at the center.

**Expected:** Aiming near the top selects the Top snap point. Aiming near the bottom selects the Bot snap point. Aiming at center selects Mid. The OnGUI display should show different snap normals/positions as you move the crosshair vertically.

**Actual:**

only snaps to bottom of the wall.

---

## Test 14: Delete placed building

**Steps:** Place any building. Press X (delete mode). Look at the building. Left-click.

**Expected:** Building is removed. Delete highlight (red tint) shows on hover before clicking.

**Actual:**
works


---

## Test 15: Zoop still works

**Steps:** Press Z (zoop mode). Press 1 (foundation). Click to set start. Move mouse along X or Z axis. Click to set end.

**Expected:** Row of foundations placed along the line. No gaps between them.

**Actual:**
Zoop is buggy. it slightly shifts the ghost to a different snap for some reason and we no longer get the placement of all of the visible green foundations when we arent actually doing the second click on where the foundation is being placed like we did before. basically you have to have your crosshair on the exact spot you want it to end or it wont fill out the ones that are already zooped in the ghost preview

---

## Test 16: Nudge + snap combo

**Steps:** Place a foundation. Press PgUp once. Look at the first foundation's side face. Left-click with foundation tool.

**Expected:** New foundation snaps to the side of the first, then the nudge offset raises it additionally. It should be flush horizontally but offset vertically.

**Actual:**
We tested this with the bottom. it doesnt work. the placement of the sides is actually different than the bug we saw with the placement on the bottom. the sides dont apply the nudge to the ghost of what you are trying to build. they get placed as if it wasnt nudged.


---

## Test 17: Tab to cycle variants

**Steps:** Press 1 (Foundation). Press Tab. Ghost preview should change to next foundation variant (SLAB_1m, SLAB_2m, SLAB_4m). Place each variant and snap them together.

**Expected:** Different-sized foundations snap flush to each other. Smaller foundations offset correctly against larger ones.

**Actual:**
this technically works. although it should depend on the ednge you're looking at to deteremine if you're supposed to be alligned at the bottom or the top.

---

## Notes

Write anything else you noticed here:


