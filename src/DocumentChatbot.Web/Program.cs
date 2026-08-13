using DocumentChatbot.Data;
using DocumentChatbot.Web.Authorization;
using DocumentChatbot.Web.Hubs;
using DocumentChatbot.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
builder.Services.AddDbContext<DocumentChatbotDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();
builder.Services.AddScoped<IUserAccountService, UserAccountService>();

builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IChatSessionRepository, SqlChatSessionRepository>();
builder.Services.AddScoped<IDocumentRepository, SqlDocumentRepository>();
builder.Services.AddSingleton<ITextExtractor, TextExtractor>();

builder.Services.Configure<RagServiceOptions>(
    builder.Configuration.GetSection(RagServiceOptions.SectionName));
builder.Services.AddHttpClient<IRagService, HttpRagService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<RagServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddHttpClient<IDocumentIngestionService, HttpDocumentIngestionService>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<RagServiceOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "DocumentChatbot.Auth";
        options.ExpireTimeSpan = TimeSpan.FromHours(4);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/chat") ||
                context.Request.Path.StartsWithSegments("/hubs"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/chat") ||
                context.Request.Path.StartsWithSegments("/hubs"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AppPolicies.SubjectLeaderOnly,
        policy => policy.RequireRole(AppRoles.SubjectLeader));
    options.AddPolicy(
        AppPolicies.StudentOnly,
        policy => policy.RequireRole(AppRoles.Student));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
app.MapRazorPages();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

public partial class Program;
