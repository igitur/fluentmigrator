#region License
//
// Copyright (c) 2007-2018, Sean Chambers <schambers80@gmail.com>
// Copyright (c) 2010, Nathan Brown
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using FluentMigrator.Expressions;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Announcers;
using FluentMigrator.Runner.Exceptions;
using FluentMigrator.Runner.Generators.SqlServer;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors;
using FluentMigrator.Runner.Processors.Firebird;
using FluentMigrator.Runner.Processors.MySql;
using FluentMigrator.Runner.Processors.Postgres;
using FluentMigrator.Runner.Processors.SqlAnywhere;
using FluentMigrator.Runner.Processors.SQLite;
using FluentMigrator.Runner.Processors.SqlServer;
using FluentMigrator.SqlAnywhere;
using FluentMigrator.Tests.Integration.Migrations;
using FluentMigrator.Tests.Integration.Migrations.Tagged;
using FluentMigrator.Tests.Unit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using NUnit.Framework;

using Shouldly;

namespace FluentMigrator.Tests.Integration
{
    [TestFixture]
    [Category("Integration")]
    public class MigrationRunnerTests : IntegrationTestBase
    {
        private IRunnerContext _runnerContext;

        [SetUp]
        public void SetUp()
        {
            _runnerContext = new RunnerContext(new TextWriterAnnouncer(TestContext.Out))
            {
                Namespace = "FluentMigrator.Tests.Integration.Migrations"
            };
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanRunMigration()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateAndDropTableMigration());

                    processor.TableExists(null, "TestTable").ShouldBeTrue();

                    // This is a hack until MigrationVersionRunner and MigrationRunner are refactored and merged together
                    //processor.CommitTransaction();

