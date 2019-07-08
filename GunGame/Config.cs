using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TShockAPI;
using System.IO;
using Newtonsoft.Json;

namespace GunGame
{
    public class Config
    {
        public int killsRequired;
        public List<int> weaponList = new List<int>()
        {
            
        };
        
        public float xr;
        public float xb;
        public float yr;
        public float yb;

        #region read + write
        public static string ConfigPath = Path.Combine(TShock.SavePath, "gungame.json");
        private static void Write(Config file)
        {
            try
            {
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(file, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception at 'Config.Write': {0}\nCheck logs for details.",
                        ex.Message);
                Console.WriteLine(ex.ToString());
            }
        }

        public static Config Read()
        {

            Config file = new Config();
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Write(file);
                }
                else
                {
                    file = JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigPath));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception at 'Config.Read': {0}\nCheck logs for details.",
                        ex.Message);
                Console.WriteLine(ex.ToString());
            }
            return file;
        }
        #endregion
    }
    

}
