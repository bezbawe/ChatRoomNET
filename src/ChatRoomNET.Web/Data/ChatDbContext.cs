using ChatRoomNET.Web.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChatRoomNET.Web.Data;

public class ChatDbContext(DbContextOptions<ChatDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Room>(room =>
        {
            room.Property(r => r.Name).HasMaxLength(100).IsRequired();
            room.Property(r => r.InviteCode).HasMaxLength(32);
            room.HasIndex(r => r.InviteCode).IsUnique();

            room.HasOne(r => r.Owner)
                .WithMany()
                .HasForeignKey(r => r.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RoomMember>(member =>
        {
            member.HasKey(m => new { m.RoomId, m.UserId });

            member.HasOne(m => m.Room)
                .WithMany(r => r.Members)
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            member.HasOne(m => m.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Message>(message =>
        {
            message.Property(m => m.Text).HasMaxLength(2000).IsRequired();
            message.HasIndex(m => new { m.RoomId, m.CreatedAt });

            message.HasOne(m => m.Room)
                .WithMany(r => r.Messages)
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            message.HasOne(m => m.User)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