                    runner.Down(new TestCreateAndDropTableMigration());
                    processor.TableExists(null, "TestTable").ShouldBeFalse();
                });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanSilentlyFail()
        {
            try
            {
                var processorOptions = new Mock<IMigrationProcessorOptions>();
                processorOptions.SetupGet(x => x.PreviewOnly).Returns(false);

                var processor = new Mock<IMigrationProcessor>();
                processor.Setup(x => x.Process(It.IsAny<CreateForeignKeyExpression>())).Throws(new Exception("Error"));
                processor.Setup(x => x.Process(It.IsAny<DeleteForeignKeyExpression>())).Throws(new Exception("Error"));
                processor.Setup(x => x.Options).Returns(processorOptions.Object);

                var serviceProvider = processor.Object.CreateServices().WithRunnerContext(_runnerContext)
                    .BuildServiceProvider();

                var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();
                runner.SilentlyFail = true;

                runner.Up(new TestForeignKeySilentFailure());

                runner.CaughtExceptions.Count.ShouldBeGreaterThan(0);

                runner.Down(new TestForeignKeySilentFailure());
                runner.CaughtExceptions.Count.ShouldBeGreaterThan(0);
            }
            finally
            {
                ExecuteWithSupportedProcessors(processor =>
                {
                    MigrationRunner testRunner = SetupMigrationRunner(processor);
                    testRunner.RollbackToVersion(0);
                }, false);
            }
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanApplyForeignKeyConvention()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestForeignKeyNamingConvention());
                    processor.ConstraintExists(null, "Users", "FK_Users_GroupId_Groups_GroupId").ShouldBeTrue();

                    runner.Down(new TestForeignKeyNamingConvention());
                    processor.ConstraintExists(null, "Users", "FK_Users_GroupId_Groups_GroupId").ShouldBeFalse();
                }, false, typeof(SQLiteProcessor));
        }

        [Test]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanApplyForeignKeyConventionWithSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestForeignKeyNamingConventionWithSchema());

                    processor.ConstraintExists("TestSchema", "Users", "FK_Users_GroupId_Groups_GroupId").ShouldBeTrue();
                    runner.Down(new TestForeignKeyNamingConventionWithSchema());
                }, false, new []{typeof(SQLiteProcessor), typeof(FirebirdProcessor)});
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanApplyIndexConvention()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestIndexNamingConvention());
                    processor.IndexExists(null, "Users", "IX_Users_GroupId").ShouldBeTrue();
                    processor.TableExists(null, "Users").ShouldBeTrue();

                    runner.Down(new TestIndexNamingConvention());
                    processor.IndexExists(null, "Users", "IX_Users_GroupId").ShouldBeFalse();
                    processor.TableExists(null, "Users").ShouldBeFalse();
                });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanApplyUniqueConvention()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestUniqueConstraintNamingConvention());
                    processor.ConstraintExists(null, "Users", "UC_Users_GroupId").ShouldBeTrue();
                    processor.ConstraintExists(null, "Users", "UC_Users_AccountId").ShouldBeTrue();
                    processor.TableExists(null, "Users").ShouldBeTrue();

                    runner.Down(new TestUniqueConstraintNamingConvention());
                    processor.ConstraintExists(null, "Users", "UC_Users_GroupId").ShouldBeFalse();
                    processor.ConstraintExists(null, "Users", "UC_Users_AccountId").ShouldBeFalse();
                    processor.TableExists(null, "Users").ShouldBeFalse();
                },
                false,
                typeof(SQLiteProcessor));
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanApplyIndexConventionWithSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestIndexNamingConventionWithSchema());
                    processor.IndexExists("TestSchema", "Users", "IX_Users_GroupId").ShouldBeTrue();
                    processor.TableExists("TestSchema", "Users").ShouldBeTrue();

                    runner.Down(new TestIndexNamingConventionWithSchema());
                    processor.IndexExists("TestSchema", "Users", "IX_Users_GroupId").ShouldBeFalse();
                    processor.TableExists("TestSchema", "Users").ShouldBeFalse();
                });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanCreateAndDropIndex()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateAndDropTableMigration());
                    processor.IndexExists(null, "TestTable", "IX_TestTable_Name").ShouldBeFalse();

                    runner.Up(new TestCreateAndDropIndexMigration());
                    processor.IndexExists(null, "TestTable", "IX_TestTable_Name").ShouldBeTrue();

                    runner.Down(new TestCreateAndDropIndexMigration());
                    processor.IndexExists(null, "TestTable", "IX_TestTable_Name").ShouldBeFalse();

                    runner.Down(new TestCreateAndDropTableMigration());
                    processor.IndexExists(null, "TestTable", "IX_TestTable_Name").ShouldBeFalse();

                    //processor.CommitTransaction();
                });
        }

        [Test]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanCreateAndDropIndexWithSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());
                    processor.IndexExists("TestSchema", "TestTable", "IX_TestTable_Name").ShouldBeFalse();

                    runner.Up(new TestCreateAndDropIndexMigrationWithSchema());
                    processor.IndexExists("TestSchema", "TestTable", "IX_TestTable_Name").ShouldBeTrue();

                    runner.Down(new TestCreateAndDropIndexMigrationWithSchema());
                    processor.IndexExists("TestSchema", "TestTable", "IX_TestTable_Name").ShouldBeFalse();

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());
                    processor.IndexExists("TestSchema", "TestTable", "IX_TestTable_Name").ShouldBeFalse();

                    runner.Down(new TestCreateSchema());
                    //processor.CommitTransaction();
                }, false, new[] { typeof(SQLiteProcessor), typeof(FirebirdProcessor) });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanRenameTable()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateAndDropTableMigration());
                    processor.TableExists(null, "TestTable2").ShouldBeTrue();

                    runner.Up(new TestRenameTableMigration());
                    processor.TableExists(null, "TestTable2").ShouldBeFalse();
                    processor.TableExists(null, "TestTable'3").ShouldBeTrue();

                    runner.Down(new TestRenameTableMigration());
                    processor.TableExists(null, "TestTable'3").ShouldBeFalse();
                    processor.TableExists(null, "TestTable2").ShouldBeTrue();

                    runner.Down(new TestCreateAndDropTableMigration());
                    processor.TableExists(null, "TestTable2").ShouldBeFalse();

                    //processor.CommitTransaction();
                });
        }


        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanRenameTableWithSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());
                    processor.TableExists("TestSchema", "TestTable2").ShouldBeTrue();

                    runner.Up(new TestRenameTableMigrationWithSchema());
                    processor.TableExists("TestSchema", "TestTable2").ShouldBeFalse();
                    processor.TableExists("TestSchema", "TestTable'3").ShouldBeTrue();

                    runner.Down(new TestRenameTableMigrationWithSchema());
                    processor.TableExists("TestSchema", "TestTable'3").ShouldBeFalse();
                    processor.TableExists("TestSchema", "TestTable2").ShouldBeTrue();

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());
                    processor.TableExists("TestSchema", "TestTable2").ShouldBeFalse();

                    runner.Down(new TestCreateSchema());

                    //processor.CommitTransaction();
                });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanRenameColumn()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateAndDropTableMigration());
                    processor.ColumnExists(null, "TestTable2", "Name").ShouldBeTrue();

                    runner.Up(new TestRenameColumnMigration());
                    processor.ColumnExists(null, "TestTable2", "Name").ShouldBeFalse();
                    processor.ColumnExists(null, "TestTable2", "Name'3").ShouldBeTrue();

                    runner.Down(new TestRenameColumnMigration());
                    processor.ColumnExists(null, "TestTable2", "Name'3").ShouldBeFalse();
                    processor.ColumnExists(null, "TestTable2", "Name").ShouldBeTrue();

                    runner.Down(new TestCreateAndDropTableMigration());
                    processor.ColumnExists(null, "TestTable2", "Name").ShouldBeFalse();
                }, true, typeof(SQLiteProcessor));
        }

        [Test]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanRenameColumnWithSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices().WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();
                    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());
                    processor.ColumnExists("TestSchema", "TestTable2", "Name").ShouldBeTrue();

                    runner.Up(new TestRenameColumnMigrationWithSchema());
                    processor.ColumnExists("TestSchema", "TestTable2", "Name").ShouldBeFalse();
                    processor.ColumnExists("TestSchema", "TestTable2", "Name'3").ShouldBeTrue();

                    runner.Down(new TestRenameColumnMigrationWithSchema());
                    processor.ColumnExists("TestSchema", "TestTable2", "Name'3").ShouldBeFalse();
                    processor.ColumnExists("TestSchema", "TestTable2", "Name").ShouldBeTrue();

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());
                    processor.ColumnExists("TestSchema", "TestTable2", "Name").ShouldBeFalse();

                    runner.Down(new TestCreateSchema());
                }, true, typeof(SQLiteProcessor), typeof(FirebirdProcessor));
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanLoadMigrations()
        {
            ExecuteWithSupportedProcessors(processor =>
            {
                var serviceProvider = processor.CreateServices()
                    .WithRunnerContext(_runnerContext)
                    .WithMigrationsIn(typeof(TestMigration).Namespace)
                    .BuildServiceProvider();
                var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

                //runner.Processor.CommitTransaction();

                runner.MigrationLoader.LoadMigrations().ShouldNotBeNull();
            });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanLoadVersion()
        {
            ExecuteWithSupportedProcessors(processor =>
            {
                var serviceProvider = processor.CreateServices()
                    .WithRunnerContext(_runnerContext)
                    .WithMigrationsIn(typeof(TestMigration).Namespace)
                    .BuildServiceProvider();
                var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                //runner.Processor.CommitTransaction();
                runner.VersionLoader.VersionInfo.ShouldNotBeNull();
            });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanRunMigrations()
        {
            ExecuteWithSupportedProcessors(processor =>
            {
                MigrationRunner runner = SetupMigrationRunner(processor);

                runner.MigrateUp(false);

                runner.VersionLoader.VersionInfo.HasAppliedMigration(1).ShouldBeTrue();
                runner.VersionLoader.VersionInfo.HasAppliedMigration(2).ShouldBeTrue();
                runner.VersionLoader.VersionInfo.HasAppliedMigration(3).ShouldBeTrue();
                runner.VersionLoader.VersionInfo.HasAppliedMigration(4).ShouldBeTrue();
                runner.VersionLoader.VersionInfo.HasAppliedMigration(5).ShouldBeTrue();
                runner.VersionLoader.VersionInfo.HasAppliedMigration(6).ShouldBeTrue();
                runner.VersionLoader.VersionInfo.Latest().ShouldBe(6);

                runner.RollbackToVersion(0, false);
            });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanMigrateASpecificVersion()
        {
            ExecuteWithSupportedProcessors(processor =>
            {
                MigrationRunner runner = SetupMigrationRunner(processor);
                try
                {
                    runner.MigrateUp(1, false);

                    runner.VersionLoader.VersionInfo.HasAppliedMigration(1).ShouldBeTrue();
                    processor.TableExists(null, "Users").ShouldBeTrue();
                }
                finally
                {
                    runner.RollbackToVersion(0, false);
                }
            });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanMigrateASpecificVersionDown()
        {
            try
            {
                ExecuteWithSupportedProcessors(processor =>
                {
                    MigrationRunner runner = SetupMigrationRunner(processor);

                    runner.MigrateUp(1, false);

                    runner.VersionLoader.VersionInfo.HasAppliedMigration(1).ShouldBeTrue();
                    processor.TableExists(null, "Users").ShouldBeTrue();

                    MigrationRunner testRunner = SetupMigrationRunner(processor);
                    testRunner.MigrateDown(0, false);
                    testRunner.VersionLoader.VersionInfo.HasAppliedMigration(1).ShouldBeFalse();
                    processor.TableExists(null, "Users").ShouldBeFalse();
                }, false, typeof(SQLiteProcessor));
            }
            finally
            {
                ExecuteWithSupportedProcessors(processor =>
                {
                    MigrationRunner testRunner = SetupMigrationRunner(processor);
                    testRunner.RollbackToVersion(0, false);
                }, false);
            }
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void RollbackAllShouldRemoveVersionInfoTable()
        {
            ExecuteWithSupportedProcessors(processor =>
            {
                MigrationRunner runner = SetupMigrationRunner(processor);

                runner.MigrateUp(2);

                processor.TableExists(runner.VersionLoader.VersionTableMetaData.SchemaName, runner.VersionLoader.VersionTableMetaData.TableName).ShouldBeTrue();

                runner.RollbackToVersion(0);

                processor.TableExists(runner.VersionLoader.VersionTableMetaData.SchemaName, runner.VersionLoader.VersionTableMetaData.TableName).ShouldBeFalse();
            });
        }

        [Test]
        [Category("SqlServer2008")]
        public void MigrateUpWithSqlServerProcessorShouldCommitItsTransaction()
        {
            if (!IntegrationTestOptions.SqlServer2008.IsEnabled)
                return;

            var connection = new SqlConnection(IntegrationTestOptions.SqlServer2008.ConnectionString);
            var processor = new SqlServerProcessor(new[] { "SqlServer2008" }, connection, new SqlServer2008Generator(), new TextWriterAnnouncer(TestContext.Out), new ProcessorOptions(), new SqlServerDbFactory());

            MigrationRunner runner = SetupMigrationRunner(processor);
            runner.MigrateUp();

            try
            {
                processor.WasCommitted.ShouldBeTrue();

            }
            finally
            {
                CleanupTestSqlServerDatabase(connection, processor);
            }
        }

        [Test]
        [Category("SqlServer2008")]
        public void MigrateUpSpecificVersionWithSqlServerProcessorShouldCommitItsTransaction()
        {
            if (!IntegrationTestOptions.SqlServer2008.IsEnabled)
                return;

            var connection = new SqlConnection(IntegrationTestOptions.SqlServer2008.ConnectionString);
            var processor = new SqlServerProcessor(new[] { "SqlServer2008" }, connection, new SqlServer2008Generator(), new TextWriterAnnouncer(TestContext.Out), new ProcessorOptions(), new SqlServerDbFactory());

            MigrationRunner runner = SetupMigrationRunner(processor);
            runner.MigrateUp(1);

            try
            {
                processor.WasCommitted.ShouldBeTrue();

            }
            finally
            {
                CleanupTestSqlServerDatabase(connection, processor);
            }
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void MigrateUpWithTaggedMigrationsShouldOnlyApplyMatchedMigrations()
        {
            ExecuteWithSupportedProcessors(processor =>
            {
                var runnerContext = new RunnerContext(new TextWriterAnnouncer(TestContext.Out))
                {
                    Namespace = typeof(TenantATable).Namespace,
                    Tags = new[] { "TenantA" }
                };

                var serviceProvider = processor.CreateServices()
                    .WithRunnerContext(runnerContext)
                    .WithMigrationsIn(typeof(TenantATable).Namespace)
                    .BuildServiceProvider();

                var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                try
                {
                    runner.MigrateUp(false);

                    processor.TableExists(null, "TenantATable").ShouldBeTrue();
                    processor.TableExists(null, "NormalTable").ShouldBeTrue();
                    processor.TableExists(null, "TenantBTable").ShouldBeFalse();
                    processor.TableExists(null, "TenantAandBTable").ShouldBeTrue();
                }
                finally
                {
                    runner.RollbackToVersion(0);
                }
            });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void MigrateUpWithTaggedMigrationsAndUsingMultipleTagsShouldOnlyApplyMatchedMigrations()
        {
            ExecuteWithSupportedProcessors(processor =>
            {
                var runnerContext = new RunnerContext(new TextWriterAnnouncer(TestContext.Out))
                {
                    Namespace = typeof(TenantATable).Namespace,
                    Tags = new[] { "TenantA", "TenantB" }
                };

                var serviceProvider = processor.CreateServices()
                    .WithRunnerContext(runnerContext)
                    .WithMigrationsIn(typeof(TenantATable).Namespace)
                    .BuildServiceProvider();

                var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                try
                {
                    runner.MigrateUp(false);

                    processor.TableExists(null, "TenantATable").ShouldBeFalse();
                    processor.TableExists(null, "NormalTable").ShouldBeTrue();
                    processor.TableExists(null, "TenantBTable").ShouldBeFalse();
                    processor.TableExists(null, "TenantAandBTable").ShouldBeTrue();
                }
                finally
                {
                    runner.RollbackToVersion(0);
                }
            });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void MigrateUpWithDifferentTaggedShouldIgnoreConcreteOfTagged()
        {
            ExecuteWithSupportedProcessors(processor =>
            {
                var runnerContext = new RunnerContext(new TextWriterAnnouncer(TestContext.Out))
                {
                    Namespace = typeof(TenantATable).Namespace,
                    Tags = new[] { "TenantB" }
                };

                var serviceProvider = processor.CreateServices()
                    .WithRunnerContext(runnerContext)
                    .WithMigrationsIn(typeof(TenantATable).Namespace)
                    .BuildServiceProvider();

                var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                try
                {
                    runner.MigrateUp(false);

                    processor.TableExists(null, "TenantATable").ShouldBeFalse();
                    processor.TableExists(null, "NormalTable").ShouldBeTrue();
                    processor.TableExists(null, "TenantBTable").ShouldBeTrue();
                }
                finally
                {
                    runner.RollbackToVersion(0);
                }
            });
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void MigrateDownWithDifferentTagsToMigrateUpShouldApplyMatchedMigrations()
        {
            var migrationsNamespace = typeof(TenantATable).Namespace;

            var runnerContext = new RunnerContext(new TextWriterAnnouncer(TestContext.Out))
            {
                Namespace = migrationsNamespace,
            };

            // Excluded SqliteProcessor as it errors on DB cleanup (RollbackToVersion).
            ExecuteWithSupportedProcessors(processor =>
            {
                runnerContext.Tags = new[] { "TenantA" };

                var serviceProvider = processor.CreateServices()
                    .WithRunnerContext(runnerContext)
                    .WithMigrationsIn(typeof(TenantATable).Namespace)
                    .BuildServiceProvider();

                try
                {
                    using (var scope = serviceProvider.CreateScope())
                    {
                        var runner = (MigrationRunner) scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                        runner.MigrateUp(false);
                    }

                    processor.TableExists(null, "TenantATable").ShouldBeTrue();
                    processor.TableExists(null, "NormalTable").ShouldBeTrue();
                    processor.TableExists(null, "TenantBTable").ShouldBeFalse();
                    processor.TableExists(null, "TenantAandBTable").ShouldBeTrue();

                    runnerContext.Tags = new[] { "TenantB" };

                    using (var scope = serviceProvider.CreateScope())
                    {
                        var runner = (MigrationRunner)scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                        runner.MigrateDown(0, false);
                    }

                    processor.TableExists(null, "TenantATable").ShouldBeTrue();
                    processor.TableExists(null, "NormalTable").ShouldBeFalse();
                    processor.TableExists(null, "TenantBTable").ShouldBeFalse();
                    processor.TableExists(null, "TenantAandBTable").ShouldBeFalse();
                }
                finally
                {
                    runnerContext.Tags = new[] { "TenantA" };

                    using (var scope = serviceProvider.CreateScope())
                    {
                        var runner = (MigrationRunner)scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                        runner.RollbackToVersion(0, false);
                    }
                }
            }, true, typeof(SQLiteProcessor));
        }

        [Test]
        [Category("SqlServer2008")]
        public void VersionInfoCreationScriptsOnlyGeneratedOnceInPreviewMode()
        {
            if (!IntegrationTestOptions.SqlServer2008.IsEnabled)
                return;

            var connection = new SqlConnection(IntegrationTestOptions.SqlServer2008.ConnectionString);
            var processorOptions = new ProcessorOptions { PreviewOnly = true };

            var outputSql = new StringWriter();
            var announcer = new TextWriterAnnouncer(outputSql){ ShowSql = true };

            var processor = new SqlServerProcessor(new[] { "SqlServer2008" }, connection, new SqlServer2008Generator(), announcer, processorOptions, new SqlServerDbFactory());

            try
            {
                var versionTableMetaData = new TestVersionTableMetaData();
                var runnerContext = new RunnerContext(announcer)
                {
                    Namespace = "FluentMigrator.Tests.Integration.Migrations",
                    PreviewOnly = true
                };

                var serviceProvider = processor.CreateServices()
                    .WithRunnerContext(runnerContext)
                    .WithMigrationsIn(runnerContext.Namespace)
                    .BuildServiceProvider();

                var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();
                runner.MigrateUp(1, false);

                processor.CommitTransaction();

                string schemaName = versionTableMetaData.SchemaName;
                var schemaAndTableName = string.Format("[{0}].[{1}]", schemaName, TestVersionTableMetaData.TABLENAME);

                var outputSqlString = outputSql.ToString();

                var createSchemaMatches = new Regex(Regex.Escape(string.Format("CREATE SCHEMA [{0}]", schemaName)))
                    .Matches(outputSqlString).Count;
                var createTableMatches = new Regex(Regex.Escape("CREATE TABLE " + schemaAndTableName))
                    .Matches(outputSqlString).Count;
                var createIndexMatches = new Regex(Regex.Escape("CREATE UNIQUE CLUSTERED INDEX [" + TestVersionTableMetaData.UNIQUEINDEXNAME + "] ON " + schemaAndTableName))
                    .Matches(outputSqlString).Count;
                var alterTableMatches = new Regex(Regex.Escape("ALTER TABLE " + schemaAndTableName))
                    .Matches(outputSqlString).Count;

                System.Console.WriteLine(outputSqlString);

                createSchemaMatches.ShouldBe(1);
                createTableMatches.ShouldBe(1);
                alterTableMatches.ShouldBe(2);
                createIndexMatches.ShouldBe(1);

            }
            finally
            {
                CleanupTestSqlServerDatabase(connection, processor);
            }
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void MigrateUpWithTaggedMigrationsShouldNotApplyAnyMigrationsIfNoTagsParameterIsPassedIntoTheRunner()
        {
            ExecuteWithSupportedProcessors(processor =>
            {
                var runnerContext = new RunnerContext(new TextWriterAnnouncer(TestContext.Out))
                {
                    Namespace = typeof(TenantATable).Namespace
                };

                var serviceProvider = processor.CreateServices()
                    .WithRunnerContext(runnerContext)
                    .WithMigrationsIn(runnerContext.Namespace)
                    .BuildServiceProvider();

                var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                try
                {
                    runner.MigrateUp(false);

                    processor.TableExists(null, "TenantATable").ShouldBeFalse();
                    processor.TableExists(null, "NormalTable").ShouldBeTrue();
                    processor.TableExists(null, "TenantBTable").ShouldBeFalse();
                    processor.TableExists(null, "TenantAandBTable").ShouldBeFalse();
                }
                finally
                {
                    runner.RollbackToVersion(0);
                }
            });
        }

        [Test]
        [Category("Firebird")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void ValidateVersionOrderShouldDoNothingIfUnappliedMigrationVersionIsGreaterThanLatestAppliedMigration()
        {

            // Using SqlServer instead of SqlLite as versions not deleted from VersionInfo table when using Sqlite.
            var excludedProcessors = new[] { typeof(SQLiteProcessor), typeof(MySqlProcessor), typeof(PostgresProcessor) };

            var runnerContext1 = new RunnerContext(new TextWriterAnnouncer(TestContext.Out)) { Namespace = typeof(Migrations.Interleaved.Pass2.User).Namespace };
            var runnerContext2 = new RunnerContext(new TextWriterAnnouncer(TestContext.Out)) { Namespace = typeof(Migrations.Interleaved.Pass3.User).Namespace };

            try
            {
                ExecuteWithSupportedProcessors(processor =>
                {
                    var runner1 = processor.CreateServices()
                        .WithRunnerContext(runnerContext1)
                        .WithMigrationsIn(runnerContext1.Namespace)
                        .BuildServiceProvider()
                        .GetRequiredService<IMigrationRunner>();

                    runner1.MigrateUp(3);

                    var runner2 = processor.CreateServices()
                        .WithRunnerContext(runnerContext2)
                        .WithMigrationsIn(runnerContext2.Namespace)
                        .BuildServiceProvider()
                        .GetRequiredService<IMigrationRunner>();

                    Assert.DoesNotThrow(runner2.ValidateVersionOrder);
                }, false, excludedProcessors);
            }
            finally
            {
                ExecuteWithSupportedProcessors(processor =>
                {
                    var runner = processor.CreateServices()
                        .WithRunnerContext(runnerContext2)
                        .WithMigrationsIn(runnerContext2.Namespace)
                        .BuildServiceProvider()
                        .GetRequiredService<IMigrationRunner>();
                    runner.RollbackToVersion(0);
                }, true, excludedProcessors);
            }
        }

        [Test]
        [Category("Firebird")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void ValidateVersionOrderShouldThrowExceptionIfUnappliedMigrationVersionIsLessThanLatestAppliedMigration()
        {
            // Using SqlServer instead of SqlLite as versions not deleted from VersionInfo table when using Sqlite.
            var excludedProcessors = new[] { typeof(MySqlProcessor), typeof(SQLiteProcessor) };

            var runnerContext1 = new RunnerContext(new TextWriterAnnouncer(TestContext.Out)) { Namespace = typeof(Migrations.Interleaved.Pass2.User).Namespace };
            var runnerContext2 = new RunnerContext(new TextWriterAnnouncer(TestContext.Out)) { Namespace = typeof(Migrations.Interleaved.Pass3.User).Namespace };

            VersionOrderInvalidException caughtException = null;

            try
            {
                ExecuteWithSupportedProcessors(processor =>
                {
                    var migrationRunner = processor.CreateServices()
                        .WithRunnerContext(runnerContext1)
                        .WithMigrationsIn(runnerContext1.Namespace)
                        .BuildServiceProvider()
                        .GetRequiredService<IMigrationRunner>();

                    migrationRunner.MigrateUp();
                }, false, excludedProcessors);

                ExecuteWithSupportedProcessors(processor =>
                {
                    var migrationRunner = processor.CreateServices()
                        .WithRunnerContext(runnerContext2)
                        .WithMigrationsIn(runnerContext2.Namespace)
                        .BuildServiceProvider()
                        .GetRequiredService<IMigrationRunner>();

                    migrationRunner.ValidateVersionOrder();
                }, false, excludedProcessors);
            }
            catch (VersionOrderInvalidException ex)
            {
                caughtException = ex;
            }
            finally
            {
                ExecuteWithSupportedProcessors(processor =>
                {
                    var migrationRunner = processor.CreateServices()
                        .WithRunnerContext(runnerContext2)
                        .WithMigrationsIn(runnerContext2.Namespace)
                        .BuildServiceProvider()
                        .GetRequiredService<IMigrationRunner>();

                    migrationRunner.RollbackToVersion(0);
                }, true, excludedProcessors);
            }

            caughtException.ShouldNotBeNull();

            caughtException.InvalidMigrations.Count().ShouldBe(1);
            var keyValuePair = caughtException.InvalidMigrations.First();
            keyValuePair.Key.ShouldBe(200909060935);
            keyValuePair.Value.Migration.ShouldBeOfType<Migrations.Interleaved.Pass3.UserEmail>();
        }

        [Test]
        [Category("SqlServer2012")]
        public void CanCreateSequence()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    if (processor.DatabaseType != "SqlServer2012")
                    {
                        Assert.Ignore("A processor of type {0} isn't supported", processor.DatabaseType);
                        return;
                    }

                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSequence());
                    processor.SequenceExists(null, "TestSequence");

                    runner.Down(new TestCreateSequence());
                    processor.SequenceExists(null, "TestSequence").ShouldBeFalse();
                }, true, pt => pt == typeof(SqlServerProcessor));
        }

        [Test]
        [Category("Postgres")]
        [Category("SqlServer2012")]
        public void CanCreateSequenceWithSchema()
        {
            if (!IntegrationTestOptions.SqlServer2012.IsEnabled && !IntegrationTestOptions.Postgres.IsEnabled)
            {
                Assert.Ignore("No processor found for the given action.");
            }

            Action<IMigrationProcessor> action = processor =>
            {
                var serviceProvider = processor.CreateServices()
                    .WithRunnerContext(_runnerContext)
                    .WithMigrationsIn(typeof(TestMigration).Namespace)
                    .BuildServiceProvider();

                var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                runner.Up(new TestCreateSchema());
                runner.Up(new TestCreateSequenceWithSchema());
                processor.SequenceExists("TestSchema", "TestSequence").ShouldBeTrue();

                runner.Down(new TestCreateSequenceWithSchema());
                runner.Down(new TestCreateSchema());
                processor.SequenceExists("TestSchema", "TestSequence").ShouldBeFalse();
            };

            if (IntegrationTestOptions.SqlServer2012.IsEnabled)
            {
                ExecuteWithSqlServer2012(
                    action,
                    true,
                    IntegrationTestOptions.SqlServer2012);
            }

            if (IntegrationTestOptions.Postgres.IsEnabled)
            {
                ExecuteWithPostgres(action, true, IntegrationTestOptions.Postgres);
            }
        }

        [Test]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        public void CanAlterColumnWithSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());
                    processor.ColumnExists("TestSchema", "TestTable2", "Name2").ShouldBeTrue();
                    processor.DefaultValueExists("TestSchema", "TestTable", "Name", "Anonymous").ShouldBeTrue();


                    runner.Up(new TestAlterColumnWithSchema());
                    processor.ColumnExists("TestSchema", "TestTable2", "Name2").ShouldBeTrue();

                    runner.Down(new TestAlterColumnWithSchema());
                    processor.ColumnExists("TestSchema", "TestTable2", "Name2").ShouldBeTrue();

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Down(new TestCreateSchema());
                },
                true,
                typeof(SQLiteProcessor),
                typeof(FirebirdProcessor),
                typeof(SqlAnywhereProcessor));
        }

        [Test]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanAlterTableWithSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());
                    processor.ColumnExists("TestSchema", "TestTable2", "NewColumn").ShouldBeFalse();

                    runner.Up(new TestAlterTableWithSchema());
                    processor.ColumnExists("TestSchema", "TestTable2", "NewColumn").ShouldBeTrue();

                    runner.Down(new TestAlterTableWithSchema());
                    processor.ColumnExists("TestSchema", "TestTable2", "NewColumn").ShouldBeFalse();

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Down(new TestCreateSchema());
                },
                true,
                typeof(SQLiteProcessor),
                typeof(FirebirdProcessor));
        }

        [Test]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        public void CanAlterTablesSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());
                    processor.TableExists("TestSchema", "TestTable").ShouldBeTrue();

                    runner.Up(new TestAlterSchema());
                    processor.TableExists("NewSchema", "TestTable").ShouldBeTrue();

                    runner.Down(new TestAlterSchema());
                    processor.TableExists("TestSchema", "TestTable").ShouldBeTrue();

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Down(new TestCreateSchema());
                },
                true,
                typeof(SQLiteProcessor),
                typeof(FirebirdProcessor),
                typeof(SqlAnywhereProcessor));
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        public void CanCreateUniqueConstraint()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateAndDropTableMigration());
                    processor.ConstraintExists(null, "TestTable2", "TestUnique").ShouldBeFalse();

                    runner.Up(new TestCreateUniqueConstraint());
                    processor.ConstraintExists(null, "TestTable2", "TestUnique").ShouldBeTrue();

                    runner.Down(new TestCreateUniqueConstraint());
                    processor.ConstraintExists(null, "TestTable2", "TestUnique").ShouldBeFalse();

                    runner.Down(new TestCreateAndDropTableMigration());

                }, true, typeof(SQLiteProcessor), typeof(SqlAnywhereProcessor));
        }

        [Test]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        public void CanCreateUniqueConstraintWithSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());
                    processor.ConstraintExists("TestSchema", "TestTable2", "TestUnique").ShouldBeFalse();

                    runner.Up(new TestCreateUniqueConstraintWithSchema());
                    processor.ConstraintExists("TestSchema", "TestTable2", "TestUnique").ShouldBeTrue();

                    runner.Down(new TestCreateUniqueConstraintWithSchema());
                    processor.ConstraintExists("TestSchema", "TestTable2", "TestUnique").ShouldBeFalse();

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Down(new TestCreateSchema());
                },
                true,
                typeof(SQLiteProcessor),
                typeof(FirebirdProcessor),
                typeof(SqlAnywhereProcessor));
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanInsertData()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateAndDropTableMigration());
                    DataSet ds = processor.ReadTableData(null, "TestTable");
                    ds.Tables[0].Rows.Count.ShouldBe(1);
                    ds.Tables[0].Rows[0][1].ShouldBe("Test");

                    runner.Down(new TestCreateAndDropTableMigration());
                },
                true,
                typeof(SQLiteProcessor));
        }

        [Test]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanInsertDataWithSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());
                    DataSet ds = processor.ReadTableData("TestSchema", "TestTable");
                    ds.Tables[0].Rows.Count.ShouldBe(1);
                    ds.Tables[0].Rows[0][1].ShouldBe("Test");

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Down(new TestCreateSchema());
                },
                true,
                typeof(SQLiteProcessor),
                typeof(FirebirdProcessor));
        }

        [Test]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanUpdateData()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Up(new TestUpdateData());
                    DataSet upDs = processor.ReadTableData("TestSchema", "TestTable");
                    upDs.Tables[0].Rows.Count.ShouldBe(1);
                    upDs.Tables[0].Rows[0][1].ShouldBe("Updated");

                    runner.Down(new TestUpdateData());
                    DataSet downDs = processor.ReadTableData("TestSchema", "TestTable");
                    downDs.Tables[0].Rows.Count.ShouldBe(1);
                    downDs.Tables[0].Rows[0][1].ShouldBe("Test");

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Down(new TestCreateSchema());
                },
                true,
                typeof(SQLiteProcessor),
                typeof(FirebirdProcessor));
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanDeleteData()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateAndDropTableMigration());

                    runner.Up(new TestDeleteData());
                    DataSet upDs = processor.ReadTableData(null, "TestTable");
                    upDs.Tables[0].Rows.Count.ShouldBe(0);

                    runner.Down(new TestDeleteData());
                    DataSet downDs = processor.ReadTableData(null, "TestTable");
                    downDs.Tables[0].Rows.Count.ShouldBe(1);
                    downDs.Tables[0].Rows[0][1].ShouldBe("Test");

                    runner.Down(new TestCreateAndDropTableMigration());

                },
                true,
                typeof(SQLiteProcessor));
        }

        [Test]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanDeleteDataWithSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Up(new TestDeleteDataWithSchema());
                    DataSet upDs = processor.ReadTableData("TestSchema", "TestTable");
                    upDs.Tables[0].Rows.Count.ShouldBe(0);

                    runner.Down(new TestDeleteDataWithSchema());
                    DataSet downDs = processor.ReadTableData("TestSchema", "TestTable");
                    downDs.Tables[0].Rows.Count.ShouldBe(1);
                    downDs.Tables[0].Rows[0][1].ShouldBe("Test");

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Down(new TestCreateSchema());
                },
                true,
                typeof(SQLiteProcessor),
                typeof(FirebirdProcessor));
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanReverseCreateIndex()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Up(new TestCreateIndexWithReversing());
                    processor.IndexExists("TestSchema", "TestTable2", "IX_TestTable2_Name2").ShouldBeTrue();

                    runner.Down(new TestCreateIndexWithReversing());
                    processor.IndexExists("TestSchema", "TestTable2", "IX_TestTable2_Name2").ShouldBeFalse();

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Down(new TestCreateSchema());
                },
                true,
                typeof(SQLiteProcessor));
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        public void CanReverseCreateUniqueConstraint()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateAndDropTableMigration());

                    runner.Up(new TestCreateUniqueConstraintWithReversing());
                    processor.ConstraintExists(null, "TestTable2", "TestUnique").ShouldBeTrue();

                    runner.Down(new TestCreateUniqueConstraintWithReversing());
                    processor.ConstraintExists(null, "TestTable2", "TestUnique").ShouldBeFalse();

                    runner.Down(new TestCreateAndDropTableMigration());
                },
                true,
                typeof(SQLiteProcessor),
                typeof(SqlAnywhereProcessor));
        }

        [Test]
        [Category("Firebird")]
        [Category("MySql")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        public void CanReverseCreateUniqueConstraintWithSchema()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestCreateSchema());

                    runner.Up(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Up(new TestCreateUniqueConstraintWithSchemaWithReversing());
                    processor.ConstraintExists("TestSchema", "TestTable2", "TestUnique").ShouldBeTrue();

                    runner.Down(new TestCreateUniqueConstraintWithSchemaWithReversing());
                    processor.ConstraintExists("TestSchema", "TestTable2", "TestUnique").ShouldBeFalse();

                    runner.Down(new TestCreateAndDropTableMigrationWithSchema());

                    runner.Down(new TestCreateSchema());

                },
                true,
                typeof(SQLiteProcessor),
                typeof(SqlAnywhereProcessor));
        }

        [Test]
        [Category("MySql")]
        [Category("SQLite")]
        [Category("Postgres")]
        [Category("SqlServer2005")]
        [Category("SqlServer2008")]
        [Category("SqlServer2012")]
        [Category("SqlServer2014")]
        [Category("SqlServer2016")]
        [Category("SqlAnywhere16")]
        public void CanExecuteSql()
        {
            ExecuteWithSupportedProcessors(
                processor =>
                {
                    var serviceProvider = processor.CreateServices()
                        .WithRunnerContext(_runnerContext)
                        .WithMigrationsIn(typeof(TestMigration).Namespace)
                        .BuildServiceProvider();

                    var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

                    runner.Up(new TestExecuteSql());
                    runner.Down(new TestExecuteSql());

                }, true,
                typeof(FirebirdProcessor));
        }

        private MigrationRunner SetupMigrationRunner(IMigrationProcessor processor)
        {
            var runnerContext = new RunnerContext(new TextWriterAnnouncer(TestContext.Out))
            {
                Namespace = "FluentMigrator.Tests.Integration.Migrations",
                AllowBreakingChange = true,
            };

            var services = ServiceCollectionExtensions.CreateServiceCollection();

            var serviceProvider = services
                .AddScoped<IRunnerContext>(sp => runnerContext)
                .WithMigrationsIn(runnerContext.Namespace)
                .WithProcessor(processor).BuildServiceProvider();
            var runner = (MigrationRunner)serviceProvider.GetRequiredService<IMigrationRunner>();

            return runner;
        }

        private void CleanupTestSqlServerDatabase(SqlConnection connection, SqlServerProcessor origProcessor)
        {
            if (origProcessor.WasCommitted)
            {
                connection.Close();

                var dbTypes = new List<string>
                {
                    origProcessor.DatabaseType
                };
                dbTypes.AddRange(origProcessor.DatabaseTypeAliases);

                var cleanupProcessor = new SqlServerProcessor(dbTypes, connection, new SqlServer2008Generator(), new TextWriterAnnouncer(TestContext.Out), new ProcessorOptions(), new SqlServerDbFactory());
                MigrationRunner cleanupRunner = SetupMigrationRunner(cleanupProcessor);
                cleanupRunner.RollbackToVersion(0);

            }
            else
            {
                origProcessor.RollbackTransaction();
            }
        }
    }


    internal class TestForeignKeyNamingConvention : Migration
    {
        public override void Up()
        {
            Create.Table("Users")
                .WithColumn("UserId").AsInt32().Identity().PrimaryKey()
                .WithColumn("GroupId").AsInt32().NotNullable()
                .WithColumn("UserName").AsString(32).NotNullable()
                .WithColumn("Password").AsString(32).NotNullable();

            Create.Table("Groups")
                .WithColumn("GroupId").AsInt32().Identity().PrimaryKey()
                .WithColumn("Name").AsString(32).NotNullable();

            Create.ForeignKey().FromTable("Users").ForeignColumn("GroupId").ToTable("Groups").PrimaryColumn("GroupId");
        }

        public override void Down()
        {
            Delete.Table("Users");
            Delete.Table("Groups");
        }
    }

    internal class TestUniqueConstraintNamingConvention : Migration
    {
        public override void Up()
        {
            Create.Table("Users")
                .WithColumn("UserId").AsInt32().Identity().PrimaryKey()
                .WithColumn("GroupId").AsInt32().NotNullable()
                .WithColumn("AccountId").AsInt32().NotNullable()
                .WithColumn("UserName").AsString(32).NotNullable()
                .WithColumn("Password").AsString(32).NotNullable();

            Create.UniqueConstraint().OnTable("Users").Column("GroupId");
            Create.UniqueConstraint().OnTable("Users").Column("AccountId");

        }

        public override void Down()
        {
            Delete.UniqueConstraint("UC_Users_GroupId").FromTable("Users");
            Delete.UniqueConstraint().FromTable("Users").Column("AccountId");
            Delete.Table("Users");
        }
    }

    internal class TestIndexNamingConvention : Migration
    {
        public override void Up()
        {
            Create.Table("Users")
                .WithColumn("UserId").AsInt32().Identity().PrimaryKey()
                .WithColumn("GroupId").AsInt32().NotNullable()
                .WithColumn("UserName").AsString(32).NotNullable()
                .WithColumn("Password").AsString(32).NotNullable();

            Create.Index().OnTable("Users").OnColumn("GroupId").Ascending();
        }

        public override void Down()
        {
            Delete.Index("IX_Users_GroupId").OnTable("Users").OnColumn("GroupId");
            Delete.Table("Users");
        }
    }

    internal class TestForeignKeySilentFailure : Migration
    {
        public override void Up()
        {
            Create.Table("Users")
                .WithColumn("UserId").AsInt32().Identity().PrimaryKey()
                .WithColumn("GroupId").AsInt32().NotNullable()
                .WithColumn("UserName").AsString(32).NotNullable()
                .WithColumn("Password").AsString(32).NotNullable();

            Create.Table("Groups")
                .WithColumn("GroupId").AsInt32().Identity().PrimaryKey()
                .WithColumn("Name").AsString(32).NotNullable();

            Create.ForeignKey("FK_Foo").FromTable("Users").ForeignColumn("GroupId").ToTable("Groups").PrimaryColumn("GroupId");
        }

        public override void Down()
        {
            Delete.ForeignKey("FK_Foo").OnTable("Users");
            Delete.Table("Users");
            Delete.Table("Groups");
        }
    }

    internal class TestCreateAndDropTableMigration : Migration
    {
        public override void Up()
        {
            Create.Table("TestTable")
                .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn("Name").AsString(255).NotNullable().WithDefaultValue("Anonymous");

            Create.Table("TestTable2")
                .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn("Name").AsString(255).Nullable()
                .WithColumn("TestTableId").AsInt32().NotNullable();

            Create.Index("ix_Name").OnTable("TestTable2").OnColumn("Name").Ascending()
                .WithOptions().NonClustered();

            Create.Column("Name2").OnTable("TestTable2").AsBoolean().Nullable();

            Create.ForeignKey("fk_TestTable2_TestTableId_TestTable_Id")
                .FromTable("TestTable2").ForeignColumn("TestTableId")
                .ToTable("TestTable").PrimaryColumn("Id");

            Insert.IntoTable("TestTable").Row(new { Name = "Test" });
        }

        public override void Down()
        {
            Delete.Table("TestTable2");
            Delete.Table("TestTable");
        }
    }

    internal class TestRenameTableMigration : AutoReversingMigration
    {
        public override void Up()
        {
            Rename.Table("TestTable2").To("TestTable'3");
        }
    }

    internal class TestRenameColumnMigration : AutoReversingMigration
    {
        public override void Up()
        {
            Rename.Column("Name").OnTable("TestTable2").To("Name'3");
        }
    }

    internal class TestCreateAndDropIndexMigration : Migration
    {
        public override void Up()
        {
            Create.Index("IX_TestTable_Name").OnTable("TestTable").OnColumn("Name");
        }

        public override void Down()
        {
            Delete.Index("IX_TestTable_Name").OnTable("TestTable");
        }
    }


    internal class TestForeignKeyNamingConventionWithSchema : Migration
    {
        public override void Up()
        {
            var createSchemaExpr = Create.Schema("TestSchema");
            IfDatabase(t => t.StartsWith("SqlAnywhere"))
                .Delegate(() => createSchemaExpr.Password("TestSchemaPassword"));
            Create.Table("Users")
                .InSchema("TestSchema")
                .WithColumn("UserId").AsInt32().Identity().PrimaryKey()
                .WithColumn("GroupId").AsInt32().NotNullable()
                .WithColumn("UserName").AsString(32).NotNullable()
                .WithColumn("Password").AsString(32).NotNullable();

            Create.Table("Groups")
                .InSchema("TestSchema")
                .WithColumn("GroupId").AsInt32().Identity().PrimaryKey()
                .WithColumn("Name").AsString(32).NotNullable();

            Create.ForeignKey().FromTable("Users").InSchema("TestSchema").ForeignColumn("GroupId").ToTable("Groups").InSchema("TestSchema").PrimaryColumn("GroupId");
        }

        public override void Down()
        {
            Delete.Table("Users").InSchema("TestSchema");
            Delete.Table("Groups").InSchema("TestSchema");
            Delete.Schema("TestSchema");
        }
    }

    internal class TestIndexNamingConventionWithSchema : Migration
    {
        public override void Up()
        {
            var createSchemaExpr = Create.Schema("TestSchema");
            IfDatabase(t => t.StartsWith("SqlAnywhere"))
                .Delegate(() => createSchemaExpr.Password("TestSchemaPassword"));

            Create.Table("Users")
                .InSchema("TestSchema")
                .WithColumn("UserId").AsInt32().Identity().PrimaryKey()
                .WithColumn("GroupId").AsInt32().NotNullable()
                .WithColumn("UserName").AsString(32).NotNullable()
                .WithColumn("Password").AsString(32).NotNullable();

            Create.Index().OnTable("Users").InSchema("TestSchema").OnColumn("GroupId").Ascending();
        }

        public override void Down()
        {
            Delete.Index("IX_Users_GroupId").OnTable("Users").InSchema("TestSchema").OnColumn("GroupId");
            Delete.Table("Users").InSchema("TestSchema");
            Delete.Schema("TestSchema");
        }
    }

    internal class TestCreateAndDropTableMigrationWithSchema : Migration
    {
        public override void Up()
        {
            Create.Table("TestTable")
                .InSchema("TestSchema")
                .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn("Name").AsString(255).NotNullable().WithDefaultValue("Anonymous");

            Create.Table("TestTable2")
                .InSchema("TestSchema")
                .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn("Name").AsString(255).Nullable()
                .WithColumn("TestTableId").AsInt32().NotNullable();

            Create.Index("ix_Name").OnTable("TestTable2").InSchema("TestSchema").OnColumn("Name").Ascending()
                .WithOptions().NonClustered();

            Create.Column("Name2").OnTable("TestTable2").InSchema("TestSchema").AsString(10).Nullable();

            Create.ForeignKey("fk_TestTable2_TestTableId_TestTable_Id")
                .FromTable("TestTable2").InSchema("TestSchema").ForeignColumn("TestTableId")
                .ToTable("TestTable").InSchema("TestSchema").PrimaryColumn("Id");

            Insert.IntoTable("TestTable").InSchema("TestSchema").Row(new { Name = "Test" });
        }

        public override void Down()
        {
            Delete.Table("TestTable2").InSchema("TestSchema");
            Delete.Table("TestTable").InSchema("TestSchema");
        }
    }

    internal class TestRenameTableMigrationWithSchema : AutoReversingMigration
    {
        public override void Up()
        {
            Rename.Table("TestTable2").InSchema("TestSchema").To("TestTable'3");
        }
    }

    internal class TestRenameColumnMigrationWithSchema : AutoReversingMigration
    {
        public override void Up()
        {
            Rename.Column("Name").OnTable("TestTable2").InSchema("TestSchema").To("Name'3");
        }
    }

    internal class TestCreateAndDropIndexMigrationWithSchema : Migration
    {
        public override void Up()
        {
            Create.Index("IX_TestTable_Name").OnTable("TestTable").InSchema("TestSchema").OnColumn("Name");
        }

        public override void Down()
        {
            Delete.Index("IX_TestTable_Name").OnTable("TestTable").InSchema("TestSchema");
        }
    }

    internal class TestCreateSchema : Migration
    {
        public override void Up()
        {
            var createSchemaExpr = Create.Schema("TestSchema");
            IfDatabase(t => t.StartsWith("SqlAnywhere"))
                .Delegate(() => createSchemaExpr.Password("TestSchemaPassword"));
        }

        public override void Down()
        {
            Delete.Schema("TestSchema");
        }
    }

    internal class TestCreateSequence : Migration
    {
        public override void Up()
        {
            Create.Sequence("TestSequence").StartWith(1).IncrementBy(1).MinValue(0).MaxValue(1000).Cycle().Cache(10);
        }

        public override void Down()
        {
            Delete.Sequence("TestSequence");
        }
    }

    internal class TestCreateSequenceWithSchema : Migration
    {
        public override void Up()
        {
            Create.Sequence("TestSequence").InSchema("TestSchema").StartWith(1).IncrementBy(1).MinValue(0).MaxValue(1000).Cycle().Cache(10);
        }

        public override void Down()
        {
            Delete.Sequence("TestSequence").InSchema("TestSchema");
        }
    }

    internal class TestAlterColumnWithSchema: Migration
    {
        public override void Up()
        {
            Alter.Column("Name2").OnTable("TestTable2").InSchema("TestSchema").AsAnsiString(100).Nullable();
        }

        public override void Down()
        {
            Alter.Column("Name2").OnTable("TestTable2").InSchema("TestSchema").AsString(10).Nullable();
        }
    }

    internal class TestAlterTableWithSchema : Migration
    {
        public override void Up()
        {
            Alter.Table("TestTable2").InSchema("TestSchema").AddColumn("NewColumn").AsInt32().WithDefaultValue(1);
        }

        public override void Down()
        {
            Delete.Column("NewColumn").FromTable("TestTable2").InSchema("TestSchema");
        }
    }

    internal class TestAlterSchema : Migration
    {
        public override void Up()
        {
            var createSchemaExpr = Create.Schema("NewSchema");
            IfDatabase(t => t.StartsWith("SqlAnywhere"))
                .Delegate(() => createSchemaExpr.Password("NewSchemaPassword"));
            Alter.Table("TestTable").InSchema("TestSchema").ToSchema("NewSchema");
        }

        public override void Down()
        {
            Alter.Table("TestTable").InSchema("NewSchema").ToSchema("TestSchema");
            Delete.Schema("NewSchema");
        }
    }

    internal class TestCreateUniqueConstraint : Migration
    {
        public override void Up()
        {
            Create.UniqueConstraint("TestUnique").OnTable("TestTable2").Column("Name");
        }

        public override void Down()
        {
            Delete.UniqueConstraint("TestUnique").FromTable("TestTable2");
        }
    }

    internal class TestCreateUniqueConstraintWithSchema : Migration
    {
        public override void Up()
        {
            Create.UniqueConstraint("TestUnique").OnTable("TestTable2").WithSchema("TestSchema").Column("Name");
        }

        public override void Down()
        {
            Delete.UniqueConstraint("TestUnique").FromTable("TestTable2").InSchema("TestSchema");
        }
    }

    internal class TestUpdateData : Migration
    {
        public override void Up()
        {
            Update.Table("TestTable").InSchema("TestSchema").Set(new { Name = "Updated" }).AllRows();
        }

        public override void Down()
        {
            Update.Table("TestTable").InSchema("TestSchema").Set(new { Name = "Test" }).AllRows();
        }
    }

    internal class TestDeleteData : Migration
    {
        public override void Up()
        {
            Delete.FromTable("TestTable").Row(new { Name = "Test" });
        }

        public override void Down()
        {
            Insert.IntoTable("TestTable").Row(new { Name = "Test" });
        }
    }

    internal class TestDeleteDataWithSchema :Migration
    {
        public override void Up()
        {
            Delete.FromTable("TestTable").InSchema("TestSchema").Row(new { Name = "Test"});
        }

        public override void Down()
        {
            Insert.IntoTable("TestTable").InSchema("TestSchema").Row(new { Name = "Test" });
        }
    }

    internal class TestCreateIndexWithReversing : AutoReversingMigration
    {
        public override void Up()
        {
            Create.Index().OnTable("TestTable2").InSchema("TestSchema").OnColumn("Name2").Ascending();
        }
    }

    internal class TestCreateUniqueConstraintWithReversing : AutoReversingMigration
    {
        public override void Up()
        {
            Create.UniqueConstraint("TestUnique").OnTable("TestTable2").Column("Name");
        }
    }

    internal class TestCreateUniqueConstraintWithSchemaWithReversing : AutoReversingMigration
    {
        public override void Up()
        {
            Create.UniqueConstraint("TestUnique").OnTable("TestTable2").WithSchema("TestSchema").Column("Name");
        }
    }

    internal class TestExecuteSql : Migration
    {
        public override void Up()
        {
            Execute.Sql("select 1");
        }

        public override void Down()
        {
            Execute.Sql("select 2");
        }
    }
}
