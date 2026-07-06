using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DistributedCounterDashboard.Data;
using CosmosDistributedCounter;


namespace DistributedCounterDashboard
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddSingleton<DistributedCounterManagementService>(InitializeDistributedCounterManagementServiceAsync(Configuration));
        }


        private static DistributedCounterManagementService InitializeDistributedCounterManagementServiceAsync(IConfiguration configuration)
        {

            string endpoint = configuration["CosmosUri"] ?? string.Empty;
            string key = configuration["CosmosKey"] ?? string.Empty;
            string databaseName = configuration["CosmosDatabase"];
            string containerName = configuration["CosmosContainer"];

            // Default to the local Azure Cosmos DB emulator when nothing is configured, so the site
            // runs with zero setup once the emulator is started (`docker compose up` from the repo
            // root). In Azure, azd sets CosmosUri to the provisioned account.
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                endpoint = "https://localhost:8081";
                if (string.IsNullOrEmpty(key))
                {
                    key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
                }
            }

            DistributedCounterManagementService dcms =  new DistributedCounterManagementService(endpoint,key,databaseName,containerName);

            return dcms;

        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            //app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
