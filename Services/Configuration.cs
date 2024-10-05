using Newtonsoft.Json;
using System;

namespace UncoverGamesExporter.Services
{
    class Config
    {
        public string token;
        public string refresh_token;
        public string expiry;
    }

    internal class Configuration
    {
        private static readonly Configuration instance = new Configuration();
        string configPath;

        public static Configuration GetInstance()
        {
            return instance;
        }

        public void SetPluginDataPath(string pluginDataPath)
        {
            configPath = pluginDataPath + "\\config.txt";
        }

        public string GetToken()
        {
            Config config = LoadConfig();
            return Decode(config.token);
        }

        public void SetToken(string value)
        {
            Config config = LoadConfig();
            config.token = Encode(value);
            SaveConfig(config);
        }
        public string GetRefreshToken()
        {
            Config config = LoadConfig();
            return Decode(config.refresh_token);
        }

        public void SetRefreshToken(string value)
        {
            Config config = LoadConfig();
            config.refresh_token = Encode(value);
            SaveConfig(config);
        }

        public void SetExpiresIn(int expiresIn)
        {
            Config config = LoadConfig();
            config.expiry = Encode(DateTime.Now.AddSeconds(expiresIn)
                .ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            SaveConfig(config);
        }

        public bool HasExpired()
        {
            Config config = LoadConfig();
            if (config.expiry == null) {
                return true;
            }
            DateTime expiry = DateTime.ParseExact(config.expiry, "o", System.Globalization.CultureInfo.InvariantCulture);
            return DateTime.Now.AddDays(-1) >= expiry;
        }

        public bool IsConfigured()
        {
            Config config = LoadConfig();
            return config.expiry != null;
        }

        private string Encode(string value)
        {
            // TODO
            return value;
        }

        private string Decode(string value)
        {
            // TODO
            return value;
        }

        private Config LoadConfig()
        {
            if (!System.IO.File.Exists(configPath))
            {
                return new Config();
            }
            string contents = System.IO.File.ReadAllText(configPath);
            return JsonConvert.DeserializeObject<Config>(contents);
        }

        private void SaveConfig(Config config)
        {
            string configJson = JsonConvert.SerializeObject(config);
            // TODO Consider encrypting the entire file
            System.IO.File.WriteAllText(configPath, configJson);
        }
    }
}
