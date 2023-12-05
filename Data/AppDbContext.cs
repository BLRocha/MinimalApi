using Microsoft.EntityFrameworkCore;
using MinimalApi.Data.Configuration;
using MinimalApi.Models;

namespace MinimalApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<Fornecedor>? Fornecedor { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppContext).Assembly);
        }
    }
}
