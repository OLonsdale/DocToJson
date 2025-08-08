using Blazored.LocalStorage;

namespace DocToJson.Server;

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllersWithViews();
        builder.Services.AddRazorPages();
        builder.Services.AddHttpClient();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        
        
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.MapStaticAssets(); // serve static assets from referenced projects (MudBlazor)
        app.UseBlazorFrameworkFiles();

        app.UseRouting();

        app.MapRazorPages();
        app.MapControllers();
        app.MapFallbackToFile("index.html");

        app.Run();
    }
}