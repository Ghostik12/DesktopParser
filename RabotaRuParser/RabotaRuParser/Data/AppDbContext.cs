using Microsoft.EntityFrameworkCore;
using RabotaRuParser.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabotaRuParser.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Vacancy> Vacancies { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) // Передаем options в базовый конструктор
        {
        }

        // Путь к БД (автоматически в папке AppData/Local)
        private string DbPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RabotaRuParser.db");

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Настройка SQLite вместо PostgreSQL
            optionsBuilder.UseSqlite($"Data Source={DbPath}");

            // Опционально: логирование запросов (для отладки)
            optionsBuilder.LogTo(message => Debug.WriteLine(message));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Оптимизация для SQLite:
            modelBuilder.Entity<Vacancy>()
                .HasIndex(v => new { v.Company, v.ShortTitle, v.Date });

            modelBuilder.Entity<Vacancy>()
                .HasIndex(v => new { v.Phone, v.Title });

            // Для строковых ID (если SiteId - строка)
            modelBuilder.Entity<Vacancy>()
                .Property(v => v.SiteId)
                .HasColumnType("TEXT COLLATE NOCASE"); // Регистронезависимый поиск
        }
    }
}
