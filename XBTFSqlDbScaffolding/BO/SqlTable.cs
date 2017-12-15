using System.Collections.Generic;
using System.Linq;

namespace XBTFSqlDbScaffolding.BO
{
    internal class SqlTable
    {
        private const string IdColumnName = "Id";


        public string Name { get; set; }
        public string Schema { get; set; }
        public List<SqlTableColumn> Columns { get; } = new List<SqlTableColumn>();
        public List<SqlTableConstraint> Constraints { get; } = new List<SqlTableConstraint>();
        public List<SqlTableForeignKey> FKs { get; } = new List<SqlTableForeignKey>();
        public List<SqlTable> Collections { get; } = new List<SqlTable>();

        public List<SqlTableColumn> ColumnsExceptId => Columns.Where(w => w.Name != IdColumnName).ToList();

        public string PkColumnName => Constraints
            .Where(w => w.Type == "PRIMARY KEY")
            .Select(s => s.Column)
            .FirstOrDefault();

        public SqlTable(string name, string schema)
        {
            Name = name;
            Schema = schema;
        }
    }
}
