using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<UserState> UserStates { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<Friendship> Friendships { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<GroupInvite> GroupInvites { get; set; }
    public DbSet<Meeting> Meetings { get; set; }
    public DbSet<MeetingInvite> Invites { get; set; }
    public DbSet<BaseScheduleEntry> BaseScheduleEntries { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Friendship>()
            .HasKey(f => new { f.RequesterId, f.AddresseeId });

        modelBuilder.Entity<Friendship>()
            .HasOne(f => f.Requester)
            .WithMany(u => u.SentFriendRequests)
            .HasForeignKey(f => f.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Friendship>()
            .HasOne(f => f.Addressee)
            .WithMany(u => u.ReceivedFriendRequests)
            .HasForeignKey(f => f.AddresseeId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<GroupMember>()
            .HasKey(gm => new { gm.GroupId, gm.UserId });

        modelBuilder.Entity<GroupInvite>()
            .HasKey(gi => new { gi.GroupId, gi.InviteeId });

        modelBuilder.Entity<MeetingInvite>()
            .HasKey(i => new { i.MeetingId, i.UserId });

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.CreatedAt });

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();
    }
}
