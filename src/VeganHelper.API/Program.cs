using VeganHelper.BLL.Contracts.Services;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDal(builder.Configuration);
builder.Services.AddScoped<IStatusService, StatusService>();
var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();
