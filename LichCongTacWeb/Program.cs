using Microsoft.EntityFrameworkCore;
using LichCongTacWeb.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// --- 1. ĐĂNG KÝ CÁC DỊCH VỤ (SERVICES) ---

builder.Services.AddControllersWithViews();

// Cấu hình Xác thực bằng Cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Cookie.Name = "LichCongTac.Auth";
    });

// Cấu hình Phân quyền (MỚI BỔ SUNG để hỗ trợ Thư ký/Admin)
builder.Services.AddAuthorization(options =>
{
    // Tạo chính sách: Chỉ những ai có Role là Admin hoặc Thư ký mới được quản lý lịch
    options.AddPolicy("AdminOrSecretary", policy =>
        policy.RequireRole("Admin", "Thư ký"));
});

// Hỗ trợ truy cập thông tin User trong View
builder.Services.AddHttpContextAccessor();

// Cấu hình Session
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Kết nối MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var app = builder.Build();

// --- 2. CẤU HÌNH PIPELINE (MIDDLEWARE) ---

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Thứ tự cực kỳ quan trọng
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// ĐỊNH TUYẾN: Vào trang khách (Home/Index) đầu tiên
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();