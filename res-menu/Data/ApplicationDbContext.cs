using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using res_menu.Models;

namespace res_menu.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<Restaurant> Restaurants { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        builder.Entity<Restaurant>()
            .HasIndex(r => r.Subdomain)
            .IsUnique();
            
        builder.Entity<MenuItem>()
            .Property(m => m.Price)
            .HasColumnType("decimal(18,2)");
    }
} 