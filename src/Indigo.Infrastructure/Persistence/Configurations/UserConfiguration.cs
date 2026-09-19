using Indigo.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Indigo.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const int LongitudMaximaNombreCompleto = 200;

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.NombreCompleto)
            .IsRequired()
            .HasMaxLength(LongitudMaximaNombreCompleto);
    }
}
