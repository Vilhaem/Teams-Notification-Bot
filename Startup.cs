//using Microsoft.Bot.Builder;
//using Microsoft.Bot.Builder.Integration.AspNet.Core;
//using Microsoft.Bot.Schema;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.OpenApi.Models;
using NotificationBot.Bot;
using NotificationBot.SpeechService;
using NotificationBot.ApiKey;
using System.Reflection;
//using System.Collections.Concurrent;

namespace NotificationBot
{
    public class Startup
    {
        public IConfiguration _configuration;
        private readonly GraphLogger _graphLogger;
        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
            _graphLogger = new GraphLogger(typeof(Startup).Assembly.GetName().Name);
        }


        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddOptions();
            services.AddRazorPages();
            services.Configure<BotOptions>(_configuration.GetSection(BotOptions.Bot));
            services.Configure<SpeechServiceOptions>(_configuration.GetSection(SpeechServiceOptions.Speech));
            services.Configure<ApiKeyOption>(_configuration.GetSection(ApiKeyOption.ApiKey));
            services.AddSingleton<IGraphLogger>(this._graphLogger);

            // Message Bot Services
            //services.AddTransient<CloudAdapter>();
            //services.AddTransient<ConcurrentDictionary<string, ConversationReference>>();

            services.AddControllers();
            

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "Call Bot",
                    Description = "An ASP.NET Core Web API for calling users",
                    TermsOfService = new Uri("https://example.com/terms"),
                    Contact = new OpenApiContact
                    {
                        Name = "Example Contact",
                        Url = new Uri("https://example.com/contact")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "Example License",
                        Url = new Uri("https://example.com/license")
                    }
                });
                var xmlFilename = Path.Combine(AppContext.BaseDirectory, "Lab.Swashbuckle.AspNetCore6.xml");
                options.IncludeXmlComments(xmlFilename);
            });

            //services.AddTransient<IBot,MessageBot>();   
            services.AddSingleton<ICallBot, CallBot>();
            //services.AddSingleton<INewClientBot, NewClientBot>();
            services.AddEndpointsApiExplorer();

        }

        public void Configure(WebApplication app)
        {
            //if (app.Environment.IsDevelopment())
            //{
// Allows swagger UI to be used for testing when publish profile is set to Debug
#if DEBUG
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Call Bot v1");
                });
            //}
#endif
            app.UseStaticFiles();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });


            app.MapRazorPages();

            app.Run();
        }
    }
}
