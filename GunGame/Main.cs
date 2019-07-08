using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;

namespace GunGame
{
    [ApiVersion(2, 1)]
    public class GunGame : TerrariaPlugin
    {

        public override string Author => "Nyanko";

        public override string Description => " ";

        public override string Name => "Gun Game";

        public override Version Version => new Version(1, 0, 0);

        public static int[] list;

        public static int[] kills;

        public static Config Config { get; private set; }

        public GunGame(Main game) : base(game)
        {

        }

        short killerid;
        bool game = false;
        int players = 0;
        List<int> playersingame = new List<int>();

        public override void Initialize()
        {
            #region commands
            Commands.ChatCommands.Add(new Command("gungame.start", StartGame, "startgg"));
            Commands.ChatCommands.Add(new Command("gungame.join", JoinGame, "joingg"));
            Commands.ChatCommands.Add(new Command("gungame.join", LeaveGame, "leavegg"));
            Commands.ChatCommands.Add(new Command("gungame.setpos", SetPos, "setposgg"));
            Commands.ChatCommands.Add(new Command("gungame.info", GGHelp, "infogg"));
            Commands.ChatCommands.Add(new Command("gungame.config", GGConfig, "configg"));
            #endregion
            list = new int[256];
            kills = new int[256];
            ServerApi.Hooks.NetGetData.Register(this, getdad);
            Config = Config.Read();
        }

