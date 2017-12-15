using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XBTFSqlDbScaffolding.BO;

namespace XBTFSqlDbScaffolding
{
    internal class Scaffolder
    {
        private readonly string _connectionString;
        private readonly string _outputDir;
        private readonly string _defaultNamespace;
        private readonly string _domainName;

        private readonly string _contextName;
        private readonly string _baseRepositoryTypeName;
        private readonly string _contextInterfaceName;
        private readonly string[] _staticDataTables;
        private readonly string[] _excludedTables;

        public Scaffolder(ScaffolderSettings settings)
        {
            _connectionString = settings.ConnectionString;
            _outputDir = settings.OutputDir;
            _defaultNamespace = settings.DefaultNamespace;
            _domainName = settings.DomainName;
            _staticDataTables = settings.StaticDataTables;
            _excludedTables = settings.ExcludedTables;

            _contextName = $"{_domainName}DataSourceContext";
            _contextInterfaceName = $"I{_contextName}";
            _baseRepositoryTypeName = $"{_domainName}BaseRepository";
        }

        public async Task Generate()
        {
            var tables = await GetTableSchemas();

            await CreateBaseRepositoryFile();
            await CreateBaseModelFile();

            foreach (var table in tables)
                await CreateModelAndRepositoriesFile(table);

            await CreateDataAccessFactoryFile(tables);
            await CreateContextInterfaceFile(tables);
            await CreateDataSourceContextFile(tables);
            await CreateCacheServiceBuilderFile(tables);
        }

        private async Task CreateContextInterfaceFile(List<SqlTable> tables)
        {
            var repoTxt = string.Join("\r\n", tables
                .Select(tab => $"\t\tI{tab.Name}Repository {tab.Name}Repository {{ get; }}"));
            await GenerateFileFromTemplate(
                "IDataSourceContext",
                $"{_contextInterfaceName}",
                new Dictionary<string, string>
                {
                    { "@namespace_name", _defaultNamespace},
                    {"@context_interface_name", _contextInterfaceName},
                    { "@repositories", repoTxt}
                });
        }

        private async Task CreateBaseModelFile()
        {
            await GenerateFileFromTemplate(
                "BaseModel","BaseModel",
                new Dictionary<string, string>
                {
                    {"@namespace_name", _defaultNamespace},
                    {"@context_interface_name", _contextInterfaceName},
                    {"@base_repository_type_name", _baseRepositoryTypeName}
                });
        }

        private async Task CreateDataSourceContextFile(List<SqlTable> tables)
        {
            var contextFields = string.Join("\r\n",
                tables.Select(tab =>$"\t\tprivate readonly Lazy<I{tab.Name}Repository>" +
                                    $" {GetRepositoryGetterVarName(tab.Name)};"));
            var contextProps = string.Join("\r\n",
                tables.Select(tab => $"\t\tpublic I{tab.Name}Repository {tab.Name}Repository" +
                                     $" => {GetRepositoryGetterVarName(tab.Name)}.Value;"));
            var contextCtorInit = string.Join("\r\n", tables
                .Select(tab => $"\t\t\t{GetRepositoryGetterVarName(tab.Name)} = new Lazy<I{tab.Name}Repository>" +
                               $"(() => new {tab.Name}Repository(this));"));
            await GenerateFileFromTemplate(
                "DataSourceContext", _contextName,
                new Dictionary<string, string>
                {
                    {"@namespace_name", _defaultNamespace},
                    {"@context_interface_name", _contextInterfaceName},
                    {"@context_name", _contextName},
                    {"@data_source_context_fields", contextFields},
                    {"@data_source_context_props", contextProps},
                    {"@data_source_context_ctor_init", contextCtorInit}
                });
        }

        private async Task CreateDataAccessFactoryFile(List<SqlTable> tables)
        {
            var cachesTxt = string.Join("\r\n",
                tables.Select(t => $"\t\tpublic ICache<{t.Name}> Create{t.Name}Cache()" +
                                   $" => new DefaultCache<{t.Name}>(EntityMapper, _useConcurrentMode);"));
            await GenerateFileFromTemplate(
                "DataAccessFactory", "DataAccessFactory",
                new Dictionary<string, string>
                {
                    {"@namespace_name", _defaultNamespace},
                    {"@caches", cachesTxt }
                });
        }

