using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using MicroZen.Api.Definitions;
using MicroZen.Api.Security.APIKeys.Attribute;
using MicroZen.Api.Services;
using MicroZen.Core.Api.Services;
using MicroZen.Api.Context;
using MicroZen.OAuth2;
using static MicroZen.Data.Context.Variables;
using MicroZen.OAuth2.Definitions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy((corsPolicyBuilder) =>
		corsPolicyBuilder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().Build()
	);
});
var policies = new Dictionary<string, AuthorizationPolicy>()
{
	{
		Constants.APIKeyAuthPolicy,
		new AuthorizationPolicyBuilder()
			.AddAuthenticationSchemes([JwtBearerDefaults.AuthenticationScheme])
			.RequireAuthenticatedUser()
			.AddRequirements(new APIKeyRequirement()).Build()
	},
};
builder.Services.AddMicroZenOAuth2(MicroZenProvider.Cognito, policies, grantTypes: OAuth2GrantType.AuthorizationCodeWithPKCE);
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

  var filePath = Path.Combine(System.AppContext.BaseDirectory, "MicroZen.Api.xml");
  c.IncludeXmlComments(filePath);
  c.IncludeGrpcXmlComments(filePath, includeControllerXmlComments: true);
});
builder.Services.AddDbContext<DbContext, MicroZenContext>(options =>
{
	options.UseNpgsql(builder.Configuration.GetConnectionString(MicroZenConnectionString));
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
app.MapGrpcService<ClientApiKeysService>();
app.MapGrpcReflectionService();
app.MapGrpcHealthChecksService();

app.MigrateAndSeedDevDataOnStart();

app.Run();
