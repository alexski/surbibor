using Microsoft.EntityFrameworkCore;
using Surbibor.Domain.Entities;

namespace Surbibor.Infrastructure;

public class SurbiborDbContext(DbContextOptions<SurbiborDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameMembership> GameMemberships => Set<GameMembership>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardSquare> BoardSquares => Set<BoardSquare>();
    public DbSet<SideBet> SideBets => Set<SideBet>();
    public DbSet<SideBetWager> SideBetWagers => Set<SideBetWager>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasIndex(g => g.InviteCode).IsUnique();
        });

        modelBuilder.Entity<GameMembership>(entity =>
        {
            entity.HasKey(m => new { m.GameId, m.UserId });

            entity.HasOne(m => m.Game)
                .WithMany(g => g.Memberships)
                .HasForeignKey(m => m.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasOne(e => e.Game)
                .WithMany(g => g.Events)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Board>(entity =>
        {
            entity.HasIndex(b => new { b.GameId, b.UserId }).IsUnique();

            entity.HasOne(b => b.Game)
                .WithMany(g => g.Boards)
                .HasForeignKey(b => b.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BoardSquare>(entity =>
        {
            entity.HasIndex(s => new { s.BoardId, s.Position }).IsUnique();

            entity.HasOne(s => s.Board)
                .WithMany(b => b.Squares)
                .HasForeignKey(s => s.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.Event)
                .WithMany()
                .HasForeignKey(s => s.EventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SideBet>(entity =>
        {
            entity.HasOne(sb => sb.Game)
                .WithMany(g => g.SideBets)
                .HasForeignKey(sb => sb.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SideBetWager>(entity =>
        {
            entity.HasIndex(w => new { w.SideBetId, w.UserId }).IsUnique();

            entity.HasOne(w => w.SideBet)
                .WithMany(sb => sb.Wagers)
                .HasForeignKey(w => w.SideBetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
