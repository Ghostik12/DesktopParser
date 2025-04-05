using Microsoft.EntityFrameworkCore;
using WpfApp1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Data
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