        private async Task CreateCacheServiceBuilderFile(List<SqlTable> tables)
        {
            var regTrans = string.Join("\r\n", tables
                .Where(w => !_staticDataTables.Contains(w.Name))
                .Select(t => $"\t\t\tcacheService.RegisterTransient<ICache<{t.Name}>,{t.Name}>" +
                             $"(dataAccessFactory.Create{t.Name}Cache);"));

            var regPerm = string.Join("\r\n", tables
                .Where(w => _staticDataTables.Contains(w.Name))
                .Select(t => $"\t\t\tcacheService.RegisterPermanent<ICache<{t.Name}>,{t.Name}>" +
                             $"(dataAccessFactory.Create{t.Name}Cache);"));
            var staticDataGetAll = string.Join("\r\n", tables
                .Where(w => _staticDataTables.Contains(w.Name))
                .Select(t => $"\t\t\tawait bc.{t.Name}Repository.GetAll();"));

            await GenerateFileFromTemplate(
                "CacheServiceBuilder", "CacheServiceBuilder",
                new Dictionary<string, string>
                {
                    {"@namespace_name", _defaultNamespace},
                    {"@context_interface_name", _contextInterfaceName},
                    { "@register_transient", regTrans },
                    {"@register_permanent", regPerm },
                    {"@static_data_repositories_get_all", staticDataGetAll }
                });
        }

        private async Task CreateBaseRepositoryFile()
        {
            await GenerateFileFromTemplate(
                "BaseRepository", "BaseRepository",
                new Dictionary<string, string>
                {
                    {"@namespace_name", _defaultNamespace},
                    {"@context_interface_name", _contextInterfaceName},
                    {"@domain_name", _domainName},
                    {"@base_repository_type_name", $"{_domainName}BaseRepository" }
                });
        }

        private async Task CreateModelAndRepositoriesFile(SqlTable table)
        {
            var modelName = table.Name;
            var privateFieldsTxt = GetModelPrivateFields(table);
            var ctorParameters = GetModelCtorParameters(table, true, true, true);
            var ctorPrivateFieldsAssignements = GetModelCtorPrivateFieldsAssignments(table);
            var pkPropName = table.PkColumnName;
            var pkPropType = table.Columns.Where(f => f.IsPrimaryKey).Select(s => s.NetFrameworkDateType).FirstOrDefault();
            var ctor2Parameters = GetModelCtorParameters(table, false, false, false);
            var ctor2TransferParameters = GetCtor2CtorCallParameters(table);
            var collectionsNotifyChanged = GetCollectionsNotifyChanged(table);

            var ctor2 = table.FKs.Count > 0
                ? @"
		public @model_name(
			IDataSourceContextService<@context_interface_name> dscs,
			Guid id,
@ctor_2_parameters)
			: this(dscs,id,
@ctor_2_transfer_parameters)
		{ } "
                    .Replace("@model_name", modelName)
                    .Replace("@context_interface_name", _contextInterfaceName)
                    .Replace("@ctor_2_parameters", ctor2Parameters)
                    .Replace("@ctor_2_transfer_parameters", ctor2TransferParameters)
                : "";

            var tags = new Dictionary<string, string>
            {
                {"@table_name", table.Name},
                {"@table_schema", table.Schema},
                {"@table_type_schema", table.Schema},
                {"@table_type_name", $"{table.Name}TableType"},
                {"@namespace_name", _defaultNamespace},
                {"@context_interface_name", _contextInterfaceName},
                {"@model_name", modelName},
                {"@model_private_fields", privateFieldsTxt},
                {"@ctor_1_parameters", ctorParameters},
                {"@ctor_1_private_fields_assignments", ctorPrivateFieldsAssignements},
                {"@pk_property_name", pkPropName},
                {"@public_scalar_props", GetModelPublicProps(table)},
                {"@pk_property_type", pkPropType },
                {"@base_repository_type_name", _baseRepositoryTypeName},
                {"@repository_class", $"{modelName}Repository" },
                {"@repository_interface", $"I{modelName}Repository" },
                {"@model_ctor_2", ctor2 },
                {"@collections_notify_changed", collectionsNotifyChanged }
            };

            await GenerateFileFromTemplate("Model", table.Name, tags);
            await GenerateFileFromTemplate("Repository", $"{table.Name}Repository", tags);
            await GenerateFileFromTemplate("IRepository", $"I{table.Name}Repository", tags);
        }

