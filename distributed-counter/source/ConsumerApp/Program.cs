using Microsoft.Extensions.Configuration;
using CosmosDistributedCounter;
using DistributedCounterConsumerApp;
using Spectre.Console;
using Console = Spectre.Console.AnsiConsole;

namespace Cosmos_Patterns_DistributedCounter
{
    internal class Program
    {
        private static DistributedCounterOperationalService dcos;
        private static PrimaryCounter pc;
        private static object _lock;

        static async Task Main(string[] args)
        {

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true);

            var config = configuration.Build();

            string endpoint = config["CosmosUri"];
            string key = config["CosmosKey"];
            string databaseName = config["CosmosDatabase"];
            string containerName = config["CosmosContainer"];

            dcos = new DistributedCounterOperationalService(endpoint, key, databaseName, containerName);

            await MainAsync();

          
        }


        static async Task MainAsync()
        {
            Console.Write(new Rule($"[underline silver]Starting distributed counter consumer...[/]"){ Justification = Justify.Left });

            string counterId = Console.Prompt(
                new TextPrompt<string>("What is the [teal]counter ID[/]?")
                    .PromptStyle("teal")
                    .ValidationErrorMessage("[maroon]Primary counter couldn't be found. Please try again.[/]")
                    .Validate(id =>
                    {
                        pc = dcos.GetPrimaryCounterAsync(id).Result;
                        return pc is not null;
                    })
            );

            int numWorkerThreads = Console.Prompt(
                new TextPrompt<int>("What are the [teal]number of worker threads required[/]?")
                    .PromptStyle("teal")
            );

            _lock = new object();
            for (int i=0;i<numWorkerThreads;i++)
            {
                WorkerThread wt = new WorkerThread(
                    pc, 
                    dcos, new PostMessageCallback(MessageCallback));
                wt.StartThread();
            }

            Console.Write(new Rule($"[underline silver]{numWorkerThreads}[/] worker threads are running...") { Justification = Justify.Left });
            Console.Ask<string>(String.Empty);
        }

        private static void MessageCallback(string message)
        {
            lock (_lock)
            {
                Console.MarkupLine(message);
            }
        }
    }
}