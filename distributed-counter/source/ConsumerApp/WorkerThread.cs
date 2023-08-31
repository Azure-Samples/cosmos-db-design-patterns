using CosmosDistributedCounter;
using Spectre.Console;

namespace DistributedCounterConsumerApp
{
    // Delegate that defines the signature for the callback method.
    public delegate void PostMessageCallback(string msg);

    internal class WorkerThread
    {
        PrimaryCounter pc;
        DistributedCounterOperationalService dcos;
        public bool isActive = true;
        PostMessageCallback postMessage;
        public WorkerThread(PrimaryCounter _pc, DistributedCounterOperationalService _dcos, PostMessageCallback _postMessage)
        {
            this.pc = _pc;
            this.dcos = _dcos;
            this.postMessage = _postMessage;
        }


        public async void StartThread()
        {            
            while (this.isActive)
            {
                //pick a random number to decrement
                Random r = new Random();
                int decrementVal = r.Next(1, 4);

                try
                {
                    if (await dcos.DecrementDistributedCounterValueAsync(pc, decrementVal) == false)
                    {
                        postMessage($"[yellow bold]Failed[/]\t\t[italic strikethrough]Attemped to decrement by {decrementVal}[/]");
                    }
                    else
                    {
                        postMessage($"[green bold]Success[/]\t\t[italic]Decrement by {decrementVal}[/]");
                    }
                }
                catch (Exception ex)
                {
                    postMessage($"[red bold]Exception[/]\t[italic]{ex.Message}[/]");
                }

                //DO WORK, delay before next execution
                await DoWork();
            }
        }

        private async Task DoWork()
        {
            //wait some random time
            Random r = new Random();
            int delay = r.Next(250, 500);
           
            await Task.Delay(delay);
        }
    }
}
