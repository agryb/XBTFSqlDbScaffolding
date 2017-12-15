namespace XBTFSqlDbScaffolding.BO
{
    internal class SqlTableConstraint
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Column { get; set; }


        public SqlTableConstraint(string name, string type, string column)
        {
            Name = name;
            Type = type;
            Column = column;
        }
    }
}
