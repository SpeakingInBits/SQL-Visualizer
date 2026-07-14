using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SqlVisualizer;
using SqlVisualizer.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient for loading lesson content (markdown + manifest) from wwwroot.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register app services as singletons so state persists across component navigations.
builder.Services.AddSingleton<SqliteConnectionService>();
// Scoped to match HttpClient (in WASM a scope lives for the whole app, so this is
// effectively a singleton — but the DI validator rejects scoped-in-singleton).
builder.Services.AddScoped<ContentService>();
builder.Services.AddSingleton<SampleDatabaseService>();
builder.Services.AddSingleton<SchemaService>();
builder.Services.AddSingleton<QueryExecutorService>();
builder.Services.AddSingleton<SceneBuilderService>();
builder.Services.AddSingleton<ScriptRunnerService>();
builder.Services.AddSingleton<ScriptStoreService>();

await builder.Build().RunAsync();
