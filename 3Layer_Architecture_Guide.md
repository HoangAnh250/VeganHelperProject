# 🚀 HUỚNG DẪN DỰNG WEB API CHUẨN 3-LAYER ARCHITECTURE VỚI .NET & VISUAL STUDIO

> **Mục đích:** Tài liệu hướng dẫn từng bước (Step-by-Step Guide) để tạo dự án ASP.NET Core Web API chuẩn 3-Layer Architecture, kết nối SQL Server qua EF Core. Các ví dụ code được viết dựa trên database **VeganHelperSystem** thực tế của bạn.

---

## 📐 1. CẤU TRÚC KIẾN TRÚC 3-LAYER (3-TIER ARCHITECTURE)

**⚠️ QUY TẮC BẮT BUỘC: Luồng dữ liệu đi theo chiều `Controller -> Service -> Repository`. Tuyệt đối không gọi tắt từ Controller xuống Repository.**

```text
Solution: VeganHelper
 ├── 1. Presentation Layer (API / Controllers)
 │    ├── Controllers/        --> Lễ tân: Hứng Request, gọi Service xử lý, trả về Response (200, 400)
 │    └── Program.cs          --> Đăng ký DI, DbContext, Swagger, Auth Middleware
 │
 ├── 2. Business Logic / Application Layer (DTOs & Services)
 │    ├── DTOs/               --> Input/Output Data Transfer Objects (Validation Attributes)
 │    └── Services/           --> Đầu bếp: Chứa toàn bộ logic nghiệp vụ (tính toán, phân quyền, xử lý)
 │
 └── 3. Data Access Layer (Models & Repositories)
      ├── Data/               --> AppDbContext (EF Core)
      ├── Models/             --> Database Entities (Code First mapping với SQL Server)
      └── Repositories/       --> Thủ kho: Chỉ thực hiện truy vấn Database (Thêm/Sửa/Xóa/Lấy)
```

---

## 🛠️ 2. QUY TRÌNH DỰNG DỰ ÁN (STEP-BY-STEP)

### BƯỚC 1 & 2: Khởi Tạo Project & Cài Đặt Gói NuGet
*(Thực hiện tạo dự án Web API và cài các gói EF Core, JWT như hướng dẫn trước).*

---

### BƯỚC 3: Thiết Kế Layer 3 - Data Access (Models & DbContext)
*Lưu ý: Vì Database của bạn có 26 bảng, trong file này mình lấy ví dụ luồng 3-Layer xuyên suốt cho bảng `posts` và `users`. Bạn có thể dùng Codex để sinh tự động 24 bảng còn lại nhé.*

#### 1. Models (`Models/User.cs`, `Models/Post.cs`)
```csharp
namespace VeganHelper.API.Models
{
    public class User
    {
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public int RoleId { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Navigation property
        public ICollection<Post> Posts { get; set; } = new List<Post>();
    }

    public class Post
    {
        public long Id { get; set; }
        public long AuthorId { get; set; }
        public string PostType { get; set; } = "recipe"; // article, video, recipe, community
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string Status { get; set; } = "draft";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;

        // Navigation property
        public User? Author { get; set; }
    }
}
```

#### 2. Data Context (`Data/AppDbContext.cs`)
```csharp
using Microsoft.EntityFrameworkCore;
using VeganHelper.API.Models;

namespace VeganHelper.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Post> Posts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Cấu hình bảng Users
            modelBuilder.Entity<User>().ToTable("users");
            modelBuilder.Entity<User>().HasKey(u => u.Id);
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            // Cấu hình bảng Posts
            modelBuilder.Entity<Post>().ToTable("posts");
            modelBuilder.Entity<Post>().HasKey(p => p.Id);
            modelBuilder.Entity<Post>()
                .HasOne(p => p.Author)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.AuthorId);
        }
    }
}
```

---

### BƯỚC 4: Thiết Kế Layer 3 (Tiếp) - Repositories

