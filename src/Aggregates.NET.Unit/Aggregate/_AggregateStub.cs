﻿using Aggregates.Contracts;
using NServiceBus.ObjectBuilder.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates.Unit.Aggregate
{
    public class _AggregateStub : Aggregate<Guid>
    {
        private _AggregateStub() { }
        public String Value { get; set; }

        public void Create(Guid id, String value)
        {
            Apply<CreatedEvent>(e => { e.Value = value; });
        }
        public void Update(String value)
        {
            Apply<UpdatedEvent>(e => { e.Value = value; });
        }

        private void Handle(CreatedEvent @event)
        {
            this.Value = @event.Value;
        }
        private void Handle(UpdatedEvent @event)
        {
            this.Value = @event.Value;
        }

        public Action<Object> TestRouteFor( Type eventType)
        {
            return RouteFor(eventType);
        }
        protected override Action<object> RouteFor(Type eventType)
        {
            return base.RouteFor(eventType);
        }
    }
}
