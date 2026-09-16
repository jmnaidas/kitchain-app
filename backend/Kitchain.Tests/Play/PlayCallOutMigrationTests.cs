using Kitchain.Domain.Play;
using Kitchain.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Kitchain.Tests.Play;

public sealed class PlayCallOutMigrationTests
{
    [Fact]
    public void Upgrade_only_extends_the_existing_call_out_constraint()
    {
        var operations = new AddPlayWrongCourtCallOut().UpOperations;
        Assert.Equal(2, operations.Count);
        var drop = Assert.IsType<DropCheckConstraintOperation>(operations[0]);
        var add = Assert.IsType<AddCheckConstraintOperation>(operations[1]);
        Assert.Equal("PlayRallyEvents", drop.Table);
        Assert.Equal("CK_PlayRallyEvents_CallOut", drop.Name);
        Assert.Equal(drop.Table, add.Table);
        Assert.Equal(drop.Name, add.Name);
        Assert.All(Enum.GetNames<PlayRallyCallOut>(), value => Assert.Contains($"'{value}'", add.Sql));
    }
}
