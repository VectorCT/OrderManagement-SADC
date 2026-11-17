using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Messaging;
using OrderManagement.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var rabbitMqOptions = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();
builder.Services.AddSingleton(rabbitMqOptions);
builder.Services.AddScoped<OrderCreatedHandler>();
builder.Services.AddHostedService<OrderCreatedConsumer>();

await builder.Build().RunAsync();
