using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace RainbowIIS
{
    public class BlankTriggerAddingConvention : Microsoft.EntityFrameworkCore.Metadata.Conventions.IModelFinalizingConvention
    {
        public virtual void ProcessModelFinalizing(
            Microsoft.EntityFrameworkCore.Metadata.Builders.IConventionModelBuilder modelBuilder,
            Microsoft.EntityFrameworkCore.Metadata.Conventions.IConventionContext<Microsoft.EntityFrameworkCore.Metadata.Builders.IConventionModelBuilder> context)
        {
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
            {
                var table = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Create(entityType, Microsoft.EntityFrameworkCore.Metadata.StoreObjectType.Table);
                if (table != null
                    && entityType.GetDeclaredTriggers().All(t => t.GetDatabaseName(table.Value) == null))
                {
                    entityType.Builder.HasTrigger(table.Value.Name + "_Trigger");
                }

                foreach (var fragment in entityType.GetMappingFragments(Microsoft.EntityFrameworkCore.Metadata.StoreObjectType.Table))
                {
                    if (entityType.GetDeclaredTriggers().All(t => t.GetDatabaseName(fragment.StoreObject) == null))
                    {
                        entityType.Builder.HasTrigger(fragment.StoreObject.Name + "_Trigger");
                    }
                }
            }
        }
    }

    public partial class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            OnConfiguring(app, env);
            if (env.IsDevelopment())
            {
                Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.Use((ctx, next) =>
                {
                    return next();
                });
            }
            app.UseStaticFiles();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });

            app.UseCors();
            OnConfigure(app, env);
        }

        public void ConfigureServices(IServiceCollection services)
        {
            OnConfiguringServices(services);

            services.AddScoped<HttpClient>(serviceProvider =>
            {
                var uriHelper = serviceProvider.GetRequiredService<NavigationManager>();

                return new HttpClient
                {
                    BaseAddress = new Uri(uriHelper.BaseUri)
                };
            });

            services.AddHttpClient();

            services.AddRazorPages();
            services.AddServerSideBlazor().AddHubOptions(o =>
            {
                o.MaximumReceiveMessageSize = 10 * 1024 * 1024;
            });

            services.AddHttpContextAccessor();
            services.AddRazorPages();
            services.AddServerSideBlazor();

            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                });
            });

            OnConfigureServices(services);
        }

        partial void OnConfigure(IApplicationBuilder app, IWebHostEnvironment env);

        partial void OnConfigureServices(IServiceCollection services);

        partial void OnConfiguring(IApplicationBuilder app, IWebHostEnvironment env);

        partial void OnConfiguringServices(IServiceCollection services);
    }
}