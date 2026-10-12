using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Zaster.Accounts;
using Zaster.Backup;
using Zaster.Categories;
using Zaster.Transactions;
using Zaster.Users;

namespace Zaster.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<TransactionSplit> TransactionSplits => Set<TransactionSplit>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Rule> Rules => Set<Rule>();

    public DbSet<RuleCondition> RuleConditions => Set<RuleCondition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasIndex(u => u.Name).IsUnique();

        modelBuilder.Entity<Account>(account =>
        {
            account.HasMany(a => a.Users)
                .WithMany(u => u.Accounts)
                .UsingEntity<Dictionary<string, object>>("AccountUsers");

            // Ein Bankkonto gibt es nur einmal; weitere Nutzer kommen als Mitglieder dazu.
            account.HasIndex(a => a.Iban).IsUnique();
        });

        modelBuilder.Entity<Transaction>(transaction =>
        {
            transaction.HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            transaction.HasMany(t => t.Splits)
                .WithOne(s => s.Transaction)
                .HasForeignKey(s => s.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            transaction.Property(t => t.Source).HasConversion<string>();
            transaction.HasIndex(t => new { t.AccountId, t.Buchung });
        });

        modelBuilder.Entity<TransactionSplit>(split =>
        {
            split.HasOne(s => s.Category)
                .WithMany(c => c.Splits)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            split.HasIndex(s => new { s.TransactionId, s.CategoryId }).IsUnique();
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

            rule.HasMany(r => r.Conditions)
                .WithOne()
                .HasForeignKey(c => c.RuleId)
                .OnDelete(DeleteBehavior.Cascade);

            rule.Property(r => r.Direction).HasConversion<string>();
        });

        modelBuilder.Entity<RuleCondition>().Property(c => c.Field).HasConversion<string>();

        // Die eine Zeile mit den Einstellungen der automatischen Sicherung.
        modelBuilder.Entity<BackupSettings>().HasData(new BackupSettings { Id = BackupSettings.SingletonId });
    }
}
