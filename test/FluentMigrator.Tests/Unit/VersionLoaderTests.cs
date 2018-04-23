#region License
//
// Copyright (c) 2018, Fluent Migrator Project
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
using System.Linq;
using System.Reflection;

using FluentMigrator.Expressions;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.VersionTableInfo;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using NUnit.Framework;

using Shouldly;

namespace FluentMigrator.Tests.Unit
{
    public class TestMigrationProcessorOptions : IMigrationProcessorOptions
    {
        public bool PreviewOnly
        {
            get { return false; }
        }

        public int? Timeout
        {
            get { return 30; }
        }

        public string ProviderSwitches
        {
            get
            {
                return string.Empty;
            }
        }
    }

    [TestFixture]
    public class VersionLoaderTests
    {
        [Test]
        public void CanLoadCustomVersionTableMetaData()
        {
            var runnerContext = new Mock<IRunnerContext>();

            var runner = new Mock<IMigrationRunner>();
            runner.SetupGet(r => r.Processor.Options).Returns(new TestMigrationProcessorOptions());
            runner.SetupGet(r => r.RunnerContext).Returns(runnerContext.Object);

            var asm = Assembly.GetExecutingAssembly();

            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => runner.Object)
                .AddScoped(_ => runnerContext.Object)
                .AddScoped(_ => ConventionSets.NoSchemaName)
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()
                .AddVersionTableMetaData(sp => new[] { asm }.GetVersionTableMetaDataType(sp))
                .AddScoped<IVersionLoader, VersionLoader>()
                .BuildServiceProvider();

            var loader = serviceProvider.GetRequiredService<IVersionLoader>();

            var versionTableMetaData = loader.GetVersionTableMetaData();
            versionTableMetaData.ShouldBeOfType<TestVersionTableMetaData>();
        }

        [Test]
        public void CanLoadDefaultVersionTableMetaData()
        {
            var runnerContext = new Mock<IRunnerContext>();

            var runner = new Mock<IMigrationRunner>();
            runner.SetupGet(r => r.Processor.Options).Returns(new TestMigrationProcessorOptions());
            runner.SetupGet(r => r.RunnerContext).Returns(runnerContext.Object);

            var asm = "s".GetType().Assembly;

            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => runner.Object)
                .AddScoped(_ => runnerContext.Object)
                .AddScoped(_ => ConventionSets.NoSchemaName)
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()
                .AddVersionTableMetaData(sp => new[] { asm }.GetVersionTableMetaDataType(sp))
                .AddScoped<IVersionLoader, VersionLoader>()
                .BuildServiceProvider();

            var loader = serviceProvider.GetRequiredService<IVersionLoader>();

