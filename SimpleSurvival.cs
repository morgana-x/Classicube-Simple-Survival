using System;
using System.IO;
using System.Collections.Generic;
using BlockID = System.UInt16;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Events;
using MCGalaxy.Events.LevelEvents;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Tasks;
using MCGalaxy.Maths;
using MCGalaxy.Blocks;
using MCGalaxy.Network;
using MCGalaxy.Commands;
namespace MCGalaxy {
	
	
	public class SimpleSurvival : Plugin {
		public override string name { get { return "SimpleSurvival"; } }
		public override string MCGalaxy_Version { get { return "1.9.1.2"; } }
		public override int build { get { return 100; } }
		public override string welcome { get { return "SimpleSurvival"; } }
		public override string creator { get { return "morgana, Venk"; } }
		public override bool LoadAtStartup { get { return true; } }

	
		public class Config {
				public static int MaxHealth = 100;
				public static int MaxAir = 11;
				public static bool FallDamage = true;
				public static bool VoidKills = true;
				public static bool UseGoodlyEffects = true; // broken right now
				public static string HitParticle = "pvp"; // broken right now
				//public static string Path = "./plugins/VenksSurvival/"; // 
		}
		
		
		
		
		
		
		SchedulerTask drownTask;
		SchedulerTask guiTask;
		SchedulerTask regenTask;
		
		public override void Load(bool startup) {
			//LOAD YOUR PLUGIN WITH EVENTS OR OTHER THINGS!
			OnPlayerClickEvent.Register(HandleBlockClicked, Priority.Low);
			OnPlayerConnectEvent.Register(HandlePlayerConnect, Priority.Low);
			OnPlayerMoveEvent.Register(HandlePlayerMove, Priority.High);
			Server.MainScheduler.QueueRepeat(HandleDrown, null, TimeSpan.FromSeconds(1));
			Server.MainScheduler.QueueRepeat(HandleGUI, null, TimeSpan.FromMilliseconds(50));
			Server.MainScheduler.QueueRepeat(HandleRegeneration, null, TimeSpan.FromSeconds(4));
			foreach (Player p in PlayerInfo.Online.Items)
			{
				InitPlayer(p);
			}
		}
                        
		public override void Unload(bool shutdown) {
			//UNLOAD YOUR PLUGIN BY SAVING FILES OR DISPOSING OBJECTS!
			OnPlayerClickEvent.Unregister(HandleBlockClicked);
			OnPlayerConnectEvent.Unregister(HandlePlayerConnect);
			OnPlayerMoveEvent.Unregister(HandlePlayerMove);
			Server.MainScheduler.Cancel(drownTask);
			Server.MainScheduler.Cancel(guiTask);
			Server.MainScheduler.Cancel(regenTask);
		}
		public override void Help(Player p) {
			//HELP INFO!
		}
		
