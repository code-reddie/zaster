using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Zaster.Accounts;
using Zaster.Categories;
using Zaster.Transactions;
using Zaster.Users;

namespace Zaster.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Rule> Rules => Set<Rule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>()
            .HasMany(a => a.Users)
            .WithMany(u => u.Accounts)
            .UsingEntity<Dictionary<string, object>>("AccountUsers");

        modelBuilder.Entity<Transaction>(transaction =>
        {
            transaction.HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            transaction.HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            transaction.Property(t => t.Source).HasConversion<string>();
            transaction.HasIndex(t => new { t.AccountId, t.Buchung });
        });

        modelBuilder.Entity<Category>(category =>
        {
            category.HasOne(c => c.Account)
                .WithMany(a => a.Categories)
                .HasForeignKey(c => c.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            category.HasOne(c => c.Parent)
                .WithMany(c => c.Children)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Rule>(rule =>
        {
            rule.HasOne(r => r.Account)
                .WithMany(a => a.Rules)
                .HasForeignKey(r => r.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            rule.HasOne(r => r.Category)
                .WithMany()
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            rule.Property(r => r.Field).HasConversion<string>();
        });
    }
}
