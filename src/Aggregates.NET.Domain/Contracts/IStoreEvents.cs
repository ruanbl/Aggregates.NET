﻿using NServiceBus.ObjectBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Contracts
{
    public interface IStoreEvents
    {
        Task<IEventStream> GetStream<T>(String bucket, String stream, Int32? start = null) where T : class, IEntity;

        Task WriteEvents(String bucket, String stream, Int32 expectedVersion, IEnumerable<IWritableEvent> events, IDictionary<String, String> commitHeaders);

        Task AppendEvents(String bucket, String stream, IEnumerable<IWritableEvent> events, IDictionary<String, String> commitHeaders);

        IBuilder Builder { get; set; }
    }
}