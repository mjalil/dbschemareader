﻿using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using DatabaseSchemaReader.Conversion;
using DatabaseSchemaReader.DataSchema;

namespace DatabaseSchemaReader.Data
{
    /// <summary>
    /// Reads data from a table into a DataTable. SELECT statement is generated.
    /// </summary>
    /// <remarks>
    /// Uses the <see cref="SqlWriter"/> to generate the SELECT statement.
    /// </remarks>
    public class Reader
    {
        private readonly DatabaseTable _databaseTable;
        private readonly string _connectionString;
        private readonly string _providerName;
        private int _pageSize = 200;

        /// <summary>
        /// Initializes a new instance of the <see cref="Reader"/> class.
        /// </summary>
        /// <param name="databaseTable">The database table.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="providerName">Name of the provider.</param>
        public Reader(DatabaseTable databaseTable, string connectionString, string providerName)
        {
            if (databaseTable == null)
                throw new ArgumentNullException("databaseTable");
            if (connectionString == null)
                throw new ArgumentNullException("connectionString");
            if (providerName == null)
                throw new ArgumentNullException("providerName");
            _providerName = providerName;
            _connectionString = connectionString;
            _databaseTable = databaseTable;
        }

        /// <summary>
        /// Gets or sets the maximum number of records returned.
        /// </summary>
        /// <value>The size of the page.</value>
        public int PageSize
        {
            get { return _pageSize; }
            set
            {
                if (value <= 0) throw new InvalidOperationException("Must be a positive number");
                if (value > 10000) throw new InvalidOperationException("Value is too large - consider another method");
                _pageSize = value;
            }
        }

        private SqlType FindSqlType()
        {
            var sqlType = ProviderToSqlType.Convert(_providerName);
            return !sqlType.HasValue ? SqlType.SqlServer : sqlType.Value;
        }

        /// <summary>
        /// Reads first 200 rows of data from the table into a DataTable.
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "We're generating the select SQL")]
        public DataTable Read()
        {
            var sqlType = FindSqlType();
            var originSql = new SqlWriter(_databaseTable, sqlType);
            var selectAll = originSql.SelectPageSql();

            var dt = new DataTable(_databaseTable.Name) { Locale = CultureInfo.InvariantCulture };

            var dbFactory = DbProviderFactories.GetFactory(_providerName);
            using (var con = dbFactory.CreateConnection())
            {
                con.ConnectionString = _connectionString;
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = selectAll;
                    var p = cmd.CreateParameter();
                    var parameterName = "currentPage";
                    if (sqlType == SqlType.SqlServerCe) parameterName = "offset";
                    p.ParameterName = parameterName;
                    p.Value = 1;
                    if (sqlType == SqlType.SqlServerCe) p.Value = 0;
                    cmd.Parameters.Add(p);
                    var ps = cmd.CreateParameter();
                    ps.ParameterName = "pageSize";
                    ps.Value = _pageSize;
                    cmd.Parameters.Add(ps);

                    using (var da = dbFactory.CreateDataAdapter())
                    {
                        da.SelectCommand = cmd;
                        da.Fill(dt);
                    }
                }
            }
            return dt;
        }
    }
}