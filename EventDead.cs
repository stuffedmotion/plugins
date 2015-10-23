using Oxide.Game.Rust.Cui;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Linq;
using System.Data;
using System.Collections.Generic;
using Oxide.Core.Libraries;
using System.Reflection;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EventDead", "Larry", "0.0.6")]
    internal class EventDead : RustPlugin
    {

        //GUI variables
        public string mainName = null;
        public string mainCount = null;
        private List<Vector3> spawns;
        private List<EventPlayer> EventPlayers;
        private List<BasePlayer> DeletingAdmins;

        private Timer freezeTimer;

        private bool EventOpen;
        private bool doAirdrop;
        private bool EventStarted;
        private bool EventLive;
        private bool EventLimitPlayers;
        private string EventKit;
		private string json1;
        private int maxSpawns;
        private string spawnName;
        public static Item itemp;

		private string winnerFirst = "N/A"; // added to display winners when typing command /winners
		private string winnerSecond = "N/A";
		private string winnerThird = "N/A";
		
        private  List<BasePlayer> players;
        
        private CuiElementContainer elements;
        public static FieldInfo lastPositionValue;
        private int currentCount = 0;

        public Vector3 airdropPos;

        private  List<UnityEngine.GameObject> objectsToBlow;


        [PluginReference]
        Plugin Spawns2;

        [PluginReference]
        Plugin Kits;

        [PluginReference]
        Plugin checkCeiling;

        [PluginReference]
        Plugin PopupNotifications;

        private static string MessagePrefix =  "[ <color=#406B35>DeadLaugh Event</color> ]: ";
        private static string MessageEventOpen = "has opened the event! Type <color=red>/event</color> to join!";
        private static string MessageEventLaunch = "Event will begin in <color=red>10</color> seconds!";
        private static string MessageEventWinner = "The winner is <color=red>{0}</color>!";
        private static string MessageEventWinner2 = "They will be momentarily sacrificed to the gods!";

  




        /////////////////////////////////////////////////////////////
        /////////////// DATA STRUCTURES /////////////////////////////



        class EventInvItem
        {
            public string itemid;
            public string bp;
            public string skinid;
            public string container;
            public string amount;
            public float durability;
            public List<Item> mods;
            public int ammo;
            public ItemDefinition ammoType;

            public EventInvItem()
            {

            }
            public EventInvItem(int itemid, bool bp, string container, int amount, int skinid = 0, float durability = 100f, int ammo = 0, ItemDefinition ammoType = null, List<Item> itemlist = null)
            {
                this.itemid = itemid.ToString();
                this.bp = bp.ToString();
                this.skinid = skinid.ToString();
                this.amount = amount.ToString();
                this.container = container;
                this.durability = durability;
                this.mods = itemlist;
                this.ammo = ammo;
                this.ammoType = ammoType;
            }

        }

        public static List<Item> getMods(ItemContainer contents){
            if(contents == null)
                return null;

            List<Item> mods = new List<Item>();
            foreach(Item itm in contents.itemList){
                  mods.Add(itm);
                  //ConsoleSystem.Broadcast("chat.add", new object[] { 0, itm.info.displayName.english });
            }
            return mods;
        }

        public static int getAmmo(BaseEntity pro){
            var pro1 = pro as BaseProjectile;
            if (pro1 == null)
                return 0;
            return pro1.primaryMagazine.contents; 
        }
        public static ItemDefinition getAmmoType(BaseEntity pro){
            var pro1 = pro as BaseProjectile;
            if (pro1 == null)
                return null;
            //ConsoleSystem.Broadcast("chat.add", new object[] { 0, pro1.primaryMagazine.ammoType.ToString() });
            return pro1.primaryMagazine.ammoType; 
        }


        class EventPlayer : MonoBehaviour
        {
            public BasePlayer player;

            public bool inEvent;
            public bool savedInventory;
            public bool savedHome;
            public Vector3 spawnVec;

            public List<EventInvItem> InvItems = new List<EventInvItem>();
            public List<Vector3> spawns = new List<Vector3>();

            public Vector3 Home;
           
			public float hydration; // added to save/restore hydration and calories on save/teleport
			public float calories;

            void Awake()
            {
                inEvent = true;
                savedInventory = false;
                savedHome = false;
                player = GetComponent<BasePlayer>();
            }

            public void SaveHome()
            {
                if (!savedHome)
                    Home = player.transform.position;
                savedHome = true;
				calories = player.metabolism.calories.value;
				hydration = player.metabolism.hydration.value;
            }
            public void TeleportHome()
            {
                if (!savedHome)
                    return;
                ForcePlayerPosition(player, Home);
                savedHome = false;
				player.metabolism.calories.value = calories;
				player.metabolism.hydration.value = hydration;
            }

            public void SaveInventory()
            {
                if (savedInventory)
                    return;

                InvItems.Clear();
                foreach (Item item in player.inventory.containerWear.itemList)
                {

                    InvItems.Add(new EventInvItem(item.info.itemid, item.IsBlueprint(), "wear", item.amount, item.skin, item.condition, getAmmo(item.GetHeldEntity()), getAmmoType(item.GetHeldEntity()), getMods(item.contents)));
                }
                foreach (Item item in player.inventory.containerMain.itemList)
                {
                    InvItems.Add(new EventInvItem(item.info.itemid, item.IsBlueprint(), "main", item.amount, item.skin, item.condition, getAmmo(item.GetHeldEntity()), getAmmoType(item.GetHeldEntity()), getMods(item.contents)));
                }
                foreach (Item item in player.inventory.containerBelt.itemList)
                {
                    InvItems.Add(new EventInvItem(item.info.itemid, item.IsBlueprint(), "belt", item.amount, item.skin, item.condition,  getAmmo(item.GetHeldEntity()), getAmmoType(item.GetHeldEntity()), getMods(item.contents)));
                }

                savedInventory = true;
            }


            public void RestoreInventory()
            {
                foreach (EventInvItem kitem in InvItems)
                {
                    Item item = ItemManager.CreateByItemID(int.Parse(kitem.itemid), int.Parse(kitem.amount), Convert.ToBoolean(kitem.bp));
                    item.skin = int.Parse(kitem.skinid);
                    item.condition = kitem.durability;

                    var weapon = item.GetHeldEntity() as BaseProjectile;
                    if (weapon != null){
                        weapon.primaryMagazine.contents = kitem.ammo;
                        if(kitem.ammoType != null){
                            weapon.primaryMagazine.ammoType = kitem.ammoType;
                        }
                    }

                   if(kitem.mods != null){
                    foreach(Item it in kitem.mods){
                        item.contents.AddItem(it.info, 1);
                    }
                   }

                    player.inventory.GiveItem(item, kitem.container == "belt" ? player.inventory.containerBelt : kitem.container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);

                }
                savedInventory = false;
            }
        }





        void Loaded()
        {
            airdropPos = new Vector3();
            airdropPos = Vector3.zero;
            doAirdrop = false;
            EventLive = false;
            json1 = null;
            EventPlayers = new List<EventPlayer>();
            DeletingAdmins = new List<BasePlayer>();
            objectsToBlow = new List<UnityEngine.GameObject>();
            spawns = new List<Vector3>();
            lastPositionValue = typeof(BasePlayer).GetField("lastPositionValue", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }
        void Unloaded()  
        {
                foreach (EventPlayer player in EventPlayers)
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.player.net.connection }, null, "DestroyUI", new Facepunch.ObjectList("event_top", null, null, null, null));
                     CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.player.net.connection }, null, "DestroyUI", new Facepunch.ObjectList("event_countdown", null, null, null, null));
                     CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.player.net.connection }, null, "DestroyUI", new Facepunch.ObjectList("event_blackout", null, null, null, null));
                    player.player.SetPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted, false);
                }
                if(freezeTimer != null){
                    freezeTimer.Destroy(); 
                }
                SendPlayersHome();
                RedeemPlayersInventory();
                TryEraseAllPlayers();
                EjectAllPlayers();
                EventPlayers.Clear();
        }  
        
        void OnServerInitialized()
        {
            EventOpen = false;
            EventStarted = false;
        }


        [ChatCommand("guitest")]
        private void guitest(BasePlayer player, string command, string[] args)
        {
            elements = new CuiElementContainer();

            mainName = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.1 0.1 0.1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.0 0.95",
                    AnchorMax = "1.0 1.0"
                },
                CursorEnabled = false
            }, "HUD/Overlay", "event_top"


            );
            

             elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "2 Players Left.",
                        FontSize = 24,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.0 0.0",
                        AnchorMax = "1.0 1.0"
                    }
                }, "event_top");

              elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "DeadLaugh Event",
                        FontSize = 16,
                        Align = TextAnchor.MiddleLeft
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.025 0.0",
                        AnchorMax = "1.0 1.0"
                    }
                }, "event_top");

              CuiHelper.AddUi(player, elements);


        }

        [ChatCommand("guiclear")]
        private void guiclear(BasePlayer player, string command, string[] args)
        {
           

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", new Facepunch.ObjectList("event_top", null, null, null, null));
            
        }

		[ChatCommand("blackout")] //remove blackout
        private void eventblackout(BasePlayer player, string command, string[] args)
        {
			if (!hasAccess(player)) return;
			
            foreach (EventPlayer eventplayer in EventPlayers)
            {
				CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = eventplayer.player.net.connection }, null, "DestroyUI", new Facepunch.ObjectList("event_blackout", null, null, null, null));
               // Effect.server.Run("assets/bundled/prefabs/fx/c4_explosion.prefab", eventplayer.player.transform.position, Vector3.zero, null, false);
			}
        }

        ////////////////// FREEZE Players ///////////////////////////

        private void OnTimerFreeze()
        {
            if(!EventLive){
                foreach (EventPlayer player in EventPlayers)
                {
					if (Vector3.Distance(player.player.transform.position, player.spawnVec) < 1) continue;
                    // player.player.transform.position = player.spawnVec;
                    //lastPositionValue.SetValue(player.player, player.transform.position);
                    // BroadcastToChat(Vector3.Distance(player.player.transform.position, player.spawnVec).ToString());
                    // player.player.ClientRPCPlayer(null, player.player, "ForcePositionTo", new object[] { player.spawnVec });
					ForcePlayerPosition2(player.player, player.spawnVec);
                    //player.player.TransformChanged();
                }

            }
        }

		[ChatCommand("winners")] // show winners
        private void eventwinners(BasePlayer player, string command, string[] args)
        {
			SendReply(player, "[ <color=#406B35>DeadLaugh Event Winners</color> ]: <color=red>First</color>: " + winnerFirst + ", <color=red>Second</color>: " + winnerSecond + ", <color=red>Third</color>: " + winnerThird);
        }

        [ChatCommand("event_floors")]
        private void eventfloors(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;

            if(!DeletingAdmins.Contains(player)){
                DeletingAdmins.Add(player);
                SendReply(player, "Exploding Floors: ON! Anything you place will explode on Event Launch.");
            }
            else{
                DeletingAdmins.Remove(player);
                SendReply(player, "Exploding Floors: OFF. Your building is back to normal.");
            }
        }

        [ChatCommand("event_launch")]
        private void eventlaunch(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;
            EventOpen = false;
            EventStarted = false;
            foreach(EventPlayer player1 in EventPlayers){
                player1.player.SetPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted, false);
            }
            startCountdown();
            BroadcastToChat(MessageEventLaunch);
            
        }
  
        [ChatCommand("event_new")]
        private void eventnew(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;

            if(args.Length < 2){
                SendReply(player, "The syntax is /event_new [kitname] [spawnlist name] [doAirdrop true/false]");
                return;
            }
            
			if (args[0] != "nokit")
			{
				object kit = Kits?.Call("CanRedeemKit", player, args[0]);
				
				if ((kit is string) && (kit.ToString().Contains("doesn't exist")))
				{
					SendReply(player, "Kit does not exist! To start without a kit, type: <color=red>/event_new nokit " + args[1] + "</color>");
					return;
				}
			}

            if(args.Length == 3){
                if(args[2] == "true"){
                    doAirdrop = true;
                }
            }
			
			EventKit = args[0];
			spawnName = args[1];
			
			EventLimitPlayers = false;

			object gsc = Spawns2.Call("GetSpawnsCount", new object[] { spawnName });
			
			if ((gsc is string) && (gsc.ToString() == "This file doesn't exist"))
			{
				SendReply(player, "Spawn does not exist: <color=red>" + spawnName + "</color>");
				return;
			}
			
			maxSpawns = Convert.ToInt32(gsc);
						
			cancelEvent();
			EventOpen = true;
			airdropPos = Vector3.zero;
            Vector3 adCenter = new Vector3(0,0,0);

			for(int i=1; i<=maxSpawns; i++) 
			{
                Vector3 spawnPos = (Vector3)Spawns2.Call("GetSpawn", new object[] { spawnName, i-1});
			   spawns.Add(spawnPos);
               adCenter += spawnPos;
			}

            airdropPos = adCenter/maxSpawns;

			freezeTimer = timer.Every(1, OnTimerFreeze);

			winnerFirst = "N/A";
			winnerSecond = "N/A";
			winnerThird = "N/A";
			
			BroadcastToChat("<color=red>" + player.displayName + "</color> " + MessageEventOpen);
        }

        [ChatCommand("event_cancel")]
        private void closeevent(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;
            if (EventOpen)
            {
                cancelEvent();
                BroadcastToChat("Event has been cancelled.");
            }
            else{
                SendReply(player, "No event to close");
            }
            return;
        }

        [ChatCommand("event_msg")]
        private void eventmsg(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;

            foreach (EventPlayer player1 in EventPlayers)
            {
                PopupNotifications?.Call("CreatePopupNotification", "Test message", player1.player);
            }
            
            return;
        }


        

        [ChatCommand("event")]
        private void joinevent(BasePlayer player, string command, string[] args)
        {
            if (EventOpen && player.IsAlive() )
            {
                if((bool)checkCeiling.Call("isCeiling", new object[] { player })){
                    SendReply(player, "You need to be on the ground floor before teleporting to the event! (This avoids potential death on respawn)");
                    return; 
                }
               // MeshBatchPhysics.Raycast(player.transform.position + new Vector3(0f, -1.15f, 0f), Vector3Down, out cachedRaycast, out cachedBoolean, out cachedhitInstance);

                if (maxSpawns != 0 && EventPlayers.Count >= maxSpawns){
					SendReply(player, "Event is full.");
                    return; 
                }
                if (player.GetComponent<EventPlayer>())
                {
					if (EventPlayers.Contains(player.GetComponent<EventPlayer>())){
                       SendReply(player, "You are already in the event.");
                       return;
					}
                }

                EventPlayer event_player = player.GetComponent<EventPlayer>();
                if (event_player == null) event_player = player.gameObject.AddComponent<EventPlayer>();

                event_player.inEvent = true;
                event_player.enabled = true;
                EventPlayers.Add(event_player);

                if (player.IsWounded()){ // wounded prior to teleport
					player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
					player.CancelInvoke("WoundingEnd");
					player.metabolism.bleeding.value = 0f;
				}
				
				player.CancelInvoke("InventoryUpdate"); // bug fix: players who were crafting joined with said crafting items/materials
                player.inventory.crafting.CancelAll(true);

                SaveHomeLocation(player);
                SaveInventory(player);
                player.inventory.Strip();
                
                player.SetPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted, true);
                //Interface.CallHook("OnEventPlayerSpawn", new object[] { player });
               
                event_player.spawnVec = spawns.First();
                ForcePlayerPosition(player, event_player.spawnVec);

                if (player.IsWounded()){ // wounded from teleport bug
					player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
					player.CancelInvoke("WoundingEnd");
					player.metabolism.bleeding.value = 0f;
				}

                player.health = 100f;
                
				if(!EventLimitPlayers)
                    spawns.Remove(event_player.spawnVec);
                updateGUI();
              
                BroadcastToChat(string.Format("{0} has joined the Event! (#<color=red>{1}</color>)", player.displayName, EventPlayers.Count.ToString()));
                return;
            }
            else{
                SendReply(player, "Event not open.");
                return;
            }
        }

        private void cancelEvent()
        {
            EventStarted = false;
            EventOpen = false;
            doAirdrop = false;
            EventLive = false;
            airdropPos = Vector3.zero;
            spawns.Clear();
            //redeem stuff
            SendPlayersHome();
            RedeemPlayersInventory();
            TryEraseAllPlayers();
            EjectAllPlayers();

            EventPlayers.Clear();
            if(freezeTimer != null){
				freezeTimer.Destroy();
            }
            //BroadcastToChat("Event has been cancelled.");
            return;
        }

        private void startCountdown()
        {
            currentCount = 10;
            timer.Repeat(1, 13, () => nextCountdown());
            
        }
        private void nextCountdown()
        {
            if(currentCount >= 0){
                if(elements != null){
                    elements = null;
                }
                elements = new CuiElementContainer();

                string numToShow = currentCount.ToString();
                string color = "0 0 0 1.0";

                if(currentCount == 0){
                    numToShow = "FIGHT!";
                    color = "0.50 0.0 0.0 1.0";
                    foreach(EventPlayer player in EventPlayers){
                        GivePlayerKit(player.player, EventKit);

                    }
                    EventStarted = true;
                }
                BroadcastToChat(numToShow);

                var oldCount = mainCount;
                mainCount = elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.1 0.1 0.1 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.45 0.45",
                        AnchorMax = "0.55 0.55"
                    },
                    CursorEnabled = false
                }, "HUD/Overlay", "event_countdown");
                

                elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = numToShow,
                            FontSize = 34,
                            Align = TextAnchor.MiddleCenter,
                            Color = color
                        }
