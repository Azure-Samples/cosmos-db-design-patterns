using CosmosDistributedLock.Services;

namespace Cosmos_Patterns_GlobalLock
{
    public record ConsoleMessage(
        string Message,
        ConsoleColor Color
    );
    // Delegate that defines the signature for the callback method.
    public delegate void PostMessageCallback(ConsoleMessage msg);

    public class LockTest
    {
        public DistributedLockService dls;

        private readonly string lockName;

        private int lockDuration;
               
        public volatile bool isActive = true;

        private ConsoleColor color;

        string threadName;

        PostMessageCallback postMessage;

        public LockTest(DistributedLockService dls, string lockName, int lockDuration, string threadName, PostMessageCallback postMessage , ConsoleColor color)
        {
            this.dls = dls;
            this.lockName = lockName;
            this.lockDuration = lockDuration;
            this.threadName = threadName;
            this.color = color;
            this.postMessage = postMessage;
        }


        public async void StartThread()
        {
            int prevFenceToken=0;
                       
            
            //postMessage(new ConsoleMessage( $"{mutex.Name}: Says Hello", this.color));

            postMessage(new ConsoleMessage($"{threadName}: Says Hello", this.color));

            while (this.isActive)
            {                
                using(var mutex = await LockManager.CreateLockAsync(dls, lockName, threadName, msg => postMessage(new ConsoleMessage(msg, this.color))))
                {
                    var reqStatus = await mutex.AcquireLeaseAsync(lockDuration, prevFenceToken);
                    var latestFenceToken = reqStatus.fenceToken;
                    var newOwner = reqStatus.currentOwner;

                    postMessage(new ConsoleMessage($"{mutex.Name}: Sees lock [{lockName}] having token {latestFenceToken}, attempting to aquire lease.", this.color));

                    if (latestFenceToken <= prevFenceToken)
                    {
                        new Exception($"[{DateTime.Now}]: {mutex.Name} : Violation: {latestFenceToken} was acquired after {prevFenceToken} was seen");
                    }


                    if (latestFenceToken > 0 && newOwner == mutex.ownerId)
                    {
                        postMessage(new ConsoleMessage($"{mutex.Name}: Attempt to aquire lease on lock [{lockName}] using token {latestFenceToken}  ==> SUCESS", this.color));

                        //DO WORK...
                        await DoWork(mutex.Name, lockName);


                        // With automatic lease renewal the lease is still valid even though
                        // the work took longer than the lease duration.
                        if (!await mutex.HasLeaseAsync(latestFenceToken))
                        {
                            //lock released because of TTL before task completed
                            postMessage(new ConsoleMessage($"{mutex.Name}: Lock [{lockName}] was lost because of TTL of {this.lockDuration} seconds ==> ERROR", this.color));
                        }
                        else
                        {
                            postMessage(new ConsoleMessage($"{mutex.Name}: Completed work while still holding lock [{lockName}] ==> OK", this.color));
                        }
                        // uncomment if  you want to explicitly want to release the lock. The lock will get released when code exists using block 
                        /*
                        else
                        {
                            //release the lease as a good lock consumer should after you are done
                            postMessage(new ConsoleMessage($"{mutex.Name}: Releasing the lock [{lockName}].", this.color));
                            await mutex.ReleaseLeaseeAsync();
                        }*/

                    }
                    else
                    {
                        postMessage(new ConsoleMessage($"{mutex.Name}: Attempt to aquire lease on lock [{lockName}] using token {latestFenceToken}  ==> FAILED", this.color));
                    }

                    postMessage(new ConsoleMessage($"{mutex.Name}: Exiting using block, I will release lock if acquired.", this.color));
                }
                //wait for 1 sec before checking again
                await Task.Delay(1000);
            }
        }

        private async Task DoWork(string threadName, string lockName)
        {
            // Intentionally do work that can take LONGER than the lease duration to show that
            // automatic lease renewal keeps the lock held for the entire time (issue #32).
            Random r = new Random();
            int delay = r.Next(lockDuration * 1000, lockDuration * 2000);
            postMessage(new ConsoleMessage($"{threadName}: Will hold lock [{lockName}] for {delay} milliseconds (lease {lockDuration}s, auto-renewed)", this.color));
            await Task.Delay(delay);
        }
    }

}
