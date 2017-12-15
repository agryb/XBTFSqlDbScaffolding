using System.Collections.Generic;

namespace XBTFSqlDbScaffolding.BO
{
    internal class SqlTableColumn
    {
        public int OrdinalPosition { get; set; }
        public string Name { get; set; }
        public bool IsNullable { get; set; }
        public string DataType { get; set; }
        public bool IsPrimaryKey { get; set; }
        public SqlTableForeignKey FK { get; }

        public bool IsFK => FK != null;

        public SqlTableColumn(string name, int ordinalPosition, bool isNullable, string dataType, bool isPk, SqlTableForeignKey fk)
        {
            Name = name;
            OrdinalPosition = ordinalPosition;
            IsNullable = isNullable;
            DataType = dataType;
            IsPrimaryKey = isPk;
            FK = fk;
        }

        public string NetFrameworkDateType =>
            !IsNullable ? GetType(DataType) : $"Nullable<{GetType(DataType)}>";


        private static string GetType(string dataType)
            => SqlTypeNameMapper.ContainsKey(dataType) ? SqlTypeNameMapper[dataType] : dataType;


        private static readonly Dictionary<string, string> SqlTypeNameMapper = new Dictionary<string, string>
        {
            {"bigint", "Int64"},
            {"binary", "Byte[]"},
            {"bit", "Boolean"},
            {"char", "String"},
            {"date", "DateTime"},
            {"datetime", "DateTime"},
            {"datetime2", "DateTime"},
            {"datetimeoffset", "DateTimeOffset"},
            {"decimal", "Decimal"},
            {"float", "Double"},
            {"image", "Byte[]"},
            {"int", "Int32"},
            {"money", "Decimal"},
            {"nchar", "String"},
            {"ntext", "String"},
            {"numeric", "Decimal"},
            {"nvarchar", "String"},
            {"real", "Single"},
            {"rowversion", "Byte[]"},
            {"smalldatetime", "DateTime"},
            {"smallint", "Int16"},
            {"smallmoney", "Decimal"},
            {"sql_variant", "Object *"},
            {"text", "String"},
            {"time", "TimeSpan"},
            {"timestamp", "Byte[]"},
            {"tinyint", "Byte"},
            {"uniqueidentifier", "Guid"},
            {"varbinary", "Byte[]"},
            {"varchar", "String"},
            {"xml", "String"},
        };

        
    }
}