		void InitPlayer(Player p)
		{
			p.Extras["SURVIVAL_HEALTH"] = Config.MaxHealth;
			p.Extras["SURVIVAL_AIR"] = Config.MaxAir;
			p.Extras["PVP_HIT_COOLDOWN"] = DateTime.UtcNow;
			p.Extras["FALLING"] = false;
			SendPlayerGui(p);
		}
		void HandleGUI(SchedulerTask task)
        {
            guiTask = task;

            foreach (Player pl in PlayerInfo.Online.Items)
            {
				SendPlayerGui(pl);
			}
		}
		void HandleDrown(SchedulerTask task)
		{
			drownTask = task;
            foreach (Player p in PlayerInfo.Online.Items)
			{
				if (p.invincible) continue;
				if (IsDrowning(p) && GetAir(p) > 0)
				{
					SetAir(p, GetAir(p)-1);
			
					if (GetAir(p) <= 0)
					{
						Damage(p, 5, 8); 
					}
					else
					{
						SendPlayerGui(p);
					}
				}
				else if (GetAir(p) < Config.MaxAir)
				{
					SetAir(p, GetAir(p)+1);
				}
				
			}
		}
		int fallDamage(int height)
		{
			if (height < 4)
			{
				return 0;
			}
			return ((height)*6);
		}
		void HandlePlayerMove(Player p, Position next, byte rotX, byte rotY, ref bool cancel)
		{
			if (Config.VoidKills && next.Y < 0) Die(p, 4); // Player fell out of the world
			
			if (Config.FallDamage)
			{
				if (p.invincible) return;// || Hacks.CanUseFly(p)) return;

				ushort x = (ushort)(p.Pos.X / 32);
				ushort y = (ushort)(((p.Pos.Y - Entities.CharacterHeight) / 32) - 1);
				ushort y2 = (ushort)(((p.Pos.Y - Entities.CharacterHeight) / 32) - 2);
				ushort z = (ushort)(p.Pos.Z / 32);

				BlockID block = p.level.GetBlock((ushort)x, ((ushort)y), (ushort)z);
				BlockID block2 = p.level.GetBlock((ushort)x, ((ushort)y2), (ushort)z);

				string below = Block.GetName(p, block);
				string below2 = Block.GetName(p, block2);

				// Don't do fall damage if player lands in deep water (2+ depth)

				if (below.ToLower().Contains("water") && below2.ToLower().Contains("water"))
				{
					int fall = p.Extras.GetInt("FALL_START") - y;
					//if (fallDamage(fall) > 0 && p.Session.ClientName().CaselessContains("cef")) p.Message("cef resume -n splash"); // Play splash sound effect
					p.Extras["FALLING"] = false;
					p.Extras["FALL_START"] = y;
					return;
				}

				if (!p.Extras.GetBoolean("FALLING") && below.ToLower() == "air")
				{
					p.Extras["FALLING"] = true;
					p.Extras["FALL_START"] = y;
				}

				else if (p.Extras.GetBoolean("FALLING") && below.ToLower() != "air")
				{
					if (p.Extras.GetBoolean("FALLING"))
					{
						int fall = p.Extras.GetInt("FALL_START") - y;

						if (fallDamage(fall) > 0){
						Damage(p, fallDamage(fall), 0);
						}

						// Reset extra variables
						p.Extras["FALLING"] = false;
						p.Extras["FALL_START"] = y;
					}
				}
			}
            
		}
        void HandlePlayerConnect(Player p)
		{
			InitPlayer(p);
		}
		int GetLagCompensation(int ping)
		{
			int penalty = 0;

			if (ping == 0) penalty = 0; // "lagged-out"
			if (ping > 0 && ping <= 29) penalty = 50; // "great"
			if (ping > 29 && ping <= 59) penalty = 100; // "good"
			if (ping > 59 && ping <= 119) penalty = 150; // "okay"
			if (ping > 119 && ping <= 180) penalty = 200; // "bad"
			if (ping > 180) penalty = 250; // "horrible"
			return penalty;
		}
		static bool CanHitPlayer(Player p, Player victim)
        {
            Vec3F32 delta = p.Pos.ToVec3F32() - victim.Pos.ToVec3F32();
            float reachSq = 12f; // 3.46410161514 block reach distance

            int ping = p.Session.Ping.AveragePing();

            if (ping > 59 && ping <= 119) reachSq = 16f; // "okay"
            if (ping > 119 && ping <= 180) reachSq = 16f; // "bad"
            if (ping > 180) reachSq = 16f; // "horrible"

            // Don't allow clicking on players further away than their reach distance
            if (delta.LengthSquared > (reachSq + 1)) return false;

            // Check if they can kill players, determined by gamemode plugins
            //bool canKill = PvP.Config.GamemodeOnly == false ? true : p.Extras.GetBoolean("PVP_CAN_KILL");
            //if (!canKill) return false;

            if (p.Game.Referee || victim.Game.Referee || p.invincible || victim.invincible) return false; // Ref or invincible
            if (inSafeZone(p, p.level.name) || inSafeZone(victim, victim.level.name)) return false; // Either player is in a safezone

            if (!string.IsNullOrWhiteSpace(p.Extras.GetString("TEAM")) && (p.Extras.GetString("TEAM") == victim.Extras.GetString("TEAM")))
            {
                return false; // Players are on the same team
            }

            BlockID b = p.GetHeldBlock();

            if (Block.GetName(p, b).ToLower() == "bow") return false; // Bow damage comes from arrows, not player click

            // If all checks are complete, return true to allow knockback and damage
            return true;
        }
		void HandleBlockClicked(Player p, MouseButton button, MouseAction action, ushort yaw, ushort pitch, byte entity, ushort x, ushort y, ushort z, TargetBlockFace face)
		{
			if (button != MouseButton.Left) return;
			if (action != MouseAction.Released) return;
			Player victim = null; // If not null, the player that is being hit

			Player[] players = PlayerInfo.Online.Items;

			foreach (Player pl in players)
			{
				// Clicked on a player

				if (pl.EntityID == entity)
				{
					victim = pl;
					break;
				}
			}
			
			if (victim == null)
			{
				p.Extras["PVP_HIT_COOLDOWN"] = DateTime.UtcNow.AddMilliseconds(550 - GetLagCompensation(p.Session.Ping.AveragePing()));
				return;
			}
			if (!p.Extras.Contains("PVP_HIT_COOLDOWN"))
			{
				p.Extras["PVP_HIT_COOLDOWN"] = DateTime.UtcNow;
			}
			DateTime lastClickTime = (DateTime)p.Extras.Get("PVP_HIT_COOLDOWN");

			if (lastClickTime > DateTime.UtcNow) return;
			
			if (!CanHitPlayer(p, victim)) return;
			DoHit(p, victim);
			p.Extras["PVP_HIT_COOLDOWN"] = DateTime.UtcNow.AddMilliseconds(400 - GetLagCompensation(p.Session.Ping.AveragePing()));
		}
		void HandleRegeneration(SchedulerTask task)
        {
            regenTask = task;
            foreach (Player pl in PlayerInfo.Online.Items)
            {
                int health = GetHealth(pl);

                if (health >= Config.MaxHealth) continue; // No need to regenerate health if player is already at max health

                pl.Extras["SURVIVAL_HEALTH"] = health + 1;
            }
        }
		void DoHit(Player p, Player victim)
		{
			PushPlayer(p, victim); // Knock the victim back
			int dmg = 10;
			if (GetHealth(victim)-dmg <= 0)
			{
				Die(victim, 4);
				string deathMessage = p.color +  p.name + " %ekilled " + victim.color + victim.name + "%e.";
				foreach( Player pl in PlayerInfo.Online.Items)
				{
					if (p.level == pl.level || victim.level == pl.level)
					{
						pl.Message(deathMessage);
					}
				}
			}
			Damage(victim, dmg, 4);
		}
		static void PushPlayer(Player p, Player victim)
        {
            if (p.level.Config.MOTD.ToLower().Contains("-damage")) return;

            int srcHeight = ModelInfo.CalcEyeHeight(p);
            int dstHeight = ModelInfo.CalcEyeHeight(victim);
            int dx = p.Pos.X - victim.Pos.X, dy = (p.Pos.Y + srcHeight) - (victim.Pos.Y + dstHeight), dz = p.Pos.Z - victim.Pos.Z;

            Vec3F32 dir = new Vec3F32(dx, dy, dz);
            if (dir.Length > 0) dir = Vec3F32.Normalise(dir);

            float mult = 1 / ModelInfo.GetRawScale(victim.Model);
            float victimScale = ModelInfo.GetRawScale(victim.Model);

            if (victim.Supports(CpeExt.VelocityControl) && p.Supports(CpeExt.VelocityControl))
            {
                // Intensity of force is in part determined by model scale
                victim.Send(Packet.VelocityControl((-dir.X * mult) * 0.5f, 0.87f * mult, (-dir.Z * mult) * 0.5f, 0, 1, 0));

                // If GoodlyEffects is enabled, show particles whenever a player is hit
                if (Config.UseGoodlyEffects)
                {
                    // Spawn effect when victim is hit
                    //Command.Find("Effect").Use(victim, Config.HitParticle + " " + (victim.Pos.X / 32) + " " + (victim.Pos.Y / 32) + " " + (victim.Pos.Z / 32) + " 0 0 0 true");
                }
            }
            else
            {
                p.Message("You can left and right click on players to hit them if you update your client!");
            }
        }
		// GUI
		///////////////////////////////////////////////////////////////////////////
		static string GetHealthBar(int health)
		{
			if (health <= 0)
			{
				return "";
			}
			int repeat = (int)Math.Ceiling((double)(health / 10));// (int)Math.Floor((double)(health/2)); //(int)Math.Round((double)(health/Config.MaxHealth) * 10);
			return ("%c" + new string('♥', repeat ));
		}
		static string GetAirBar(int air)
		{
			if (air <= 0)
			{
				return "";
			}
			if (air >= Config.MaxAir)
			{
				return "";
			}
			int repeat = air; //(int)Math.Round((double)(air/Config.MaxAir) * 10);
			return ("%9" + new string('♥', repeat));
		}
		void SendPlayerGui(Player p)
		{
			p.SendCpeMessage(CpeMessageType.Status1, GetHealthBar	(GetHealth	(p)));
			p.SendCpeMessage(CpeMessageType.Status2, GetAirBar		(GetAir		(p)));
			
			//p.Message("%c" + GetHealth(p).ToString() + " " + ((GetHealth(p)/Config.MaxHealth) * 100).ToString() + " " +GetHealthBar(GetHealth(p)));
			///p.Message("%9" + GetAir(p).ToString()    + " " + ((GetAir(p)/Config.MaxAir) * 100).ToString() + " " + GetAirBar(GetAir(p)));
		}
		///////////////////////////////////////////////////////////////////////////
		
