using System;
using System.Threading;

namespace Ryujinx.HLE.HOS.Kernel
{
    partial class KScheduler
    {
        private const int RoundRobinTimeQuantumMs = 10;

        private int CurrentCore;

        public bool MultiCoreScheduling { get; set; }

        public HleCoreManager CoreManager { get; private set; }

        private bool KeepPreempting;

        public void StartAutoPreemptionThread()
        {
            Thread PreemptionThread = new Thread(PreemptCurrentThread);

            KeepPreempting = true;

            PreemptionThread.Start();
        }

        public void ContextSwitch()
        {
            lock (CoreContexts)
            {
                if (MultiCoreScheduling)
                {
                    int SelectedCount = 0;

                    for (int Core = 0; Core < KScheduler.CpuCoresCount; Core++)
                    {
                        KCoreContext CoreContext = CoreContexts[Core];

                        if (CoreContext.ContextSwitchNeeded && (CoreContext.CurrentThread?.Context.IsCurrentThread() ?? false))
                        {
                            CoreContext.ContextSwitch();
                        }

                        if (CoreContext.CurrentThread?.Context.IsCurrentThread() ?? false)
                        {
                            SelectedCount++;
                        }
                    }

                    if (SelectedCount == 0)
                    {
                        CoreManager.Reset(Thread.CurrentThread);
                    }
                    else if (SelectedCount == 1)
                    {
                        CoreManager.Set(Thread.CurrentThread);
                    }
                    else
                    {
                        throw new InvalidOperationException("Thread scheduled in more than one core!");
                    }
                }
                else
                {
                    KThread CurrentThread = CoreContexts[CurrentCore].CurrentThread;

                    bool HasThreadExecuting = CurrentThread != null;

                    if (HasThreadExecuting)
                    {
                        //If this is not the thread that is currently executing, we need
                        //to request an interrupt to allow safely starting another thread.
                        if (!CurrentThread.Context.IsCurrentThread())
                        {
                            CurrentThread.Context.RequestInterrupt();

                            return;
                        }

                        CoreManager.Reset(CurrentThread.Context.Work);
                    }

                    //Advance current core and try picking a thread,
                    //keep advancing if it is null.
                    for (int Core = 0; Core < 4; Core++)
                    {
                        CurrentCore = (CurrentCore + 1) % CpuCoresCount;

                        KCoreContext CoreContext = CoreContexts[CurrentCore];

                        CoreContext.UpdateCurrentThread();

                        if (CoreContext.CurrentThread != null)
                        {
                            CoreContext.CurrentThread.ClearExclusive();

                            CoreManager.Set(CoreContext.CurrentThread.Context.Work);

                            CoreContext.CurrentThread.Context.Execute();

                            break;
                        }
                    }

                    //If nothing was running before, then we are on a "external"
                    //HLE thread, we don't need to wait.
                    if (!HasThreadExecuting)
                    {
                        return;
                    }
                }
            }

            CoreManager.Wait(Thread.CurrentThread);
        }

        private void PreemptCurrentThread()
        {
            //Preempts current thread every 10 milliseconds on a round-robin fashion,
            //when multi core scheduling is disabled, to try ensuring that all threads
            //gets a chance to run.
            while (KeepPreempting)
            {
                lock (CoreContexts)
                {
                    KThread CurrentThread = CoreContexts[CurrentCore].CurrentThread;

                    CurrentThread?.Context.RequestInterrupt();
                }

                PreemptThreads();

                Thread.Sleep(RoundRobinTimeQuantumMs);
            }
        }

        public void ExitThread(KThread Thread)
        {
            Thread.Context.StopExecution();

            CoreManager.Exit(Thread.Context.Work);
        }

        public void RemoveThread(KThread Thread)
        {
            CoreManager.RemoveThread(Thread.Context.Work);
        }
    }
}