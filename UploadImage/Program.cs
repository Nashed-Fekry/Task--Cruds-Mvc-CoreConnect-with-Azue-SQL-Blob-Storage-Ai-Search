using Azure;
using Azure.Search.Documents;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UploadImage.Data;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<UploadImageContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UploadImageContext") ?? throw new InvalidOperationException("Connection string 'UploadImageContext' not found.")));

builder.Services.AddSingleton(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("BlobStorageConnection");
    return new BlobServiceClient(connectionString);
});

string searchServiceEndpoint = "https://indexersearch.search.windows.net";
string searchServiceKey = "jpTIMUr8xqargl4iNR228Elooi3giNNWPaFAkVUIn0AzSeDaXnWa";
string indexName = "azure-sql-index";

Uri endpointUri = new Uri(searchServiceEndpoint);
AzureKeyCredential credential = new AzureKeyCredential(searchServiceKey);
SearchClient searchClient = new SearchClient(endpointUri, indexName, credential);

builder.Services.AddSingleton(searchClient);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Students}/{action=Create}/{id?}");

app.Run();
