using Indigo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Indigo.Infrastructure.Persistence.Configurations;

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    private const int LongitudMaximaProductoNombre = 150;

    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems", tabla =>
        {
            tabla.HasCheckConstraint("CK_SaleItems_Cantidad_Positiva", "\"Cantidad\" > 0");
            tabla.HasCheckConstraint("CK_SaleItems_PrecioUnitario_Positivo", "\"PrecioUnitario\" > 0");
            tabla.HasCheckConstraint("CK_SaleItems_Subtotal_No_Negativo", "\"Subtotal\" >= 0");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.SaleId).IsRequired();
        builder.Property(i => i.ProductId).IsRequired();

        builder.Property(i => i.ProductoNombre)
            .IsRequired()
            .HasMaxLength(LongitudMaximaProductoNombre);

        builder.Property(i => i.PrecioUnitario)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(i => i.Cantidad)
            .IsRequired();

        builder.Property(i => i.Subtotal)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.SaleId);
        builder.HasIndex(i => i.ProductId);
    }
}