,                        RectTransform =
                        {
                            AnchorMin = "0.0 0.0",
                            AnchorMax = "1.0 1.0"
                        }
                    }, "event_countdown");

                if(currentCount == 0){
                    ConsoleSystem.Run.Server.Normal("launchEvent");
                }
				
                foreach (EventPlayer player in EventPlayers)
                {   
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.player.net.connection }, null, "DestroyUI", new Facepunch.ObjectList("event_countdown", null, null, null, null));

                    CuiHelper.AddUi(player.player, elements);

                    if(currentCount > 0){
						Effect.server.Run("assets/bundled/prefabs/fx/door/lock.code.lock.prefab", player.player.transform.position, Vector3.zero, null, false);
                    }
                    if(currentCount == 0){
						CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.player.net.connection }, null, "DestroyUI", new Facepunch.ObjectList("event_blackout", null, null, null, null));
						Effect.server.Run("assets/bundled/prefabs/fx/entities/helicopter/heli_explosion.prefab", player.player.transform.position, Vector3.zero, null, false);
                        BroadcastToChat("SDFDDSF");
                    }
                }
            }
            else{
                foreach (EventPlayer player in EventPlayers)
                { 
                    if(mainCount != null){
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.player.net.connection }, null, "DestroyUI", new Facepunch.ObjectList("event_countdown", null, null, null, null));
                    }
                }
            }
            currentCount--;
        }

        [ConsoleCommand("launchEvent")]
        private void launchEvent(ConsoleSystem.Arg arg)
        {
            foreach (UnityEngine.GameObject gobject in objectsToBlow)
            {
                UnityEngine.GameObject.Destroy(gobject);
            }
            objectsToBlow.Clear();

            EventLive = true;
            if(freezeTimer != null){
                freezeTimer.Destroy();
            }

            if(doAirdrop && airdropPos != Vector3.zero){
                BaseEntity entity = GameManager.server.CreateEntity("assets/bundled/prefabs/events/cargo_plane.prefab", new Vector3(), new Quaternion(1f,0f,0f,0f));
                if (entity != null)
                {
                    CargoPlane plane = entity.GetComponent<CargoPlane>();
                    plane.InitDropPosition(airdropPos);
                    entity.Spawn(true);
                }
            }


        }
        private void OnEntityBuilt(Planner planner, UnityEngine.GameObject component)
        {

            if(DeletingAdmins.Contains(planner.ownerPlayer)){
                objectsToBlow.Add(component);
                PrintToChat(planner.ownerPlayer, "Event structure place.");
            }
        
        }

        private BaseEntity CreateRocket(Vector3 startPoint, Vector3 direction, bool isFireRocket)
        {
            ItemDefinition projectileItem;
            

            projectileItem = ItemManager.FindItemDefinition("ammo.rocket.basic");
        
            ItemModProjectile component = projectileItem.GetComponent<ItemModProjectile>();
            BaseEntity entity = GameManager.server.CreateEntity(component.projectileObject.resourcePath, startPoint, new Quaternion(), true);
            
            TimedExplosive timedExplosive = entity.GetComponent<TimedExplosive>();
            ServerProjectile serverProjectile = entity.GetComponent<ServerProjectile>();
            
            serverProjectile.gravityModifier = 0f;
            serverProjectile.speed = 20f;
            timedExplosive.timerAmountMin = 0.1f;
            timedExplosive.timerAmountMax = 0.1f;
            
            entity.SendMessage("InitializeVelocity", (object) (direction * 0.5f));
            entity.Spawn(true);
            
            return entity;
        }

        [ChatCommand("leave")]
        void cmdEventLeave(BasePlayer player, string command, string[] args)
        {
            object success = LeaveEvent(player);
            if (success is string)
            {
                SendReply(player, (string)success);
                return;
            }
        }

        [ConsoleCommand("closeui")]
        private void CloseUi(ConsoleSystem.Arg arg)
        {
           // CuiHelper.DestroyUi(arg.Player(), mainName);
           clearGUI();
        }

        private int activeCount(){
            int count1 = 0;
            foreach (EventPlayer player in EventPlayers)
            {
                if(player.inEvent){
                    count1++;
                }
            }
            return count1;
        }
         private BasePlayer getWinner(){
            foreach (EventPlayer player in EventPlayers)
            {
                if(player.inEvent){
                    return player.player;
                }
            }
            return null;
        }

        private void clearGUI()
        {
             foreach (EventPlayer player1 in EventPlayers)
            {
                if(mainName != null){
                    CuiHelper.DestroyUi(player1.player, mainName);
                }
            }
        }
        private void updateGUI()
        {
           // startCountdown();

            if(elements != null){
                elements = null;
            }
            elements = new CuiElementContainer();
            string mainNameTemp = mainName;
            

            if(!EventStarted){
                json1 = @"[
                       { 
                            ""name"": ""event_blackout"",
                            ""parent"": ""HUD/Overlay"",
                            ""components"":
                            [ 
                                {
                                    ""type"":""UnityEngine.UI.RawImage"",
                                    ""imagetype"": ""Filled"",
                                    ""color"": ""1.0 1.0 1.0 1.0"",
                                    ""url"": ""http://rust.deadlaugh.com/overlay/dl2.png"",
                                    ""fadeIn"": ""1""
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.0 0.0"",
                                    ""anchormax"": ""1.0 1.0""
                                }
                            ]
                        }
                    ]
                    ";

            }



            mainName = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.1 0.1 0.1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.0 0.95",
                    AnchorMax = "1.0 1.0"
                },
                CursorEnabled = false
            },"HUD/Overlay", "event_top");
            

             elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = activeCount().ToString() + " Players Left.",
                        FontSize = 24,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.0 0.0",
                        AnchorMax = "1.0 1.0"
                    }
                }, "event_top");

              elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "DeadLaugh Event",
                        FontSize = 16,
                        Align = TextAnchor.MiddleLeft
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.025 0.0",
                        AnchorMax = "1.0 1.0"
                    }
                }, "event_top");

            foreach (EventPlayer player1 in EventPlayers)
            {
                    
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player1.player.net.connection }, null, "DestroyUI", new Facepunch.ObjectList("event_top", null, null, null, null));
                    
                    
                    if(player1.inEvent){
                        if(!EventStarted){
                            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player1.player.net.connection }, null, "AddUI", new Facepunch.ObjectList(json1));
                        }
                        CuiHelper.AddUi(player1.player, elements);
                    }
            }

            if(activeCount() == 1 && EventStarted ){
                startCelebration(getWinner());
            }
        } 

        void startCelebration(BasePlayer player)
        {
            EventOpen = false;
            EventStarted = false;
			winnerFirst = player.displayName.ToString();
            BroadcastToChat(string.Format(MessageEventWinner, winnerFirst));
            BroadcastToChat(string.Format(MessageEventWinner2));

            timer.Once(5, () => killWinner(player));
        }
        private void killWinner(BasePlayer player)
        {
            Vector3 direction = (Vector3.up).normalized;
            Vector3 launchPos = player.transform.position;
            BaseEntity rocket = CreateRocket(launchPos, direction, true);
        }

        static void PutToSleep(BasePlayer player)
        {
            if (!player.IsSleeping())
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
                if (!BasePlayer.sleepingPlayerList.Contains(player))
                {
                    BasePlayer.sleepingPlayerList.Add(player);
                }
                player.CancelInvoke("InventoryUpdate");
                player.inventory.crafting.CancelAll(true);
            }
        }

        void SaveInventory(BasePlayer player)
        {
            var eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            eventplayer.SaveInventory();
        }
        void SaveHomeLocation(BasePlayer player)
        {
            EventPlayer eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            eventplayer.SaveHome();
        }
        void RedeemInventory(BasePlayer player)
        {
            EventPlayer eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            if (player.IsDead() || player.health < 1)
                return;
            if (eventplayer.savedInventory)
            {
                eventplayer.player.inventory.Strip();
                eventplayer.RestoreInventory();
            }
        }
        void TeleportPlayerHome(BasePlayer player)
        {
            EventPlayer eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            if (player.IsDead() || player.health < 1)
                return;
            if (eventplayer.savedHome)
            {
                eventplayer.TeleportHome();
            }
        }
        void TryErasePlayer(BasePlayer player)
        {
            var eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            if (!(eventplayer.inEvent) && !(eventplayer.savedHome) && !(eventplayer.savedInventory))
                GameObject.Destroy(eventplayer);
        }
        void GivePlayerKit(BasePlayer player, string GiveKit)
        {
            Kits.Call("GiveKit", player, GiveKit);
        }

        void EjectAllPlayers()
        {
            foreach (EventPlayer eventplayer in EventPlayers)
            {
                EjectPlayer(eventplayer.player);
                eventplayer.inEvent = false;
            }
        }
        void SendPlayersHome()
        {
            foreach (EventPlayer eventplayer in EventPlayers)
            {
                TeleportPlayerHome(eventplayer.player);
            }
        }
        void RedeemPlayersInventory()
        {
            foreach (EventPlayer eventplayer in EventPlayers)
            {
                RedeemInventory(eventplayer.player);
            }
        }
        void TryEraseAllPlayers()
        {
            foreach (EventPlayer eventplayer in EventPlayers)
            {
                TryErasePlayer(eventplayer.player);
            }
        }
        void EjectPlayer(BasePlayer player)
        {
            if (player.IsAlive())
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
                player.CancelInvoke("WoundingEnd");
                player.health = 100f;
                player.metabolism.bleeding.value = 0f;
            }
        }
		
		static void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            PutToSleep(player);

            player.transform.position = destination;
            lastPositionValue.SetValue(player, player.transform.position);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", new object[] { destination });
            player.TransformChanged();

            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();

            player.SendNetworkUpdateImmediate(false);
            player.ClientRPCPlayer(null, player, "StartLoading");
            player.SendFullSnapshot();
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, false);
            player.ClientRPCPlayer(null, player, "FinishLoading");

        }

		static void ForcePlayerPosition2(BasePlayer player, Vector3 destination)
        {
            //PutToSleep(player);

            player.transform.position = destination;
            lastPositionValue.SetValue(player, player.transform.position);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", new object[] { destination });
            player.TransformChanged();

            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();

            player.SendNetworkUpdateImmediate(false);
            //player.ClientRPCPlayer(null, player, "StartLoading");
            player.SendFullSnapshot();
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, false);
           // player.ClientRPCPlayer(null, player, "FinishLoading");

        }
        void OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        {
            //if (!EventStarted) return;
            if (!(entity is BasePlayer)) return;
            if ((entity as BasePlayer).GetComponent<EventPlayer>() == null) return;
            
            (entity as BasePlayer).GetComponent<EventPlayer>().inEvent = false;
             (entity as BasePlayer).SetPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted, false);
            addBackSpawn((entity as BasePlayer));
            if(EventStarted){
                if(activeCount() == 2){
					winnerThird = (entity as BasePlayer).displayName.ToString();					
                    BroadcastToChat("Third Place: <color=red>" + winnerThird + "</color>");
                }
                if(activeCount() == 1){
                    winnerSecond = (entity as BasePlayer).displayName.ToString();
					BroadcastToChat("Second Place: <color=red>" + winnerSecond + "</color>");
                }
            }
            updateGUI();
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = (entity as BasePlayer).net.connection }, null, "DestroyUI", new Facepunch.ObjectList("event_blackout", null, null, null, null));
           // BroadcastToChat((entity as BasePlayer).displayName.ToString());
            return;
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player.GetComponent<EventPlayer>() != null)
            {
                LeaveEvent(player);
                

            }
        }
        void OnPlayerRespawned(BasePlayer player)
        {
            if (!(player.GetComponent<EventPlayer>())) return;
            if (player.GetComponent<EventPlayer>().savedInventory || player.GetComponent<EventPlayer>().savedHome)
            {
                player.GetComponent<EventPlayer>().inEvent = false;
                updateGUI();
                RedeemInventory(player);
                TeleportPlayerHome(player);
                EventPlayers.Remove(player.GetComponent<EventPlayer>());
                TryErasePlayer(player);
				player.health = 100f;
            }
        }
        object LeaveEvent(BasePlayer player)
        {
            if (player.GetComponent<EventPlayer>() == null)
            {
                return "You are not currently in the Event.";
            }
            if (!EventPlayers.Contains(player.GetComponent<EventPlayer>()))
            {
                return "You are not currently in the Event.";
            }
            player.GetComponent<EventPlayer>().inEvent = false;
            //BroadcastToChat(string.Format(MessagesEventLeft, player.displayName.ToString(), (EventPlayers.Count - 1).ToString()));
            updateGUI();
            addBackSpawn(player);
            player.inventory.Strip();
            RedeemInventory(player);
            TeleportPlayerHome(player);
            EventPlayers.Remove(player.GetComponent<EventPlayer>());
            EjectPlayer(player);
            TryErasePlayer(player);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", new Facepunch.ObjectList("event_blackout", null, null, null, null));
            return true;
        }
        void addBackSpawn(BasePlayer player)
        {
            if(!EventLimitPlayers)
                spawns.Add(player.GetComponent<EventPlayer>().spawnVec);
        }
        bool hasAccess(BasePlayer player)
        {
            if(player.userID.ToString() == "76561197966944585"){ // Larry steam ID
				return true;
            }
            if (player.net.connection.authLevel < 1){
                SendReply(player, "You are not allowed to use this command");
                return false;
            }
            return true;
        }
 
        // Broadcast To The General Chat /////////////////////////////////////////////////////
        void BroadcastToChat(string msg)
        {
            Debug.Log(msg);
            ConsoleSystem.Broadcast("chat.add", new object[] { 0, MessagePrefix + msg });
        }
 
		[HookMethod("SendHelpText")]
        void SendHelpText(BasePlayer player)
        {
            SendReply(player, "Type <color=red>/winners</color> to see the winners from the last event");
        }
    }
}