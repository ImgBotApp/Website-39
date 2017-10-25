using System;
using System.IO;
using System.Linq;
using Daniel15.Infrastructure;
using Daniel15.SimpleIdentity;
using Daniel15.Web.Services;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using React.AspNet;

namespace Daniel15.Web
{
	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; set; }

		public IServiceProvider ConfigureServices(IServiceCollection services)
		{
			services.AddIdentity<SimpleIdentityUser, SimpleIdentityRole>()
				.AddSimpleIdentity<SimpleIdentityUser>(Configuration.GetSection("Auth"))
				.AddDefaultTokenProviders();

			services.AddSession();
			services.AddMvc();
			services.AddReact();
			services.AddDaniel15(Configuration);
			services.AddDaniel15Config(Configuration);

			// Temporary workaround for https://github.com/aspnet/Routing/issues/391
			services.Replace(ServiceDescriptor.Transient<IApplicationModelProvider, BugfixApplicationModelProvider>());

			// Registered here rather than in AddDaniel15() as it's web-specific
			services.AddSingleton<IHostedService, BackgroundTaskService>();
			services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

			// For https://github.com/reactjs/React.NET/issues/433
			return services.BuildServiceProvider();
		}

		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			loggerFactory.AddDebug();

			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				loggerFactory.AddConsole(LogLevel.Information);
			}
			else
			{
				app.UseStatusCodePagesWithReExecute("/Error/Status{0}");
				app.UseExceptionHandler("/Error");
				loggerFactory.AddConsole(LogLevel.Warning);
			}

			// Handle X-Fowarded-Proto to know Nginx is using HTTPS
			app.UseForwardedHeaders(new ForwardedHeadersOptions
			{
				ForwardedHeaders = ForwardedHeaders.XForwardedProto,
			});

			app.UseReact(config =>
			{
				config
					.AddScript("~/Content/js/socialfeed.jsx")
					.SetReuseJavaScriptEngines(false);
			});

			app.UseStaticFiles();
			app.UseAuthentication();
			app.UseSession();
			// All real routes are defined using attributes.
			app.UseMvc();
		}

		public static void Main(string[] args)
		{
			var host = BuildWebHost(args);

			// Delete UNIX pipe if it exists at startup (eg. previous process crashed before cleaning it up)
			var addressFeature = host.ServerFeatures.Get<IServerAddressesFeature>();
			var url = ServerAddress.FromUrl(addressFeature.Addresses.First());
			if (url.IsUnixPipe && File.Exists(url.UnixPipePath))
			{
				Console.WriteLine("UNIX pipe {0} already existed, deleting it.", url.UnixPipePath);
				File.Delete(url.UnixPipePath);
			}

			host.Run();
		}

		public static IWebHost BuildWebHost(string[] args)
		{
			return WebHost.CreateDefaultBuilder(args)
				.UseStartup<Startup>()
				.ConfigureAppConfiguration((hostContext, config) =>
				{
					config
						.AddJsonFile("config.json", optional: false, reloadOnChange: true)
						.AddJsonFile("config.generated.json", optional: true)
						.AddJsonFile($"config.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true);
				})
				.Build();
		}
	}
}
