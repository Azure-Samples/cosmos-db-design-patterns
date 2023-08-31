using CosmosDistributedLock.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.Net;
using System.Collections;
using System.Drawing;
using System.Diagnostics.Metrics;


namespace Cosmos_Patterns_GlobalLock
{
    internal class Program
    {
        static DistributedLockService dls;

        static DateTime dtLog ;
        private static object _lock;

        static async Task Main(string[] args)
        {

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true);

            var config = configuration.Build();

            dls = new DistributedLockService(config);
            await dls.InitDatabaseAsync();

            await MainAsync();
        }

        /// <summary>
        /// This function runs two threads that attempt to compete for a lock.  Only one thread can have the lock on an object at a time.
        /// </summary>
        /// <returns></returns>
        static async Task MainAsync()
        {
            Console.WriteLine("Running complex lease example...");
                       

            string lockName = "lock1";

            //in seconds
            int lockDuration = 3;

            Console.WriteLine("Enter the name of the lock:");
            lockName = Console.ReadLine().Trim();

            Console.WriteLine("Enter the lock TTL duration in seconds:");
            try
            {
                lockDuration = int.Parse(Console.ReadLine());
            }
            catch
            {
                Console.WriteLine($"Supplied lock TTL duration is invalid, continuing with default value {lockDuration} seconds");
            }

            dtLog = DateTime.Now;
            _lock=new object(); 

            Console.WriteLine("Warming Up SDK...");
            await dls.Init(lockName);

        
            LockTest lcTestCyan = new LockTest(dls, lockName, lockDuration,"Cyan",new PostMessageCallback(MessageCallback),ConsoleColor.Cyan);
            Thread tCyan = new Thread(new ThreadStart(lcTestCyan.StartThread));


            LockTest lcTestPink = new LockTest(dls, lockName, lockDuration, "Pink", new PostMessageCallback(MessageCallback), ConsoleColor.Magenta); ;
            Thread tPink = new Thread(new ThreadStart(lcTestPink.StartThread));


            LockTest lcTestBluw = new LockTest(dls, lockName, lockDuration, "Blue", new PostMessageCallback(MessageCallback), ConsoleColor.Blue);
            Thread tBlue = new Thread(new ThreadStart(lcTestBluw.StartThread));
                        

            Console.WriteLine("Starting three threads...");
            tCyan.Start();
            tPink.Start();
            tBlue.Start();


            //run for 30 seconds...
            await Task.Delay(30 * 1000);

            //tell all threads to stop
            lcTestCyan.isActive = false;
            lcTestPink.isActive=false;
            lcTestBluw.isActive=false;

            tCyan.Join();
            tPink.Join();
            tBlue.Join();

            //wait for 30 seconds...
            await Task.Delay(2 * 1000);

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Disabling threads...");
            

            Console.WriteLine("Hit enter to re-run");
            var input = Console.ReadLine();
            
        }


       
        public static void MessageCallback(ConsoleMessage msg)
        {
       
            lock (_lock)
            {

                DateTime dtnow = DateTime.Now;
                if (dtnow - dtLog > TimeSpan.FromSeconds(1))
                {
                    dtLog = dtnow;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"[{dtLog}]:");
                }

                Console.ForegroundColor = msg.Color;
                Console.WriteLine($"        {msg.Message}");

            }
        }

    }
}