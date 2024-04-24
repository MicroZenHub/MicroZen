using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MicroZen.Core.Api.Services;
using MicroZen.Data.Context;
using MicroZen.OAuth2.Definitions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors();
builder.Services.AddMicroZenOAuth2(MicroZenProvider.Cognito);
builder.Services.AddGrpc().AddJsonTranscoding();
builder.Services.AddGrpcHealthChecks();
builder.Services.AddGrpcReflection();
builder.Services.AddGrpcSwagger();
builder.Services.AddSwaggerGen(c =>
{
  c.SwaggerDoc("v1",
    new OpenApiInfo { Title = "MicroZen API", Version = "v1", Description = "The MicroZen API",  });

  c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
    In = ParameterLocation.Header,
    Description = "Please enter into field the word 'Bearer' following by space and JWT access token",
    Name = "Authorization",
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer"
  });

  c.AddSecurityRequirement(new OpenApiSecurityRequirement
  {
    {
      new OpenApiSecurityScheme
      {
        Reference = new OpenApiReference
        {
          Type = ReferenceType.SecurityScheme,
          Id = "Bearer"
        },
        Scheme = "oauth2",
        Name = "Bearer",
        In = ParameterLocation.Header
      },
      Array.Empty<string>()
    }
  });

  var filePath = Path.Combine(System.AppContext.BaseDirectory, "MicroZen.Core.Api.xml");
  c.IncludeXmlComments(filePath);
  c.IncludeGrpcXmlComments(filePath, includeControllerXmlComments: true);
});
builder.Services.AddDbContext<DbContext, MicroZenContext>(options =>
{
	options.UseNpgsql(builder.Configuration.GetConnectionString("MicroZenContext"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
	app.UseHsts();

app.UseHttpsRedirection();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
  c.SwaggerEndpoint("/swagger/v1/swagger.json", "MicroZen API v1");
});

// Configure the HTTP request pipeline.
app.MapGrpcService<ClientsService>();
app.MapGrpcService<OrganizationsService>();
app.MapGrpcService<OrganizationUsersService>();
app.MapGrpcReflectionService();
app.MapGrpcHealthChecksService();

app.MigrateOnStart();

app.Run();