        void getdad(GetDataEventArgs args)
        {
            if (!args.Handled && args.MsgID == PacketTypes.PlayerSpawn && playersingame.Contains(args.Msg.whoAmI))
            {
                if (TShock.Players[args.Msg.whoAmI].Team == 1)
                {
                    TShock.Players[args.Msg.whoAmI].Teleport(Config.xr, Config.yr, 1);
                }
                else if (TShock.Players[args.Msg.whoAmI].Team == 3)
                {
                    TShock.Players[args.Msg.whoAmI].Teleport(Config.xb, Config.yb, 1);
                }
            }

            if (!args.Handled && args.MsgID == PacketTypes.PlayerDeathV2 && playersingame.Contains(args.Msg.whoAmI))
            {
                using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
                {
                    short playerid = reader.ReadByte();
                    if (!playersingame.Contains(playerid))
                    {
                        return;
                    }
                    short deathtype = reader.ReadByte();
                    if ((deathtype & 1) != 0)
                    {
                        killerid = reader.ReadInt16();

                        kills[killerid]++;
                        if (kills[killerid] < Config.killsRequired)
                        {
                            TShock.Players[args.Msg.whoAmI].SendMessage("You are " + (Config.killsRequired - kills[killerid]).ToString() + " kills away from the next weapon.", Color.Yellow);
                            return;
                        }

                        if (list[killerid] != Config.weaponList.Count)
                        {

                            Main.ServerSideCharacter = true;
                            NetMessage.SendData((int)PacketTypes.WorldInfo, killerid);

                            for (int i = 0; i != 50; i++)
                            {
                                TShock.Players[killerid].TPlayer.inventory[i] = TShock.Utils.GetItemById(0);
                                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, i);
                            }
                            TShock.Players[killerid].TPlayer.inventory[58] = TShock.Utils.GetItemById(0);
                            TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 58);

                            TShock.Players[killerid].TPlayer.inventory[0] = TShock.Utils.GetItemById(Config.weaponList[list[killerid]]);
                            TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 0);

                            list[killerid]++;
                            TShock.Players[args.Msg.whoAmI].SendMessage("Your current weapon is: " + TShock.Utils.GetItemById(Config.weaponList[list[killerid]]).Name, Color.YellowGreen);

                            Main.ServerSideCharacter = false;
                            NetMessage.SendData((int)PacketTypes.WorldInfo, killerid);
                        }
                        else
                        {
                            Victory(args.Msg.whoAmI);
                        }
                    }
                }
            }
        }

        void StartGame(CommandArgs args)
        {
            if (!Main.ServerSideCharacter)
            {
                args.Player.SendErrorMessage("Gun Game requires SSC to be enabled.");
                return;
            }
            if (game == true)
            {
                args.Player.SendErrorMessage("A game is currently running!");
                return;
            }
            else
            {
                foreach (int i in Config.weaponList)
                {
                    if (i > 3928 || Config.weaponList.Contains(0) || Config.weaponList.Count == 0)
                    {
                        args.Player.SendErrorMessage("Weapon list is invalid.");
                        return;
                    }
                }
                if (Config.xr == 0 || Config.xb == 0 || Config.yr == 0 || Config.yb == 0)
                {
                    args.Player.SendErrorMessage("Spawn point(s) are not properly defined. Use /setposgg 'blue' or 'red'.");
                    return;
                }
                if (Config.killsRequired < 1)
                {
                    args.Player.SendErrorMessage("Kills required cannot be below 1.");
                    return;
                }
                else
                {
                    args.Player.SendMessage("Successfully started a Gun Game.", Color.Yellow);
                    TShock.Utils.Broadcast("A Gun Game has been started! Type /joingg to join.", Color.Yellow);
                    game = true;
                    return;
                }
            }

        }

        void JoinGame(CommandArgs args)
        {
            if (!game)
            {
                args.Player.SendErrorMessage("No game is currently running.");
                return;
            }
            else if (playersingame.Contains(args.Player.Index))
            {
                args.Player.SendErrorMessage("You are already in the game!");
                return;
            }
            else
            {
                Main.ServerSideCharacter = true;
                NetMessage.SendData((int)PacketTypes.WorldInfo, args.Player.Index);

                for (int i = 0; i != 50; i++)
                {
                    args.Player.TPlayer.inventory[i] = TShock.Utils.GetItemById(0);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, i);
                }
                args.Player.TPlayer.inventory[58] = TShock.Utils.GetItemById(0);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 58);

                args.Player.TPlayer.inventory[0] = TShock.Utils.GetItemById(Config.weaponList[0]);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 0);

                Main.ServerSideCharacter = false;
                NetMessage.SendData((int)PacketTypes.WorldInfo, args.Player.Index);

                playersingame.Add(args.Player.Index);
                players++;
                string s = "player.";
                if (players > 1 || players == 0)
                {
                    s = "players.";
                }
                args.Player.SendMessage("Joined the game. Currently playing with " + (playersingame.Count - 1) + " other " + s, Color.Yellow);
                if (playersingame.Count % 2 == 0)
                {
                    args.Player.TPlayer.team = 1;
                    Main.player[args.Player.Index].hostile = true;
                    TSPlayer.All.SendData(PacketTypes.PlayerTeam, null, args.Player.Index, 1);
                    TSPlayer.All.SendData(PacketTypes.TogglePvp, null, args.Player.Index, 1);
                    args.Player.SendMessage("You are on red team.", Color.Yellow);
                    args.Player.Teleport(Config.xr, Config.yr, 1);
                }
                else
                {
                    args.Player.TPlayer.team = 3;
                    Main.player[args.Player.Index].hostile = true;
                    TSPlayer.All.SendData(PacketTypes.PlayerTeam, null, args.Player.Index, 3);
                    TSPlayer.All.SendData(PacketTypes.TogglePvp, null, args.Player.Index, 1);
                    args.Player.SendMessage("You are on blue team.", Color.Yellow);
                    args.Player.Teleport(Config.xb, Config.yb, 1);
                }
            }
        }

        void LeaveGame(CommandArgs args)
        {
            if (game == false)
            {
                args.Player.SendErrorMessage("No game is currently running.");
            }
            else
            {
                if (playersingame.Contains(args.Player.Index))
                {
                    playersingame.Remove(args.Player.Index);
                    Main.player[args.Player.Index].hostile = false;
                    args.Player.TPlayer.team = 0;
                    TSPlayer.All.SendData(PacketTypes.PlayerTeam, null, args.Player.Index, 0);
                    TSPlayer.All.SendData(PacketTypes.TogglePvp, null, args.Player.Index, 0);
                    args.Player.SendMessage("You have left the game.", Color.Yellow);
                    foreach (int i in playersingame)
                    {
                        TShock.Players[i].SendMessage(args.Player.Name + " has left the game.", Color.Yellow);
                    }
                }
                else
                {
                    args.Player.SendErrorMessage("You are not in the game!");
                }
            }
        }

        void SetPos(CommandArgs args)
        {
            if (args.Parameters.Count == 1)
            {
                string position = args.Parameters[0];
                switch (position)
                {
                    case ("blue"):
                        Config.xb = args.Player.X;
                        Config.yb = args.Player.Y;
                        args.Player.SendMessage("Successfully set blue spawn point.", Color.Blue);
                        break;
                    case ("red"):
                        Config.xr = args.Player.X;
                        Config.yr = args.Player.Y;
                        args.Player.SendMessage("Successfully set red spawn point.", Color.Red);
                        break;
                    default:
                        args.Player.SendErrorMessage("Syntax error. Expected /setposgg 'blue' or 'red'");
                        break;
                }

            }
            else
            {
                args.Player.SendErrorMessage("Syntax error. Expected /setposgg 'blue' or 'red'");
            }
        }

        void GGHelp(CommandArgs args)
        {
            string printweplist = "";
            args.Player.SendMessage("Gun Game plugin, designed by nyan.", Color.Yellow);
            args.Player.SendMessage($"Blue spawn position: {Config.xb.ToString()}, {Config.yb.ToString()}", Color.Yellow);
            args.Player.SendMessage($"Red spawn position: {Config.xr.ToString()}, {Config.yr.ToString()}", Color.Yellow);
            foreach (int i in Config.weaponList)
            {
                Item temp = TShock.Utils.GetItemById(i);
                printweplist = printweplist + ", " + temp.Name;
            }
            printweplist = printweplist.TrimStart(',');
            args.Player.SendMessage("Current weapon list:" + printweplist, Color.Yellow);
        }

        void GGConfig(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                string command = args.Parameters[0].ToLower();
                switch(command)
                {

                    case ("weaponlist"):
                        if (args.Parameters.Count > 1)
                        {
                            string weplist = args.Parameters[1].ToLower();
                            switch(weplist)
                            {
                                case ("add"):
                                    if (args.Parameters.Count > 2 && int.TryParse(args.Parameters[2], out int weplistadd))
                                    {
                                        Config.weaponList.Add(weplistadd);
                                        Item temp = TShock.Utils.GetItemById(weplistadd);
                                        args.Player.SendMessage($"Successfully added {temp.Name} to the weapon list.", Color.Yellow);
                                    }
                                    else
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg weaponlist add (item id).");
                                    }
                                    break;
                                case ("del"):
                                    if(args.Parameters.Count == 2)
                                    {
                                        string weapons = "";
                                        foreach(int i in Config.weaponList)
                                        {
                                            Item tempite = TShock.Utils.GetItemById(i);
                                            weapons = weapons + ", " + tempite.Name;
                                        }
                                        weapons = weapons.TrimStart(',');
                                        args.Player.SendMessage("Current weapons list: " + weapons, Color.Yellow);
                                        args.Player.SendMessage("Type /configg weaponlist del (item pos. in list) to remove it from the list.", Color.Yellow);
                                    }
                                    else if (int.TryParse(args.Parameters[2], out int weplistdel))
                                    {
                                        weplistdel = weplistdel - 1;
                                        if(weplistdel > Config.weaponList.Count || weplistdel < 0)
                                        {
                                            args.Player.SendErrorMessage("Invalid position!");
                                            return;
                                        }
                                        Item tempite = TShock.Utils.GetItemById(Config.weaponList[weplistdel]);
                                        Config.weaponList.RemoveAt(weplistdel);
                                        args.Player.SendMessage($"Successfully removed {tempite.Name} from the weapon list.", Color.Yellow);
                                    }
                                    else
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg weaponlist del (item pos. in list).");
                                    }
                                    break;
                                default:
                                    args.Player.SendErrorMessage("Syntax error. Expected /configg weaponlist 'add' or 'del'.");
                                    break;
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage("Syntax error. Expected /configg weaponlist 'add' or 'del'.");
                        }
                        break;

                    case ("killsrequired"):
                        if (args.Parameters.Count == 2 && int.TryParse(args.Parameters[1], out Config.killsRequired))
                        {
                            args.Player.SendMessage("Successfully changed kills required to " + Config.killsRequired.ToString() + ".", Color.Yellow);
                        }
                        else
                        {
                            args.Player.SendErrorMessage("Syntax error. Expected /configg killsrequired (number of kills).");
                        }
                        break;
                    case ("setpos"):
                        if (args.Parameters.Count < 1)
                        {
                            string setpos = args.Parameters[1].ToLower();
                            switch(setpos)
                            {
                                case ("red"):
                                    if (args.Parameters.Count == 4)
                                    {
                                        if (!float.TryParse(args.Parameters[2], out Config.xr) || !float.TryParse(args.Parameters[3], out Config.yr))
                                        {
                                            args.Player.SendErrorMessage("Syntax error. Expected /configg setpos red (x) (y)");
                                            return;
                                        }
                                        Config.xr = int.Parse(args.Parameters[2]) * 16;
                                        Config.yr = int.Parse(args.Parameters[3]) * 16;
                                        args.Player.SendMessage("Successfully changed red spawn position to " + Config.xr.ToString() + ", " + Config.yr.ToString() + ".", Color.Yellow);
                                    }
                                    else
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg setpos red (x) (y)");
                                        return;
                                    }
                                    break;
                                case ("blue"):
                                    if (args.Parameters.Count == 4)
                                    {
                                        if (!float.TryParse(args.Parameters[2], out Config.xb) || !float.TryParse(args.Parameters[3], out Config.yb))
                                        {
                                            args.Player.SendErrorMessage("Syntax error. Expected /configg setpos blue (x) (y)");
                                            return;
                                        }
                                        Config.xb = int.Parse(args.Parameters[2]) * 16;
                                        Config.yb = int.Parse(args.Parameters[3]) * 16;
                                        args.Player.SendMessage("Successfully changed blue spawn position to " + Config.xb.ToString() + ", " + Config.yb.ToString() + ".", Color.Yellow);                                        
                                    }
                                    else
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg setpos blue (x) (y)");
                                        return;
                                    }
                                    break;
                                default:
                                    args.Player.SendErrorMessage("Syntax error. Expected /configg setpos 'red' or 'blue'.");
                                    break;

                            }

                        }
                        else
                        {
                            args.Player.SendErrorMessage("Syntax error. Expected /configg setpos 'red' or 'blue'.");
                        }
                        break;
                    default:
                        args.Player.SendErrorMessage("Syntax error. Expected /configg 'weaponlist', 'killsrequired' or 'setpos'.");
                        break;
                }
            }
            else
            {
                args.Player.SendErrorMessage("Syntax error. Expected /configg 'weaponlist', 'killsrequired' or 'setpos'.");
            }
        }

        void Victory(int pindex)
        {
            game = false;
            TShock.Utils.Broadcast(TShock.Players[pindex].Name + " has won a Gun Game!", Color.Yellow);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, getdad);
            }
            base.Dispose(disposing);
        }
    }

}
