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

        public override Version Version => new Version(1, 1, 0);

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
            Commands.ChatCommands.Add(new Command("gungame.info", GGHelp, "infogg"));
            Commands.ChatCommands.Add(new Command("gungame.info", lazyvoidname, "helpgg"));
            Commands.ChatCommands.Add(new Command("gungame.setpos", SetPos, "setposgg"));
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
                            list[killerid]++;
                            Itemswitch(killerid);
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
                if (Config.weaponList.Count == 0)
                {
                    args.Player.SendErrorMessage("Weapon list is invalid.");
                    return;
                }
                foreach (int i in Config.weaponList)
                {
                    if (i > 3928 || i < 0)
                    {
                        args.Player.SendErrorMessage("Weapon list is invalid.");
                        return;
                    }
                }

                if (Config.armor)
                {
                    if (Config.armorList.Count % 3 != 0)
                    {
                        args.Player.SendErrorMessage("Armor list is invalid, list must contain a multiple of 3. Use 0 for empty slots.");
                        return;
                    }
                    if (Config.armorList.Count == 0)
                    {
                        args.Player.SendErrorMessage("Armor list is invalid, no items found.");
                        return;
                    }
                    foreach (int i in Config.armorList)
                    {
                        if (i > 3928 || i < 0)
                        {
                            args.Player.SendErrorMessage("Armor list is invalid.");
                            return;
                        }

                    }
                }

                if (Config.ammo)
                {
                    if (Config.ammoList.Count == 0)
                    {
                        args.Player.SendErrorMessage("Ammo list is invalid.");
                        return;
                    }
                    foreach(int i in Config.ammoList)
                    {
                        if (i > 3928 || i < 0)
                        {
                            args.Player.SendErrorMessage("Ammo list is invalid.");
                            return;
                        }
                    }
                }

                if (Config.accessory)
                {
                    if (Config.accessoryList.Count % 5 != 0)
                    {
                        args.Player.SendErrorMessage("Accessory list is invalid, list must contain a multiple of 5. Use 0 for empty slots.");
                        return;
                    }
                    if (Config.accessoryList.Count == 0)
                    {
                        args.Player.SendErrorMessage("Accessory list is invalid, no items found.");
                        return;
                    }
                    foreach (int i in Config.accessoryList)
                    {
                        if (i > 3928 || i < 0)
                        {
                            args.Player.SendErrorMessage("Accessory list is invalid.");
                            return;
                        }
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
                for (int i = 0; i != 50; i++)
                {
                    args.Player.TPlayer.inventory[i] = TShock.Utils.GetItemById(0);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, i);
                }
                args.Player.TPlayer.inventory[58] = TShock.Utils.GetItemById(0);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 58);

                args.Player.TPlayer.inventory[0] = TShock.Utils.GetItemById(Config.weaponList[0]);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 0);

                #region Stuff
                if (Config.armor)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        TShock.Players[args.Player.Index].TPlayer.armor[i] = TShock.Utils.GetItemById(0);
                        TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, i);
                    }
                    for (int i = 10; i < 13; i++)
                    {
                        TShock.Players[args.Player.Index].TPlayer.armor[i] = TShock.Utils.GetItemById(0);
                        TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, i);
                    }

                    TShock.Players[args.Player.Index].TPlayer.armor[0] = TShock.Utils.GetItemById(Config.armorList[0]);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 59);

                    TShock.Players[args.Player.Index].TPlayer.armor[1] = TShock.Utils.GetItemById(Config.armorList[1]);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 60);

                    TShock.Players[args.Player.Index].TPlayer.armor[2] = TShock.Utils.GetItemById(Config.armorList[2]);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 61);                    
                }
                if (Config.ammo)
                {
                    for (int i = 54; i < 58; i++)
                    {
                        TShock.Players[args.Player.Index].TPlayer.inventory[i] = TShock.Utils.GetItemById(0);
                        TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, i);
                        Item itetemp = TShock.Utils.GetItemById(Config.ammoList[0]);
                        itetemp.stack = 999;
                        TShock.Players[args.Player.Index].TPlayer.inventory[i] = itetemp;
                        TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, i);
                    }
                }
                if (Config.accessory)
                {
                    for (int i = 3; i < 10; i++)
                    {
                        TShock.Players[args.Player.Index].TPlayer.armor[i] = TShock.Utils.GetItemById(0);
                        TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, i);
                    }
                    for (int i = 13; i < 20; i++)
                    {
                        TShock.Players[args.Player.Index].TPlayer.armor[i] = TShock.Utils.GetItemById(0);
                        TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, i);
                    }
                    TShock.Players[args.Player.Index].TPlayer.armor[3] = TShock.Utils.GetItemById(Config.accessoryList[0]);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 62);

                    TShock.Players[args.Player.Index].TPlayer.armor[4] = TShock.Utils.GetItemById(Config.accessoryList[1]);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 63);

                    TShock.Players[args.Player.Index].TPlayer.armor[5] = TShock.Utils.GetItemById(Config.accessoryList[2]);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 64);

                    TShock.Players[args.Player.Index].TPlayer.armor[6] = TShock.Utils.GetItemById(Config.accessoryList[3]);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 65);

                    TShock.Players[args.Player.Index].TPlayer.armor[7] = TShock.Utils.GetItemById(Config.accessoryList[4]);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, args.Player.Index, 66);
                }
                #endregion

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
                    list[args.Player.Index] = 0;

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
                        Config.Write(Config.ConfigPath);
                        break;
                    case ("red"):
                        Config.xr = args.Player.X;
                        Config.yr = args.Player.Y;
                        args.Player.SendMessage("Successfully set red spawn point.", Color.Red);
                        Config.Write(Config.ConfigPath);
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
            string printlist = "";
            if (Config.armor)
            {
                args.Player.SendMessage("Armor switching is enabled.", Color.BlueViolet);
            }
            else
            {
                args.Player.SendMessage("Armor switching is disabled.", Color.BlueViolet);
            }
            if (Config.ammo)
            {
                args.Player.SendMessage("Ammo switching is enabled.", Color.GreenYellow);
            }
            else
            {
                args.Player.SendMessage("Ammo switching is disabled.", Color.GreenYellow);
            }
            if (Config.accessory)
            {
                args.Player.SendMessage("Accessory switching is enabled.", Color.OrangeRed);
            }
            else
            {
                args.Player.SendMessage("Accessory switching is disabled.", Color.OrangeRed);
            }
            args.Player.SendMessage($"Blue spawn position: {Config.xb.ToString()}, {Config.yb.ToString()}", Color.Yellow);
            args.Player.SendMessage($"Red spawn position: {Config.xr.ToString()}, {Config.yr.ToString()}", Color.Yellow);
            foreach (int i in Config.weaponList)
            {
                Item temp = TShock.Utils.GetItemById(i);
                printlist = printlist + ", " + temp.Name;
            }
            printlist = printlist.TrimStart(',');
            args.Player.SendMessage("Current weapon list:" + printlist, Color.Yellow);
            if (Config.armor)
            {
                printlist = "";
                foreach (int i in Config.armorList)
                {
                    if (i == 0)
                    {
                        printlist = printlist + ", " + "/";
                    }
                    else
                    {
                        Item temp = TShock.Utils.GetItemById(i);
                        printlist = printlist + ", " + temp.Name;
                    }
                }
                printlist = printlist.TrimStart(',');
                args.Player.SendMessage("Current armor list:" + printlist, Color.BlueViolet);
            }
            if (Config.ammo)
            {
                printlist = "";
                foreach (int i in Config.ammoList)
                {
                    Item temp = TShock.Utils.GetItemById(i);
                    printlist = printlist + ", " + temp.Name;
                }
                printlist = printlist.TrimStart(',');
                args.Player.SendMessage("Current ammo list:" + printlist, Color.GreenYellow);
            }
            if (Config.accessory)
            {
                printlist = "";
                foreach (int i in Config.accessoryList)
                {
                    if (i == 0)
                    {
                        printlist = printlist + ", " + "/";
                    }
                    else
                    {
                        Item temp = TShock.Utils.GetItemById(i);
                        printlist = printlist + ", " + temp.Name;
                    }
                }
                printlist = printlist.TrimStart(',');
                args.Player.SendMessage("Current accessory list:" + printlist, Color.OrangeRed);
            }
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
                                        Config.Write(Config.ConfigPath);
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
                                        Config.Write(Config.ConfigPath);
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

                    case ("ammolist"):
                        if (args.Parameters.Count > 1)
                        {
                            string weplist = args.Parameters[1].ToLower();
                            switch (weplist)
                            {
                                case ("add"):
                                    if (args.Parameters.Count > 2 && int.TryParse(args.Parameters[2], out int weplistadd))
                                    {
                                        Config.weaponList.Add(weplistadd);
                                        Item temp = TShock.Utils.GetItemById(weplistadd);
                                        args.Player.SendMessage($"Successfully added {temp.Name} to the ammo list.", Color.Yellow);
                                        Config.Write(Config.ConfigPath);
                                    }
                                    else
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg ammolist add (item id).");
                                    }
                                    break;
                                case ("del"):
                                    if (args.Parameters.Count == 2)
                                    {
                                        string weapons = "";
                                        foreach (int i in Config.ammoList)
                                        {
                                            Item tempite = TShock.Utils.GetItemById(i);
                                            weapons = weapons + ", " + tempite.Name;
                                        }
                                        weapons = weapons.TrimStart(',');
                                        args.Player.SendMessage("Current ammo list: " + weapons, Color.Yellow);
                                        args.Player.SendMessage("Type /configg ammolist del (item pos. in list) to remove it from the list.", Color.Yellow);
                                    }
                                    else if (int.TryParse(args.Parameters[2], out int weplistdel))
                                    {
                                        weplistdel = weplistdel - 1;
                                        if (weplistdel > Config.ammoList.Count || weplistdel < 0)
                                        {
                                            args.Player.SendErrorMessage("Invalid position!");
                                            return;
                                        }
                                        Item tempite = TShock.Utils.GetItemById(Config.ammoList[weplistdel]);
                                        Config.ammoList.RemoveAt(weplistdel);
                                        args.Player.SendMessage($"Successfully removed {tempite.Name} from the ammo list.", Color.Yellow);
                                        Config.Write(Config.ConfigPath);
                                    }
                                    else
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg ammolist del (item pos. in list).");
                                    }
                                    break;
                                default:
                                    args.Player.SendErrorMessage("Syntax error. Expected /configg ammolist 'add' or 'del'.");
                                    break;
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage("Syntax error. Expected /configg ammolist 'add' or 'del'.");
                        }
                        break;

                    case ("armorlist"):
                        if (args.Parameters.Count > 1)
                        {
                            string weplist = args.Parameters[1].ToLower();
                            switch (weplist)
                            {
                                case ("add"):
                                    if (args.Parameters.Count > 2 && int.TryParse(args.Parameters[2], out int weplistadd))
                                    {
                                        Config.armorList.Add(weplistadd);
                                        Item temp = TShock.Utils.GetItemById(weplistadd);
                                        args.Player.SendMessage($"Successfully added {temp.Name} to the armor list.", Color.Yellow);
                                        Config.Write(Config.ConfigPath);
                                    }
                                    else
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg armorlist add (item id).");
                                    }
                                    break;
                                case ("del"):
                                    if (args.Parameters.Count == 2)
                                    {
                                        string weapons = "";
                                        foreach (int i in Config.armorList)
                                        {
                                            Item tempite = TShock.Utils.GetItemById(i);
                                            weapons = weapons + ", " + tempite.Name;
                                        }
                                        weapons = weapons.TrimStart(',');
                                        args.Player.SendMessage("Current armor list: " + weapons, Color.Yellow);
                                        args.Player.SendMessage("Type /configg armorlist del (item pos. in list) to remove it from the list.", Color.Yellow);
                                    }
                                    else if (int.TryParse(args.Parameters[2], out int weplistdel))
                                    {
                                        weplistdel = weplistdel - 1;
                                        if (weplistdel > Config.armorList.Count || weplistdel < 0)
                                        {
                                            args.Player.SendErrorMessage("Invalid position!");
                                            return;
                                        }
                                        Item tempite = TShock.Utils.GetItemById(Config.armorList[weplistdel]);
                                        Config.armorList.RemoveAt(weplistdel);
                                        args.Player.SendMessage($"Successfully removed {tempite.Name} from the armor list.", Color.Yellow);
                                        Config.Write(Config.ConfigPath);
                                    }
                                    else
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg armorlist del (item pos. in list).");
                                    }
                                    break;
                                default:
                                    args.Player.SendErrorMessage("Syntax error. Expected /configg armorlist 'add' or 'del'.");
                                    break;
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage("Syntax error. Expected /configg armorlist 'add' or 'del'.");
                        }
                        break;

                    case ("accessorylist"):
                        if (args.Parameters.Count > 1)
                        {
                            string weplist = args.Parameters[1].ToLower();
                            switch (weplist)
                            {
                                case ("add"):
                                    if (args.Parameters.Count > 2 && int.TryParse(args.Parameters[2], out int weplistadd))
                                    {
                                        Config.accessoryList.Add(weplistadd);
                                        Item temp = TShock.Utils.GetItemById(weplistadd);
                                        args.Player.SendMessage($"Successfully added {temp.Name} to the accessory list.", Color.Yellow);
                                        Config.Write(Config.ConfigPath);
                                    }
                                    else
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg accessorylist add (item id).");
                                    }
                                    break;
                                case ("del"):
                                    if (args.Parameters.Count == 2)
                                    {
                                        string weapons = "";
                                        foreach (int i in Config.accessoryList)
                                        {
                                            Item tempite = TShock.Utils.GetItemById(i);
                                            weapons = weapons + ", " + tempite.Name;
                                        }
                                        weapons = weapons.TrimStart(',');
                                        args.Player.SendMessage("Current accessory list: " + weapons, Color.Yellow);
                                        args.Player.SendMessage("Type /configg accessorylist del (item pos. in list) to remove it from the list.", Color.Yellow);
                                    }
                                    else if (int.TryParse(args.Parameters[2], out int weplistdel))
                                    {
                                        weplistdel = weplistdel - 1;
                                        if (weplistdel > Config.accessoryList.Count || weplistdel < 0)
                                        {
                                            args.Player.SendErrorMessage("Invalid position!");
                                            return;
                                        }
                                        Item tempite = TShock.Utils.GetItemById(Config.accessoryList[weplistdel]);
                                        Config.accessoryList.RemoveAt(weplistdel);
                                        args.Player.SendMessage($"Successfully removed {tempite.Name} from the accessory list.", Color.Yellow);
                                        Config.Write(Config.ConfigPath);
                                    }
                                    else
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg acessorylist del (item pos. in list).");
                                    }
                                    break;
                                default:
                                    args.Player.SendErrorMessage("Syntax error. Expected /configg accessorylist 'add' or 'del'.");
                                    break;
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage("Syntax error. Expected /configg accessorylist 'add' or 'del'.");
                        }
                        break;



                    case ("killsrequired"):
                        if (args.Parameters.Count == 2 && int.TryParse(args.Parameters[1], out Config.killsRequired))
                        {
                            args.Player.SendMessage("Successfully changed kills required to " + Config.killsRequired.ToString() + ".", Color.Yellow);
                            Config.Write(Config.ConfigPath);
                        }
                        else
                        {
                            args.Player.SendErrorMessage("Syntax error. Expected /configg killsrequired (number of kills).");
                        }
                        break;
                    case ("setpos"):
                        if (args.Parameters.Count > 1)
                        {
                            string setpos = args.Parameters[1].ToLower();
                            switch(setpos)
                            {
                                case ("red"):
                                    if (args.Parameters.Count < 4)
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg setpos red (x) (y)");
                                        return;
                                    }
                                    else
                                    {
                                        if (!float.TryParse(args.Parameters[2], out Config.xr) || !float.TryParse(args.Parameters[3], out Config.yr))
                                        {
                                            args.Player.SendErrorMessage("Syntax error. Expected /configg setpos red (x) (y)");
                                            return;
                                        }
                                        Config.xr = int.Parse(args.Parameters[2]) * 16;
                                        Config.yr = int.Parse(args.Parameters[3]) * 16;
                                        args.Player.SendMessage("Successfully changed red spawn position to " + Config.xr.ToString() + ", " + Config.yr.ToString() + ".", Color.Yellow);
                                        Config.Write(Config.ConfigPath);
                                    }
                                    break;
                                case ("blue"):
                                    if (args.Parameters.Count < 4)
                                    {
                                        args.Player.SendErrorMessage("Syntax error. Expected /configg setpos blue (x) (y)");
                                        return;
                                    }
                                    else
                                    {
                                        if (!float.TryParse(args.Parameters[2], out Config.xb) || !float.TryParse(args.Parameters[3], out Config.yb))
                                        {
                                            args.Player.SendErrorMessage("Syntax error. Expected /configg setpos blue (x) (y)");
                                            return;
                                        }
                                        Config.xb = int.Parse(args.Parameters[2]) * 16;
                                        Config.yb = int.Parse(args.Parameters[3]) * 16;
                                        args.Player.SendMessage("Successfully changed blue spawn position to " + Config.xb.ToString() + ", " + Config.yb.ToString() + ".", Color.Yellow);
                                        Config.Write(Config.ConfigPath);
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

                    case ("toggle"):
                        if (args.Parameters.Count == 2)
                        {
                            string shutup = args.Parameters[1];
                            switch(shutup)
                            {
                                case ("ammo"):
                                    if (Config.ammo)
                                    {
                                        args.Player.SendMessage("Ammo switching is now disabled.", Color.Yellow);
                                    }
                                    else
                                    {
                                        args.Player.SendMessage("Ammo switching is now enabled.", Color.Yellow);
                                    }
                                    Config.ammo = !Config.ammo;
                                    Config.Write(Config.ConfigPath);
                                    break;
                                case ("armor"):
                                    if (Config.armor)
                                    {
                                        args.Player.SendMessage("Armor switching is now disabled.", Color.Yellow);
                                    }
                                    else
                                    {
                                        args.Player.SendMessage("Armor switching is now enabled.", Color.Yellow);
                                    }
                                    Config.armor = !Config.armor;
                                    Config.Write(Config.ConfigPath);
                                    break;
                                case ("accessory"):
                                    if (Config.accessory)
                                    {
                                        args.Player.SendMessage("Accessory switching is now disabled.", Color.Yellow);
                                    }
                                    else
                                    {
                                        args.Player.SendMessage("Accessory switching is now enabled.", Color.Yellow);
                                    }
                                    Config.accessory = !Config.accessory;
                                    Config.Write(Config.ConfigPath);
                                    break;
                                default:
                                    args.Player.SendErrorMessage("Syntax error. Expected /configg toggle 'ammo', 'armor' or 'accessory'.");
                                    break;
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage("Syntax error. Expected /configg toggle 'ammo', 'armor' or 'accessory'.");
                        }
                        break;

                    default:
                        args.Player.SendErrorMessage("Syntax error. Expected /configg 'weaponlist', 'armorlist', 'ammolist', 'accessorylist', 'killsrequired' or 'setpos'.");
                        break;
                }
            }
            else
            {
                args.Player.SendErrorMessage("Syntax error. Expected /configg 'weaponlist', 'killsrequired', 'toggle' or 'setpos'.");
            }
        }
        
        void lazyvoidname(CommandArgs args)
        {
            args.Player.SendMessage("Gun Game is a plugin based off the Gun Game mode from CoD.", Color.Yellow);
            args.Player.SendMessage("In essence, players would work their way up through a series of weapons by getting kills, usually starting at something weak.", Color.Yellow);
            args.Player.SendMessage("This plugin lets you do all that, as well as cycle through armor, accessories and ammo. Other equipment soon.", Color.Yellow);
            args.Player.SendMessage("Armor/accessory lists will have to contain a multiple of 3 or 5 respectively. Use a 0 for empty slots.", Color.Yellow);

        }

        void Victory(int pindex)
        {
            game = false;
            TShock.Utils.Broadcast(TShock.Players[pindex].Name + " has won a Gun Game!", Color.Yellow);
            foreach (int i in playersingame)
            {
                TShock.Players[i].TPlayer.team = 0;
                Main.player[i].hostile = false;
                TSPlayer.All.SendData(PacketTypes.PlayerTeam, null, i, 0);
                TSPlayer.All.SendData(PacketTypes.TogglePvp, null, i, 0);
            }
        }

        void Itemswitch(int killerid)
        {
            for (int i = 0; i < 50; i++)
            {
                TShock.Players[killerid].TPlayer.inventory[i] = TShock.Utils.GetItemById(0);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, i);
            }

            TShock.Players[killerid].TPlayer.inventory[58] = TShock.Utils.GetItemById(0);
            TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 58);

            TShock.Players[killerid].TPlayer.inventory[0] = TShock.Utils.GetItemById(Config.weaponList[list[killerid]]);
            TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 0);

            TShock.Players[killerid].SendMessage("Your current weapon is: " + TShock.Utils.GetItemById(Config.weaponList[list[killerid]]).Name, Color.Yellow);

            if (Config.armor)
            {
                for (int i = 0; i < 3; i++)
                {
                    TShock.Players[killerid].TPlayer.armor[i] = TShock.Utils.GetItemById(0);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, i);
                }
                for (int i = 10; i < 13; i++)
                {
                    TShock.Players[killerid].TPlayer.armor[i] = TShock.Utils.GetItemById(0);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, i);
                }

                TShock.Players[killerid].TPlayer.armor[0] = TShock.Utils.GetItemById(Config.armorList[list[killerid] * 3]);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 59);

                TShock.Players[killerid].TPlayer.armor[1] = TShock.Utils.GetItemById(Config.armorList[list[killerid] * 3 + 1]);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 60);

                TShock.Players[killerid].TPlayer.armor[2] = TShock.Utils.GetItemById(Config.armorList[list[killerid] * 3 + 2]);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 61);

                TShock.Players[killerid].SendMessage("Your current armor set is: " + TShock.Utils.GetItemById(Config.armorList[list[killerid] * 3]).Name + ", " + TShock.Utils.GetItemById(Config.armorList[list[killerid] * 3 + 1]).Name + ", " + TShock.Utils.GetItemById(Config.armorList[list[killerid] * 3 + 2]).Name, Color.Yellow);
            }
            if (Config.ammo)
            {
                for (int i = 54; i < 58; i++)
                {
                    TShock.Players[killerid].TPlayer.inventory[i] = TShock.Utils.GetItemById(0);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, i);
                    Item itetemp = TShock.Utils.GetItemById(Config.ammoList[list[killerid]]);
                    itetemp.stack = 999;
                    TShock.Players[killerid].TPlayer.inventory[i] = itetemp;
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, i);
                }
                TShock.Players[killerid].SendMessage("Your current bullets are: " + TShock.Utils.GetItemById(Config.ammoList[list[killerid]]).Name, Color.Yellow);
            }
            if (Config.accessory)
            {
                for (int i = 3; i < 11; i++)
                {
                    TShock.Players[killerid].TPlayer.armor[i] = TShock.Utils.GetItemById(0);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, i);
                }
                for (int i = 13; i < 21; i++)
                {
                    TShock.Players[killerid].TPlayer.armor[i] = TShock.Utils.GetItemById(0);
                    TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, i);
                }
                TShock.Players[killerid].TPlayer.armor[3] = TShock.Utils.GetItemById(Config.accessoryList[list[killerid] * 5]);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 62);

                TShock.Players[killerid].TPlayer.armor[4] = TShock.Utils.GetItemById(Config.accessoryList[list[killerid] * 5 + 1]);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 63);

                TShock.Players[killerid].TPlayer.armor[5] = TShock.Utils.GetItemById(Config.accessoryList[list[killerid] * 5 + 2]);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 64);

                TShock.Players[killerid].TPlayer.armor[6] = TShock.Utils.GetItemById(Config.accessoryList[list[killerid] * 5 + 3]);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 65);

                TShock.Players[killerid].TPlayer.armor[7] = TShock.Utils.GetItemById(Config.accessoryList[list[killerid] * 5 + 4]);
                TSPlayer.All.SendData(PacketTypes.PlayerSlot, null, killerid, 66);

                TShock.Players[killerid].SendMessage("Your current accessories are: " + TShock.Utils.GetItemById(Config.accessoryList[list[killerid] * 5]).Name + ", " + TShock.Utils.GetItemById(Config.accessoryList[list[killerid] * 5 + 1]).Name + ", " + TShock.Utils.GetItemById(Config.accessoryList[list[killerid] * 5 + 2]).Name + ", " + TShock.Utils.GetItemById(Config.accessoryList[list[killerid] * 5 + 3]).Name + ", " + TShock.Utils.GetItemById(Config.accessoryList[list[killerid] * 5 + 4]).Name, Color.Yellow);
            }
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
