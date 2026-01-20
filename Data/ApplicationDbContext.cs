using Microsoft.EntityFrameworkCore;
using LichCongTacWeb.Models;

namespace LichCongTacWeb.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // Bổ sung DbSet cho bảng Roles
        public DbSet<Role> Roles { get; set; }
        public DbSet<Location> Locations { get; set; }

        public DbSet<Leader> Leaders { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<WorkShift> WorkShifts { get; set; }
        public DbSet<ShiftAssignment> ShiftAssignments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình tên bảng trong Database (đảm bảo khớp với SQL bạn đã chạy)
            modelBuilder.Entity<Role>().ToTable("roles");
            modelBuilder.Entity<Leader>().ToTable("leaders");
            modelBuilder.Entity<Schedule>().ToTable("schedules");
            modelBuilder.Entity<Department>().ToTable("departments");

            // Cấu hình quan hệ giữa Leader và Role (Một Role có nhiều Leader)
            modelBuilder.Entity<Leader>()
                .HasOne(l => l.Role)
                .WithMany(r => r.Leaders)
                .HasForeignKey(l => l.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Cấu hình quan hệ giữa Leader và Department
            modelBuilder.Entity<Leader>()
                .HasOne(l => l.Department)
                .WithMany(d => d.Leaders)
                .HasForeignKey(l => l.DepartmentId);
        }
    }
}