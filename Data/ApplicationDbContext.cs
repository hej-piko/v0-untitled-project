using EsportsTournament.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace EsportsTournament.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<Participant> Participants { get; set; }
        public DbSet<Match> Matches { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure relationships
            builder.Entity<Tournament>()
                .HasOne(t => t.Creator)
                .WithMany(u => u.CreatedTournaments)
                .HasForeignKey(t => t.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Participant>()
                .HasOne(p => p.Tournament)
                .WithMany(t => t.Participants)
                .HasForeignKey(p => p.TournamentId);

            builder.Entity<Participant>()
                .HasOne(p => p.User)
                .WithMany(u => u.Participations)
                .HasForeignKey(p => p.UserId);

            builder.Entity<Match>()
                .HasOne(m => m.Tournament)
                .WithMany(t => t.Matches)
                .HasForeignKey(m => m.TournamentId);

            builder.Entity<Match>()
                .HasOne(m => m.Participant1)
                .WithMany()
                .HasForeignKey(m => m.Participant1Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Match>()
                .HasOne(m => m.Participant2)
                .WithMany()
                .HasForeignKey(m => m.Participant2Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Match>()
                .HasOne(m => m.Winner)
                .WithMany()
                .HasForeignKey(m => m.WinnerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