#### Repository Contract & Implementation (`Repositories/`)
```csharp
using Microsoft.EntityFrameworkCore;
using VeganHelper.API.Data;
using VeganHelper.API.Models;

namespace VeganHelper.API.Repositories
{
    public interface IPostRepository
    {
        Task<IEnumerable<Post>> GetAllActiveAsync();
        Task<Post?> GetByIdAsync(long id);
        Task<Post> CreateAsync(Post post);
        Task<bool> SoftDeleteAsync(long id); // Dựa theo DB của bạn có trường is_deleted
    }

    public class PostRepository : IPostRepository
    {
        private readonly AppDbContext _context;
        public PostRepository(AppDbContext context) => _context = context;

        public async Task<IEnumerable<Post>> GetAllActiveAsync()
        {
            return await _context.Posts
                .Where(p => !p.IsDeleted && p.Status == "published")
                .Include(p => p.Author) // Lấy kèm thông tin tác giả
                .ToListAsync();
        }

        public async Task<Post?> GetByIdAsync(long id)
        {
            return await _context.Posts
                .Include(p => p.Author)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        }

        public async Task<Post> CreateAsync(Post post)
        {
            await _context.Posts.AddAsync(post);
            await _context.SaveChangesAsync();
            return post;
        }

        public async Task<bool> SoftDeleteAsync(long id)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return false;

            post.IsDeleted = true; // Cập nhật cờ is_deleted = 1
            post.Status = "hidden";
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
```

---

### BƯỚC 5: Thiết Kế Layer 2 - Business Logic (DTOs & Services)

#### 1. Data Transfer Objects (`DTOs/`)
```csharp
using System.ComponentModel.DataAnnotations;

namespace VeganHelper.API.DTOs
{
    // DTO Trả về cho Client
    public class PostDto
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string PostType { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty; // Phẳng hóa dữ liệu tác giả
        public DateTime CreatedAt { get; set; }
    }

    // DTO Client gửi lên
    public class CreatePostDto
    {
        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Content { get; set; }

        [Required]
        [RegularExpression("^(article|video|recipe|community)$")] // Ràng buộc y như DB của bạn
        public string PostType { get; set; } = "article";
        
        [Required]
        public long AuthorId { get; set; } 
    }
}
```

#### 2. Services (`Services/`)
```csharp
using VeganHelper.API.DTOs;
using VeganHelper.API.Models;
using VeganHelper.API.Repositories;

namespace VeganHelper.API.Services
{
    public interface IPostService
    {
        Task<IEnumerable<PostDto>> GetAllPostsAsync();
        Task<PostDto> CreatePostAsync(CreatePostDto dto);
        Task<bool> DeletePostAsync(long id);
    }

    public class PostService : IPostService
    {
        private readonly IPostRepository _repository;

        // Tiêm Repository vào Service
        public PostService(IPostRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<PostDto>> GetAllPostsAsync()
        {
            var posts = await _repository.GetAllActiveAsync();
            
            // Map Entity -> DTO
            return posts.Select(p => new PostDto
            {
                Id = p.Id,
                Title = p.Title,
                Content = p.Content,
                PostType = p.PostType,
                AuthorName = p.Author?.Username ?? "Unknown",
                CreatedAt = p.CreatedAt
            });
        }

        public async Task<PostDto> CreatePostAsync(CreatePostDto dto)
        {
            var entity = new Post
            {
                Title = dto.Title,
                Content = dto.Content,
                PostType = dto.PostType,
                AuthorId = dto.AuthorId,
                Status = "draft",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            var created = await _repository.CreateAsync(entity);

            return new PostDto
            {
                Id = created.Id,
                Title = created.Title,
                Content = created.Content,
                PostType = created.PostType,
                CreatedAt = created.CreatedAt
            };
        }

        public async Task<bool> DeletePostAsync(long id)
        {
            return await _repository.SoftDeleteAsync(id);
        }
    }
}
```

---

### BƯỚC 6: Thiết Kế Layer 1 - Controllers (Presentation)

```csharp
using Microsoft.AspNetCore.Mvc;
using VeganHelper.API.DTOs;
using VeganHelper.API.Services;

namespace VeganHelper.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _service;
        
        // Tiêm Service vào Controller
        public PostsController(IPostService service) 
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var posts = await _service.GetAllPostsAsync();
            return Ok(posts);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePostDto dto)
        {
            var createdPost = await _service.CreatePostAsync(dto);
            return Ok(createdPost);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var success = await _service.DeletePostAsync(id);
            if (!success) return NotFound(new { message = $"Không tìm thấy Post ID = {id}" });
            
            return Ok(new { message = "Đã xóa bài viết (Soft Delete)!" });
        }
    }
}
```

---

### BƯỚC 7: Đăng Ký Dependency Injection trong `Program.cs`

```csharp
using VeganHelper.API.Repositories;
using VeganHelper.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký Repository & Service
builder.Services.AddScoped<IPostRepository, PostRepository>();
builder.Services.AddScoped<IPostService, PostService>();

var app = builder.Build();
app.Run();
```

