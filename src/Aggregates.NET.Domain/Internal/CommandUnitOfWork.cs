﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aggregates.Extensions;
using Metrics;
using Newtonsoft.Json;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Pipeline;

namespace Aggregates.Internal
{


    internal class CommandUnitOfWork : Behavior<IIncomingLogicalMessageContext>
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CommandUnitOfWork));
        private static readonly object SlowLock = new object();
        private static readonly HashSet<string> SlowCommandTypes = new HashSet<string>();

        private static readonly Meter CommandsMeter = Metric.Meter("Commands", Unit.Commands);
        private static readonly Timer CommandsTimer = Metric.Timer("Command Duration", Unit.Commands);

        private static readonly Meter ErrorsMeter = Metric.Meter("Command Errors", Unit.Errors);
        
        private readonly int _slowAlert;

        public CommandUnitOfWork(int slowAlertThreshold)
        {
            _slowAlert = slowAlertThreshold;
        }

        public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
        {
            if(!(context.Message.Instance is ICommand))
            {
                await next().ConfigureAwait(false);
                return;
            }

            var verbose = false;
            // Todo: break out timing of commands into a different pipeline step I think
            if (SlowCommandTypes.Contains(context.Message.MessageType.FullName))
            {
                lock (SlowLock) SlowCommandTypes.Remove(context.Message.MessageType.FullName);
                Logger.Write(LogLevel.Info, () => $"Command {context.Message.MessageType.FullName} was previously detected as slow, switching to more verbose logging (for this instance)\nPayload: {JsonConvert.SerializeObject(context.Message.Instance, Formatting.Indented).MaxLines(15)}");
                Defaults.MinimumLogging.Value = LogLevel.Info;
                verbose = true;
            }

            var s = new Stopwatch();
            var uows = new ConcurrentStack<ICommandUnitOfWork>();
            try
            {
                CommandsMeter.Mark();
                using (CommandsTimer.NewContext())
                {
                    foreach (var uow in context.Builder.BuildAll<ICommandUnitOfWork>())
                    {
                        uows.Push(uow);
                        uow.Builder = context.Builder;

                        var retries = 0;
                        context.Extensions.TryGet(Defaults.Attempts, out retries);
                        uow.Retries = retries;

                        await uow.Begin().ConfigureAwait(false);
                    }

                    s.Restart();

                    await next().ConfigureAwait(false);

                    s.Stop();
                    if (s.ElapsedMilliseconds > _slowAlert)
                    {
                        Logger.Write(LogLevel.Warn, () => $" - SLOW ALERT - Processing command {context.Message.MessageType.FullName} took {s.ElapsedMilliseconds} ms\nPayload: {JsonConvert.SerializeObject(context.Message.Instance, Formatting.Indented).MaxLines(15)}");
                        if (!verbose)
                            lock (SlowLock) SlowCommandTypes.Add(context.Message.MessageType.FullName);
                    }
                    else
                        Logger.Write(LogLevel.Debug, () => $"Processing command {context.Message.MessageType.FullName} took {s.ElapsedMilliseconds} ms");

                    s.Restart();
                    foreach (var uow in uows.Generate())
                    {
                        try
                        {
                            await uow.End().ConfigureAwait(false);
                        }
                        catch
                        {
                            // If it failed it needs to go back on the stack
                            uows.Push(uow);
                            throw;
                        }
                    }
                    s.Stop();
                    if (s.ElapsedMilliseconds > _slowAlert)
                        Logger.Write(LogLevel.Warn, () => $" - SLOW ALERT - UOW.End for command {context.Message.MessageType.FullName} took {s.ElapsedMilliseconds} ms");
                    else
                        Logger.Write(LogLevel.Debug, () => $"UOW.End for command {context.Message.MessageType.FullName} took {s.ElapsedMilliseconds} ms");

                }


            }
            catch (Exception e)
            {
                Logger.WriteFormat(LogLevel.Warn, "Caught exception '{0}' while executing command {1}", e.GetType().FullName, context.Message.MessageType.FullName);
                ErrorsMeter.Mark();
                var trailingExceptions = new List<Exception>();
                foreach (var uow in uows.Generate())
                {
                    try
                    {
                        await uow.End(e).ConfigureAwait(false);
                    }
                    catch (Exception endException)
                    {
                        trailingExceptions.Add(endException);
                    }
                }


                if (trailingExceptions.Any())
                {
                    trailingExceptions.Insert(0, e);
                    e = new System.AggregateException(trailingExceptions);
                }
                throw;
            }
            finally
            {
                if (verbose)
                {
                    Logger.Write(LogLevel.Info, () => $"Finished processing command {context.Message.MessageType.FullName} verbosely - resetting log level");
                    Defaults.MinimumLogging.Value = null;
                }
            }
        }
    }
}

