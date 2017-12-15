using System.IO;
using System.Reflection;

namespace XBTFSqlDbScaffolding
{
    internal class Helpers
    {
        public static string ReadTemplateFromResource(string templateFileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"XBTFSqlDbScaffolding.Templates.{templateFileName}";
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
