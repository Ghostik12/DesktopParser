using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace WpfApp3
{
    public class AppDbContext : DbContext
    {
        public DbSet<Vacancy> Vacancies { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=RabotaRuDb;Username=postgres;Password=12345Ob@");
        }
    }
}