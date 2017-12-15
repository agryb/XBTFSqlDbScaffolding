namespace XBTFSqlDbScaffolding
{
    public class ScaffolderSettings
    {
        public string ConnectionString { get; set; }
        public string DomainName { get; set; }
        public string DefaultNamespace { get; set; }
        public string OutputDir { get; set; }

        public string[] StaticDataTables { get; set; }
        public string[] ExcludedTables { get; set; }

        public string EnumNamespace { get; set; }
        public EnumColumnMapping[] EnumColumnMapping { get; set; }

    }

    public class EnumColumnMapping
    {
        public string Entity { get; set; }
        public string Prop { get; set; }
        public string Enum { get; set; }
    }
}