		// UTILITIES
		///////////////////////////////////////////////////////////////////////////
		public void SetHealth(Player p, int health)
		{
			p.Extras["SURVIVAL_HEALTH"] = health;
		}
		public int GetHealth(Player p)
		{
			return p.Extras.GetInt("SURVIVAL_HEALTH");
		}
		public int GetAir(Player p)
		{
			return p.Extras.GetInt("SURVIVAL_AIR");
		}
		public void SetAir(Player p, int air)
		{
			p.Extras["SURVIVAL_AIR"] = air;
		}
		bool IsDrowning(Player p)
		{
			ushort x = (ushort)(p.Pos.X / 32);
			ushort y = (ushort)(((p.Pos.Y - Entities.CharacterHeight) / 32));
			ushort z = (ushort)(p.Pos.Z / 32);
			bool drowning = false;
			try
			{
				drowning = (p.level.FastGetBlock((ushort)x, (ushort)y, (ushort)z) != 0) && (p.level.FastGetBlock((ushort)x, (ushort)(y+1), (ushort)z) != 0);
			}
			catch
			{
				drowning = false;
			}
			return drowning;
		}
		public void Damage(Player p, int amount, BlockID reason = 0)
		{
			SetHealth(p, GetHealth(p) - amount);
			if (GetHealth(p) <= 0)
			{
				// die
				Die(p, reason);
			}
			SendPlayerGui(p);
		}
		public void Die(Player p, BlockID reason = 4)
		{
			p.HandleDeath(reason, immediate: true);	
			InitPlayer(p);
		}
		///////////////////////////////////////////////////////////////////////////
		public static bool inSafeZone(Player p, string map)
        {
			return true;
            /*if (File.Exists(Config.Path + "safezones" + map + ".txt"))
            {
                using (var r = new StreamReader(Config.Path + "safezones" + map + ".txt"))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        string[] temp = line.Split(';');
                        string[] coord1 = temp[0].Split(',');
                        string[] coord2 = temp[1].Split(',');

                        if ((p.Pos.BlockX <= int.Parse(coord1[0]) && p.Pos.BlockX >= int.Parse(coord2[0])) || (p.Pos.BlockX >= int.Parse(coord1[0]) && p.Pos.BlockX <= int.Parse(coord2[0])))
                        {
                            if ((p.Pos.BlockZ <= int.Parse(coord1[2]) && p.Pos.BlockZ >= int.Parse(coord2[2])) || (p.Pos.BlockZ >= int.Parse(coord1[2]) && p.Pos.BlockZ <= int.Parse(coord2[2])))
                            {
                                if ((p.Pos.BlockY <= int.Parse(coord1[1]) && p.Pos.BlockY >= int.Parse(coord2[1])) || (p.Pos.BlockY >= int.Parse(coord1[1]) && p.Pos.BlockY <= int.Parse(coord2[1])))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    return false;
                }
            }
            return false;*/
        }
	}
}