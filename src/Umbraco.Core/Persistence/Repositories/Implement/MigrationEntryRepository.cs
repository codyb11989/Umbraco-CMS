﻿using System;
using System.Collections.Generic;
using System.Linq;
using LightInject;
using NPoco;
using Semver;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing.CompositionRoots;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence.Factories;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Persistence.Repositories.Implement
{
    internal class MigrationEntryRepository : NPocoRepositoryBase<int, IMigrationEntry>, IMigrationEntryRepository
    {
        public MigrationEntryRepository(IScopeUnitOfWork work, [Inject(RepositoryCompositionRoot.DisabledCache)] CacheHelper cache, ILogger logger)
            : base(work, cache, logger)
        {
        }

        protected override IMigrationEntry PerformGet(int id)
        {
            var sql = GetBaseQuery(false);
            sql.Where(GetBaseWhereClause(), new { Id = id });

            var dto = Database.Fetch<MigrationDto>(SqlSyntax.SelectTop(sql, 1)).FirstOrDefault();
            if (dto == null)
                return null;

            var factory = new MigrationEntryFactory();
            var entity = factory.BuildEntity(dto);

            // reset dirty initial properties (U4-1946)
            entity.ResetDirtyProperties(false);

            return entity;
        }

        protected override IEnumerable<IMigrationEntry> PerformGetAll(params int[] ids)
        {
            var factory = new MigrationEntryFactory();

            if (ids.Any())
            {
                return Database.Fetch<MigrationDto>("WHERE id in (@ids)", new { ids = ids })
                    .Select(x => factory.BuildEntity(x));
            }

            return Database.Fetch<MigrationDto>("WHERE id > 0")
                .Select(x => factory.BuildEntity(x));
        }

        protected override IEnumerable<IMigrationEntry> PerformGetByQuery(IQuery<IMigrationEntry> query)
        {
            var factory = new MigrationEntryFactory();
            var sqlClause = GetBaseQuery(false);
            var translator = new SqlTranslator<IMigrationEntry>(sqlClause, query);
            var sql = translator.Translate();

            return Database.Fetch<MigrationDto>(sql).Select(x => factory.BuildEntity(x));
        }

        protected override Sql<ISqlContext> GetBaseQuery(bool isCount)
        {
            var sql = Sql();

            sql = isCount
                ? sql.SelectCount()
                : sql.Select<MigrationDto>();

            sql
                .From<MigrationDto>();

            return sql;
        }

        protected override string GetBaseWhereClause()
        {
            return "id = @id";
        }

        protected override IEnumerable<string> GetDeleteClauses()
        {
            var list = new List<string>
            {
                "DELETE FROM umbracoMigration WHERE id = @id"
            };
            return list;
        }

        protected override Guid NodeObjectTypeId
        {
            get { throw new NotImplementedException(); }
        }

        protected override void PersistNewItem(IMigrationEntry entity)
        {
            ((MigrationEntry)entity).AddingEntity();

            var factory = new MigrationEntryFactory();
            var dto = factory.BuildDto(entity);

            var id = Convert.ToInt32(Database.Insert(dto));
            entity.Id = id;

            entity.ResetDirtyProperties();
        }

        protected override void PersistUpdatedItem(IMigrationEntry entity)
        {
            ((MigrationEntry)entity).UpdatingEntity();

            var factory = new MigrationEntryFactory();
            var dto = factory.BuildDto(entity);

            Database.Update(dto);

            entity.ResetDirtyProperties();
        }

        public IMigrationEntry FindEntry(string migrationName, SemVersion version)
        {
            var versionString = version.ToString();

            var sql = Sql().SelectAll()
                .From<MigrationDto>()
                .Where<MigrationDto>(x => x.Name.InvariantEquals(migrationName) && x.Version == versionString);

            var result = Database.FirstOrDefault<MigrationDto>(sql);

            var factory = new MigrationEntryFactory();

            return result == null ? null : factory.BuildEntity(result);
        }
    }
}