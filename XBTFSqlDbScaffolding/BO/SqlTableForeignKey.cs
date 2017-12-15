namespace XBTFSqlDbScaffolding.BO
{
    internal class SqlTableForeignKey
    {
        public string Name { get; set; }
        public string FkTable { get; set; }
        public string FkColumn { get; set; }
        public string PkTable { get; set; }
        public string PkColumn { get; set; }

        public SqlTableForeignKey(string name, 
            string fkTable, string fkColumn, 
            string pkTable, string pkColumn)
        {
            Name = name;
            FkTable = fkTable;
            FkColumn = fkColumn;
            PkTable = pkTable;
            PkColumn = pkColumn;
        }
    }
}
