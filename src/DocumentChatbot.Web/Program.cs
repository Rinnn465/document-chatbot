using DocumentChatbot.Core.Application.Abstractions;
using DocumentChatbot.Core.Application.Services;
using DocumentChatbot.Infrastructure;
using DocumentChatbot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
