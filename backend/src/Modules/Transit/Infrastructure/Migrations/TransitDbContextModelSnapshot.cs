using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace Transit.Infrastructure.Migrations;
[DbContext(typeof(TransitDbContext))]
public sealed class TransitDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) => TransitSchemaV1.Configure(modelBuilder);
}
