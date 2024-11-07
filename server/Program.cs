using server.Models;
using server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(x =>
{
    x.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Paypal Integration",
        Version = "v1",
        Description = "API Documentation",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Anonymous",
            Email = "anonymous@email.com"
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    x.IncludeXmlComments(xmlPath);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.Configure<PayPalSettings>(builder.Configuration.GetSection("PayPalSettings"));
builder.Services.AddHttpClient<PayPalService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(x =>
    {
        x.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
    });
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

