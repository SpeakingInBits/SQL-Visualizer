using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SqlVisualizer;
using SqlVisualizer.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register app services as singletons so state persists across component navigations.
builder.Services.AddSingleton<SqliteConnectionService>();
builder.Services.AddSingleton<SampleDatabaseService>();
builder.Services.AddSingleton<SchemaService>();
builder.Services.AddSingleton<QueryExecutorService>();
builder.Services.AddSingleton<ScriptRunnerService>();
builder.Services.AddSingleton<ScriptStoreService>();

await builder.Build().RunAsync();