            var versionTableMetaData = loader.GetVersionTableMetaData();
            versionTableMetaData.ShouldBeOfType<DefaultVersionTableMetaData>();
        }

        [Test]
        [Obsolete]
        public void CanSetupApplicationContext()
        {
            var applicationContext = "Test context";

            var runnerContext = new Mock<IRunnerContext>();
            runnerContext.SetupGet(r => r.ApplicationContext).Returns(applicationContext);

            var runner = new Mock<IMigrationRunner>();
            runner.SetupGet(r => r.Processor.Options).Returns(new TestMigrationProcessorOptions());
            runner.SetupGet(r => r.RunnerContext).Returns(runnerContext.Object);

            var asm = Assembly.GetExecutingAssembly();

            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => runner.Object)
                .AddScoped(_ => runnerContext.Object)
                .AddScoped(_ => ConventionSets.NoSchemaName)
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()
                .AddVersionTableMetaData(sp => new[] { asm }.GetVersionTableMetaDataType(sp))
                .AddScoped<IVersionLoader, VersionLoader>()
                .BuildServiceProvider();

            var loader = serviceProvider.GetRequiredService<IVersionLoader>();

            var versionTableMetaData = loader.GetVersionTableMetaData();
            versionTableMetaData.ApplicationContext.ShouldBe(applicationContext);
        }

        [Test]
        public void DeleteVersionShouldExecuteDeleteDataExpression()
        {
            var runnerContext = new Mock<IRunnerContext>();

            var processor = new Mock<IMigrationProcessor>();
            var runner = new Mock<IMigrationRunner>();
            runner.SetupGet(r => r.Processor).Returns(processor.Object);
            runner.SetupGet(r => r.RunnerContext).Returns(runnerContext.Object);

            var asm = Assembly.GetExecutingAssembly();

            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => runner.Object)
                .AddScoped(_ => runnerContext.Object)
                .AddScoped(_ => ConventionSets.NoSchemaName)
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()
                .AddVersionTableMetaData(sp => new[] { asm }.GetVersionTableMetaDataType(sp))
                .AddScoped<IVersionLoader, VersionLoader>()
                .BuildServiceProvider();

            var loader = serviceProvider.GetRequiredService<IVersionLoader>();

            processor.Setup(p => p.Process(It.Is<DeleteDataExpression>(expression =>
                                                                       expression.SchemaName == loader.VersionTableMetaData.SchemaName
                                                                       && expression.TableName == loader.VersionTableMetaData.TableName
                                                                       && expression.Rows.All(
                                                                           definition =>
                                                                           definition.All(
                                                                               pair =>
                                                                               pair.Key == loader.VersionTableMetaData.ColumnName && pair.Value.Equals(1L))))))
                .Verifiable();

            loader.DeleteVersion(1);

            processor.VerifyAll();
        }

        [Test]
        public void RemoveVersionTableShouldBehaveAsExpected()
        {
            var runnerContext = new Mock<IRunnerContext>();
            var processor = new Mock<IMigrationProcessor>();
            var runner = new Mock<IMigrationRunner>();
            runner.SetupGet(r => r.Processor).Returns(processor.Object);
            runner.SetupGet(r => r.RunnerContext).Returns(runnerContext.Object);

            var asm = Assembly.GetExecutingAssembly();

            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => runner.Object)
                .AddScoped(_ => runnerContext.Object)
                .AddScoped(_ => ConventionSets.NoSchemaName)
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()
                .AddVersionTableMetaData(sp => new[] { asm }.GetVersionTableMetaDataType(sp))
                .AddScoped<IVersionLoader, VersionLoader>()
                .BuildServiceProvider();

            var loader = serviceProvider.GetRequiredService<IVersionLoader>();

            processor.Setup(p => p.Process(It.Is<DeleteTableExpression>(expression =>
                                                                        expression.SchemaName == loader.VersionTableMetaData.SchemaName
                                                                        && expression.TableName == loader.VersionTableMetaData.TableName)))
                .Verifiable();

            processor.Setup(p => p.Process(It.Is<DeleteSchemaExpression>(expression =>
                                                                         expression.SchemaName == loader.VersionTableMetaData.SchemaName)))
                .Verifiable();

            loader.RemoveVersionTable();

            processor.VerifyAll();
        }

        [Test]
        public void RemoveVersionTableShouldNotRemoveSchemaIfItDidNotOwnTheSchema()
        {
            var runnerContext = new Mock<IRunnerContext>();
            var processor = new Mock<IMigrationProcessor>();
            var runner = new Mock<IMigrationRunner>();
            runner.SetupGet(r => r.Processor).Returns(processor.Object);
            runner.SetupGet(r => r.RunnerContext).Returns(runnerContext.Object);

            var asm = Assembly.GetExecutingAssembly();

            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => runner.Object)
                .AddScoped(_ => runnerContext.Object)
                .AddScoped(_ => ConventionSets.NoSchemaName)
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()
                .AddVersionTableMetaData(sp => new[] { asm }.GetVersionTableMetaDataType(sp))
                .AddScoped<IVersionLoader, VersionLoader>()
                .BuildServiceProvider();

            var loader = serviceProvider.GetRequiredService<IVersionLoader>();

            ((TestVersionTableMetaData) loader.VersionTableMetaData).OwnsSchema = false;

            processor.Setup(p => p.Process(It.Is<DeleteTableExpression>(expression =>
                                                                        expression.SchemaName == loader.VersionTableMetaData.SchemaName
                                                                        && expression.TableName == loader.VersionTableMetaData.TableName)))
                .Verifiable();

            loader.RemoveVersionTable();

            processor.Verify(p => p.Process(It.IsAny<DeleteSchemaExpression>()), Times.Never());
        }

        [Test]
        public void UpdateVersionShouldExecuteInsertDataExpression()
        {
            var runnerContext = new Mock<IRunnerContext>();
            var processor = new Mock<IMigrationProcessor>();
            var runner = new Mock<IMigrationRunner>();
            runner.SetupGet(r => r.Processor).Returns(processor.Object);
            runner.SetupGet(r => r.RunnerContext).Returns(runnerContext.Object);

            var asm = Assembly.GetExecutingAssembly();

            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => runner.Object)
                .AddScoped(_ => runnerContext.Object)
                .AddScoped(_ => ConventionSets.NoSchemaName)
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()
                .AddVersionTableMetaData(sp => new[] { asm }.GetVersionTableMetaDataType(sp))
                .AddScoped<IVersionLoader, VersionLoader>()
                .BuildServiceProvider();

            var loader = serviceProvider.GetRequiredService<IVersionLoader>();

            processor.Setup(p => p.Process(It.Is<InsertDataExpression>(expression =>
                                                                       expression.SchemaName == loader.VersionTableMetaData.SchemaName
                                                                       && expression.TableName == loader.VersionTableMetaData.TableName
                                                                       && expression.Rows.Any(
                                                                           definition =>
                                                                           definition.Any(
                                                                               pair =>
                                                                               pair.Key == loader.VersionTableMetaData.ColumnName && pair.Value.Equals(1L))))))
                .Verifiable();

            loader.UpdateVersionInfo(1);

            processor.VerifyAll();
        }

        [Test]
        public void VersionSchemaMigrationOnlyRunOnceEvenIfExistenceChecksReturnFalse()
        {
            var runnerContext = new Mock<IRunnerContext>();
            var processor = new Mock<IMigrationProcessor>();
            var runner = new Mock<IMigrationRunner>();
            var asm = Assembly.GetExecutingAssembly();

            runner.SetupGet(r => r.Processor).Returns(processor.Object);
            runner.SetupGet(r => r.RunnerContext).Returns(runnerContext.Object);

            processor.Setup(p => p.SchemaExists(It.IsAny<string>())).Returns(false);

            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => runner.Object)
                .AddScoped(_ => runnerContext.Object)
                .AddScoped(_ => ConventionSets.NoSchemaName)
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()
                .AddVersionTableMetaData(sp => new[] { asm }.GetVersionTableMetaDataType(sp))
                .AddScoped<VersionLoader>()
                .BuildServiceProvider();

            var loader = serviceProvider.GetRequiredService<VersionLoader>();

            loader.LoadVersionInfo();

            runner.Verify(r => r.Up(loader.VersionSchemaMigration), Times.Once());
        }

        [Test]
        public void VersionMigrationOnlyRunOnceEvenIfExistenceChecksReturnFalse()
        {
            var runnerContext = new Mock<IRunnerContext>();
            var processor = new Mock<IMigrationProcessor>();
            var runner = new Mock<IMigrationRunner>();
            var asm = Assembly.GetExecutingAssembly();

            runner.SetupGet(r => r.Processor).Returns(processor.Object);
            runner.SetupGet(r => r.RunnerContext).Returns(runnerContext.Object);

            processor.Setup(p => p.TableExists(new TestVersionTableMetaData().SchemaName, TestVersionTableMetaData.TABLENAME)).Returns(false);

            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => runner.Object)
                .AddScoped(_ => runnerContext.Object)
                .AddScoped(_ => ConventionSets.NoSchemaName)
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()
                .AddVersionTableMetaData(sp => new[] { asm }.GetVersionTableMetaDataType(sp))
                .AddScoped<VersionLoader>()
                .BuildServiceProvider();

            var loader = serviceProvider.GetRequiredService<VersionLoader>();

            loader.LoadVersionInfo();

            runner.Verify(r => r.Up(loader.VersionMigration), Times.Once());
        }

        [Test]
        public void VersionUniqueMigrationOnlyRunOnceEvenIfExistenceChecksReturnFalse()
        {
            var runnerContext = new Mock<IRunnerContext>();
            var processor = new Mock<IMigrationProcessor>();
            var runner = new Mock<IMigrationRunner>();
            var asm = Assembly.GetExecutingAssembly();

            runner.SetupGet(r => r.Processor).Returns(processor.Object);
            runner.SetupGet(r => r.RunnerContext).Returns(runnerContext.Object);

            processor.Setup(p => p.ColumnExists(new TestVersionTableMetaData().SchemaName, TestVersionTableMetaData.TABLENAME, TestVersionTableMetaData.APPLIEDONCOLUMNNAME)).Returns(false);

            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => runner.Object)
                .AddScoped(_ => runnerContext.Object)
                .AddScoped(_ => ConventionSets.NoSchemaName)
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()
                .AddVersionTableMetaData(sp => new[] { asm }.GetVersionTableMetaDataType(sp))
                .AddScoped<VersionLoader>()
                .BuildServiceProvider();

            var loader = serviceProvider.GetRequiredService<VersionLoader>();

            loader.LoadVersionInfo();

            runner.Verify(r => r.Up(loader.VersionUniqueMigration), Times.Once());
        }

        [Test]
        public void VersionDescriptionMigrationOnlyRunOnceEvenIfExistenceChecksReturnFalse()
        {
            var runnerContext = new Mock<IRunnerContext>();
            var processor = new Mock<IMigrationProcessor>();
            var runner = new Mock<IMigrationRunner>();
            var asm = Assembly.GetExecutingAssembly();

            runner.SetupGet(r => r.Processor).Returns(processor.Object);
            runner.SetupGet(r => r.RunnerContext).Returns(runnerContext.Object);

            processor.Setup(p => p.ColumnExists(new TestVersionTableMetaData().SchemaName, TestVersionTableMetaData.TABLENAME, TestVersionTableMetaData.APPLIEDONCOLUMNNAME)).Returns(false);

            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => runner.Object)
                .AddScoped(_ => runnerContext.Object)
                .AddScoped(_ => ConventionSets.NoSchemaName)
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()
                .AddVersionTableMetaData(sp => new[] { asm }.GetVersionTableMetaDataType(sp))
                .AddScoped<VersionLoader>()
                .BuildServiceProvider();

            var loader = serviceProvider.GetRequiredService<VersionLoader>();

            loader.LoadVersionInfo();

            runner.Verify(r => r.Up(loader.VersionDescriptionMigration), Times.Once());
        }
    }
}