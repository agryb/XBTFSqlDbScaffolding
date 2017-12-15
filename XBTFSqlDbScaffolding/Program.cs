using System.IO;
using Newtonsoft.Json;

namespace XBTFSqlDbScaffolding
{
    class Program
    {

        static void Main(string[] args)
        {
            var configPath = args?.Length > 0 ? args[0] : "settings.json";
            var str = File.ReadAllText(configPath);
            var settings = JsonConvert.DeserializeObject<ScaffolderSettings>(str);
            var s = new Scaffolder(settings);
            s.Generate().Wait();
        }
    }
}