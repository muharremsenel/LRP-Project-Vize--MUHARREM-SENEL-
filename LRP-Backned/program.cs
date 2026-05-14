using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 🛠️ CORS Ayarı: Frontend isteklerini engellememesi için
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// 💾 SQLite Veritabanı Bağlantısı (LRP.db adında bir dosya oluşturacak)
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseSqlite("Data Source=LRP.db"));

var app = builder.Build();
app.UseCors("AllowAll");

// 🔄 Veritabanını Otomatik Oluşturma ve İlk Admin Hesabını Ekleme (Seed Veri)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated(); // Veritabanı dosyası yoksa otomatik yaratır
    
    if (!db.Users.Any())
    {
        db.Users.Add(new User { Username = "admin", Password = "123", FullName = "Sistem Yöneticisi", Role = "Admin" });
        db.SaveChanges();
    }
}

// ==================== ENDPOINTLER (API) ====================

// 🔐 Giriş Yapma (Auth)
app.MapPost("/api/login", async ([FromBody] LoginDto login, AppDbContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == login.Username && u.Password == login.Password);
    if (user == null) return Results.BadRequest("Hatalı kullanıcı adı veya şifre!");
    return Results.Ok(user);
});

// 📂 Laboratuvarları Listeleme
app.MapGet("/api/labs", async (AppDbContext db) => Results.Ok(await db.Labs.ToListAsync()));

// 📂 Laboratuvar Ekleme
app.MapPost("/api/labs", async ([FromBody] Lab lab, AppDbContext db) =>
{
    db.Labs.Add(lab);
    await db.SaveChangesAsync();
    return Results.Ok(lab);
});

// 💻 Bilgisayarları Listeleme (Laboratuvar bilgisiyle birlikte)
app.MapGet("/api/computers", async (AppDbContext db) => 
    Results.Ok(await db.Computers.ToListAsync()));

// 💻 Bilgisayar Ekleme ve Otomatik Demirbaş Kodu Üretimi
app.MapPost("/api/computers", async ([FromBody] ComputerDto dto, AppDbContext db) =>
{
    var lab = await db.Labs.FindAsync(dto.LabId);
    if (lab == null) return Results.BadRequest("Geçersiz Laboratuvar!");

    // Otomatik Demirbaş Kodu Üretme (Örn: LAB1-PC-01)
    int countInLab = await db.Computers.CountAsync(c => c.LabId == dto.LabId);
    string assetCode = $"{lab.Name.Replace(" ", "").ToUpper()}-PC-{(countInLab + 1):D2}";

    var newComp = new Computer
    {
        AssetCode = assetCode,
        Brand = dto.Brand,
        Processor = dto.Processor,
        Ram = dto.Ram,
        HardwareFeatures = dto.HardwareFeatures,
        LabId = dto.LabId
    };

    db.Computers.Add(newComp);
    await db.SaveChangesAsync();
    return Results.Ok(newComp);
});

// 🤝 Öğrenci Atama ve Otomatik Hesap Oluşturma
app.MapPost("/api/computers/assign", async ([FromBody] AssignDto dto, AppDbContext db) =>
{
    var comp = await db.Computers.FindAsync(dto.ComputerId);
    if (comp == null) return Results.BadRequest("Bilgisayar bulunamadı!");

    // Bilgisayara öğrenci bilgilerini zimmetle
    comp.StudentNo = dto.StudentNo;
    comp.StudentName = dto.StudentName;

    // Öğrenci için otomatik giriş hesabı oluştur (Kullanıcı adı: Öğrenci No, Şifre: 1234)
    var userExists = await db.Users.AnyAsync(u => u.Username == dto.StudentNo);
    if (!userExists)
    {
        db.Users.Add(new User 
        { 
            Username = dto.StudentNo, 
            Password = "1234", 
            FullName = dto.StudentName, 
            Role = "Student" 
        });
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Atama başarılı ve öğrenci hesabı oluşturuldu! Şifre: 1234" });
});

// 🎓 Öğrenci Portalı: Öğrencinin kendi zimmetli bilgisayarını getirme
app.MapGet("/api/student/computer/{studentNo}", async (string studentNo, AppDbContext db) =>
{
    var comp = await db.Computers.FirstOrDefaultAsync(c => c.StudentNo == studentNo);
    if (comp == null) return Results.NotFound("Üzerinize zimmetli bir bilgisayar bulunamadı!");
    return Results.Ok(comp);
});

app.Run();

// ==================== VERİTABANI MODEL VE PARAMETRELERİ ====================

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users { get; set; }
    public DbSet<Lab> Labs { get; set; }
    public DbSet<Computer> Computers { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string FullName { get; set; }
    public string Role { get; set; } // Admin veya Student
}

public class Lab
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Computer
{
    public int Id { get; set; }
    public string AssetCode { get; set; } // Otomatik üretilecek
    public string Brand { get; set; }
    public string Processor { get; set; }
    public string Ram { get; set; }
    public string HardwareFeatures { get; set; } // HDMI, Veyon vb. Özellikler
    public int LabId { get; set; }
    public string? StudentNo { get; set; }
    public string? StudentName { get; set; }
}

// Veri Taşıma Sınıfları (DTOs)
public class LoginDto { public string Username { get; set; } public string Password { get; set; } }
public class ComputerDto { public string Brand { get; set; } public string Processor { get; set; } public string Ram { get; set; } public string HardwareFeatures { get; set; } public int LabId { get; set; } }
public class AssignDto { public int ComputerId { get; set; } public string StudentNo { get; set; } public string StudentName { get; set; } }