using Indigo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Indigo.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    private const int LongitudMaximaNombre = 150;

    private const int LongitudMaximaImagenUrl = 500;

    private const int LongitudMaximaCategoria = 20;

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", tabla =>
        {
            tabla.HasCheckConstraint("CK_Products_Precio_Positivo", "\"Precio\" > 0");
            tabla.HasCheckConstraint("CK_Products_Stock_No_Negativo", "\"Stock\" >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Nombre)
            .IsRequired()
            .HasMaxLength(LongitudMaximaNombre);

        builder.Property(p => p.Precio)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(p => p.Stock)
            .IsRequired();

        builder.Property(p => p.Categoria)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(LongitudMaximaCategoria);

        builder.Property(p => p.ImagenUrl)
            .HasMaxLength(LongitudMaximaImagenUrl);

        builder.Property(p => p.Activo)
            .IsRequired();

        builder.HasIndex(p => p.Nombre);
        builder.HasIndex(p => p.Categoria);
    }
}
