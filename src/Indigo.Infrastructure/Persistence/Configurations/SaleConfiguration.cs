using System.Globalization;
using Indigo.Domain.Entities;
using Indigo.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Indigo.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    private const int LongitudMaximaUsuarioId = 450;

    private const string FormatoDeFechaUtc = "yyyy-MM-dd HH:mm:ss.FFFFFFFzzz";

    private static readonly ValueConverter<DateTimeOffset, string> ConversorDeFechaUtc = new(
        fecha => fecha.ToUniversalTime().ToString(FormatoDeFechaUtc, CultureInfo.InvariantCulture),
        texto => DateTimeOffset.ParseExact(texto, FormatoDeFechaUtc, CultureInfo.InvariantCulture));

    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales", tabla =>
            tabla.HasCheckConstraint("CK_Sales_Total_Positivo", "\"Total\" > 0"));

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Fecha)
            .IsRequired()
            .HasConversion(ConversorDeFechaUtc);

        builder.Property(s => s.UsuarioId)
            .IsRequired()
            .HasMaxLength(LongitudMaximaUsuarioId);

        builder.Property(s => s.Total)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Ignore(s => s.CantidadItems);

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.Fecha);
        builder.HasIndex(s => new { s.UsuarioId, s.Fecha });
    }
}