        private string GetCollectionsNotifyChanged(SqlTable tab)
        {
            var rows = tab.Collections
                .Select(t =>
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"\t\tprivate void {t.Name}CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)");
                    sb.AppendLine("\t\t{");
                    sb.AppendLine("\t\t\tif (e.Action == NotifyCollectionChangedAction.Move) return;");
                    sb.AppendLine("\t\t\tif (e.NewItems != null)");
                    sb.AppendLine("\t\t\t{");
                    sb.AppendLine("\t\t\t\tvar dataSourceContext = DataSourceContext;");
                    sb.AppendLine($"\t\t\t\tforeach ({t.Name} n in e.NewItems)");
                    sb.AppendLine($"\t\t\t\t\tdataSourceContext.{t.Name}Repository.Insert(n);");
                    sb.AppendLine("\t\t\t}");
                    sb.AppendLine("\t\t}");
                    sb.AppendLine();
                    return sb.ToString();
                }).ToList();
            return string.Join("\r\n", rows);
        }

        private string GetModelPrivateFields(SqlTable table)
        {
            var parameters = table.ColumnsExceptId
                .Select(col =>
                {
                    var type = col.IsPrimaryKey ? $"{col.NetFrameworkDateType}?" : col.NetFrameworkDateType;
                    return $"\t\tprivate {type} {GetPrivateFieldName(col.Name)};";
                })
                .ToArray();

            var contextReferences = table.Columns
                .Where(w => w.IsFK)
                .Select(col =>
                    $"\t\tprivate IContextReference<{col.FK.PkTable}> {GetContextReferencePrivateFieldName(col.Name)};")
                .ToList();
            return string.Join("\r\n", parameters.Union(contextReferences));
        }

        private string GetModelCtorPrivateFieldsAssignments(SqlTable table)
        {
            var scalarVars = table.ColumnsExceptId
                .Select(col => $"\t\t\t{GetPrivateFieldName(col.Name)} = {GetLowerCaseVarName(col.Name)};")
                .ToArray();

            var fks = table.FKs
                .Select(fk =>
                    $"\t\t\t{GetContextReferencePrivateFieldName(fk.FkColumn)} = CreateContextReference(nameof({RemoveIdFromVarName(fk.FkColumn)}), {GetFkCtorVariableName(fk.FkColumn)});")
                .ToList();

            var collections = table.Collections
                .Select(tab =>
                {
                    var propName = $"{tab.Name}s";
                    var sb = new StringBuilder();
                    sb.AppendLine($"\t\t\t{propName} = GetExternalEntities<{tab.Name}>(nameof({propName}));");
                    sb.AppendLine($"\t\t\t{propName}.CollectionChanged += {tab.Name}CollectionChanged;");
                    sb.AppendLine();
                    return sb.ToString();
                });

            return string.Join("\r\n", scalarVars.Union(fks).Union(collections));
        }

        private string GetModelCtorParameters(SqlTable table, bool includeFkScalars, bool mapToProperty, bool assignFksNull)
        {
            var parameters = table.ColumnsExceptId
                .Where(w=> includeFkScalars || !w.IsFK)
                .Select(col =>
                {
                    var type = col.IsPrimaryKey ? $"{col.NetFrameworkDateType}?" : col.NetFrameworkDateType;
                    var param = $"{type} {GetLowerCaseVarName(col.Name)}";
                    return mapToProperty
                        ? $"\t\t\t[MapToProperty(PropertyName = nameof({col.Name}))] {param}"
                        : $"\t\t\t{param}";
                })
                .ToArray();
            var fkParameteres = table.FKs
                .Select(fk =>
                {
                    var rslt = $"\t\t\t{fk.PkTable} {GetFkCtorVariableName(fk.FkColumn)}";
                    return assignFksNull ? $"{rslt} = null" : rslt;
                })
                .ToArray();

            return string.Join(",\r\n", parameters.Union(fkParameteres));
        }

        private string GetCtor2CtorCallParameters(SqlTable table)
        {
            var parameters = table.ColumnsExceptId
                .Select(col => col.IsFK
                    ? $"\t\t\t{GetFkCtorVariableName(col.FK.FkColumn)}.{col.FK.PkColumn}.GetValueOrDefault()"
                    : $"\t\t\t{GetLowerCaseVarName(col.Name)}")
                .ToArray();
            var fkParameteres = table.FKs
                .Select(fk =>$"\t\t\t{GetFkCtorVariableName(fk.FkColumn)}")
                .ToArray();
            return string.Join(",\r\n", parameters.Union(fkParameteres));
        }

        private string GetModelPublicProps(SqlTable table)
        {
            var parameters = table.ColumnsExceptId
                .Select(col =>
                {
                    var type = col.IsPrimaryKey ? $"{col.NetFrameworkDateType}?" : col.NetFrameworkDateType;
                    var colName = col.Name;
                    var pfn = GetPrivateFieldName(col.Name);
                    var sb = new StringBuilder();
                    if (col.IsPrimaryKey)
                    {
                        sb.AppendLine("\t\t[PrimaryKey]");
                        sb.AppendLine("\t\t[RetriveFromDataSourceOnInsert]");
                    }
                    sb.AppendLine($"\t\t[ColumnMapping(ColumnName = \"{colName}\", QueryParameterName = \"@{colName}\")]");
                    sb.AppendLine($"\t\tpublic {type} {colName} {{ get => {pfn}; set => SetField(ref {pfn}, value); }}");
                    return sb.ToString();
                })
                .ToArray();

            var externalReferences = table.Columns
                .Where(w => w.IsFK)
                .Select(col =>
                {
                    var sb = new StringBuilder();
                    var pfn = GetContextReferencePrivateFieldName(col.Name);
                    sb.AppendLine(
                        $"\t\t[ExternalReference(typeof({col.FK.PkTable}), nameof({col.Name}), nameof({_defaultNamespace}.{col.FK.PkTable}.{col.FK.PkColumn}))]");
                    sb.AppendLine(
                        $"\t\tpublic {col.FK.PkTable} {RemoveIdFromVarName(col.Name)} {{ get => {pfn}.Value; set => {pfn}.Value = value; }}");
                    return sb.ToString();
                })
                .ToList();

            var collections = table.Collections
                .Select(tab =>
                {
                    var sb = new StringBuilder();
                    
                    sb.AppendLine($"\t\t[ExternalReference(typeof({tab.Name}), nameof({table.PkColumnName}), nameof({_defaultNamespace}.{tab.Name}.{table.PkColumnName}))]");
                    sb.AppendLine($"\t\tpublic IObservableSet<{tab.Name}> {tab.Name}s {{ get; }}");
                    sb.AppendLine();
                    return sb.ToString();
                })
                .ToList();

            return string.Join("\r\n", parameters.Union(externalReferences).Union(collections));
        }

        #region Helpers

        private async Task GenerateFileFromTemplate(string templateName, string fileName, Dictionary<string, string> tags, string subfolder = "")
        {
            var path = GetPath(fileName, subfolder);
            var template = Helpers.ReadTemplateFromResource($"{templateName}.txt");
            var rslt = template;
            //  todo optimize template generation
            foreach (var pair in tags)
            {
                rslt = rslt.Replace(pair.Key, pair.Value);
            }

            await File.WriteAllTextAsync(path, rslt);
        }

        private string GetRepositoryGetterVarName(string name) => $"_{GetLowerCaseVarName(name)}RepositoryGetter";

        private string GetPath(string codeFileName, string subfolder) => $"{_outputDir}/{subfolder}/{codeFileName}.cs";

        private string GetLowerCaseVarName(string name)
            => name.Length > 1
                ? $"{name.Substring(0, 1).ToLower()}{name.Substring(1)}"
                : name.ToLower();

        private string GetPrivateFieldName(string name) 
            => $"_{GetLowerCaseVarName(name)}";

        private string RemoveIdFromVarName(string name) => name.Substring(0, name.Length - 2);

        private string GetFkCtorVariableName(string name) 
            => $"{GetLowerCaseVarName(RemoveIdFromVarName(name))}";

        private string GetContextReferencePrivateFieldName(string name)
            => $"_{GetLowerCaseVarName(RemoveIdFromVarName(name))}Reference";

        #endregion

        #region SQL

        private async Task<List<SqlTable>> GetTableSchemas()
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                await sqlConnection.OpenAsync();

                //  determine list of tables
                var tables = await ImportTables(sqlConnection);

                tables = tables.Where(w => !_excludedTables.Contains(w.Name)).ToList();

                await ImportTableConstraints(sqlConnection, tables);
                await ImportForeignKeys(sqlConnection, tables);
                
                //  parse tables schemas
                foreach (var table in tables)
                {
                    await ImportTableColumns(sqlConnection, table);
                }

                //  fill collections by FKs
                foreach (var table in tables)
                {
                    foreach (var fk in table.FKs)
                    {
                        var pkTable = tables.First(f => f.Name == fk.PkTable);
                        //  for now we skip complex scenario: when more than one column references table
                        //  in order to achieve that we make an assumption:
                        //  PK col name doesn't equal Fk column name only if there's more than one FK in the same table
                        if (fk.PkColumn == fk.FkColumn)
                        {
                            pkTable.Collections.Add(table);
                        }
                    }
                }

                return tables;
            }
        }

        private async Task<List<SqlTable>> ImportTables(SqlConnection connection)
        {
            var tables = new List<SqlTable>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "select * from INFORMATION_SCHEMA.TABLES";
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        var tableName = reader["TABLE_NAME"].ToString();
                        var schema = reader["TABLE_SCHEMA"].ToString();
                        tables.Add(new SqlTable(tableName, schema));
                    }
                }
            }
            return tables;
        }

        private async Task ImportTableConstraints(SqlConnection connection, List<SqlTable> tables)
        {
            //  import constraints 
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "select t.*,u.COLUMN_NAME From INFORMATION_SCHEMA.TABLE_CONSTRAINTS t join INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE u on u.CONSTRAINT_NAME=t.CONSTRAINT_NAME";
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        var tableName = reader["TABLE_NAME"].ToString();
                        var constraintName = reader["CONSTRAINT_NAME"].ToString();
                        var constraintType = reader["CONSTRAINT_TYPE"].ToString();
                        var columnName = reader["COLUMN_NAME"].ToString();
                        var table = tables.First(f => f.Name == tableName);
                        table.Constraints.Add(new SqlTableConstraint(constraintName, constraintType, columnName));
                    }
                }
            }
        }

        private async Task ImportForeignKeys(SqlConnection connection, List<SqlTable> tables)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
		SELECT RC.CONSTRAINT_NAME FK_Name, KF.TABLE_SCHEMA FK_Schema, KF.TABLE_NAME FK_Table, KF.COLUMN_NAME FK_Column
		, RC.UNIQUE_CONSTRAINT_NAME PK_Name, KP.TABLE_SCHEMA PK_Schema, KP.TABLE_NAME PK_Table, KP.COLUMN_NAME PK_Column
		, RC.MATCH_OPTION MatchOption, RC.UPDATE_RULE UpdateRule, RC.DELETE_RULE DeleteRule
		FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS RC
		JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KF ON RC.CONSTRAINT_NAME = KF.CONSTRAINT_NAME
		JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KP ON RC.UNIQUE_CONSTRAINT_NAME = KP.CONSTRAINT_NAME";
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        var fkName = reader["FK_Name"].ToString();
                        var fkTable = reader["FK_Table"].ToString();
                        var fkColumn = reader["FK_Column"].ToString();
                        var pkTable = reader["PK_Table"].ToString();
                        var pkColumn = reader["PK_Column"].ToString();
                        var table = tables.First(f => f.Name == fkTable);
                        table.FKs.Add(new SqlTableForeignKey(fkName, fkTable, fkColumn, pkTable, pkColumn));
                    }
                }
            }
        }

        private async Task ImportTableColumns(SqlConnection connection, SqlTable table)
        {
            var pkColumnName = table.PkColumnName;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "select * from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME=@name";
                cmd.Parameters.Add(new SqlParameter("@name", table.Name));
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        var columnName = reader["COLUMN_NAME"].ToString();
                        var ordinalPosition = Convert.ToInt32(reader["ORDINAL_POSITION"]);
                        var isNullable = reader["IS_NULLABLE"].ToString() != "NO";
                        var dataType = reader["DATA_TYPE"].ToString();
                        var isPk = columnName == pkColumnName;
                        var fk = table.FKs.FirstOrDefault(a => a.FkColumn == columnName);
                        var column = new SqlTableColumn(columnName, ordinalPosition, isNullable, dataType, isPk, fk);
                        table.Columns.Add(column);
                    }
                }
            }
        }

        #endregion
    }
}
 