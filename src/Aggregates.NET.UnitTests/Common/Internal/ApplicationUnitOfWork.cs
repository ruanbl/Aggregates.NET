﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aggregates.Contracts;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.ObjectBuilder;
using NServiceBus.Pipeline;
using NUnit.Framework;

namespace Aggregates.NET.UnitTests.Common.Internal
{
    [TestFixture]
    public class ApplicationUnitOfWork
    {
        private class FakeUnitOfWork : IApplicationUnitOfWork
        {
            public IBuilder Builder { get; set; }
            public int Retries { get; set; }
            public ContextBag Bag { get; set; }

            public Task Begin()
            {
                return Task.CompletedTask;
            }

            public Task End(Exception ex = null)
            {
                return Task.CompletedTask;
            }
        }
        
        private Aggregates.Internal.ApplicationUnitOfWork _uow;

        [SetUp]
        public void Setup()
        {
            _uow = new Aggregates.Internal.ApplicationUnitOfWork();
        }

        [Test]
        public async Task no_problem_no_units_of_work()
        {
            var bag = new ContextBag();
            var context = new Moq.Mock<IIncomingLogicalMessageContext>();
            var next = new Moq.Mock<Func<Task>>();
            var builder = new Moq.Mock<IBuilder>();
            context.Setup(x => x.MessageId).Returns("1");
            context.Setup(x => x.Message).Returns(new LogicalMessage(new NServiceBus.Unicast.Messages.MessageMetadata(typeof(object)), new object()));
            context.Setup(x => x.Extensions).Returns(bag);
            context.Setup(x => x.Builder).Returns(builder.Object);
            context.Setup(x => x.Headers).Returns(new Dictionary<string, string>());
            context.Setup(x => x.MessageHeaders)
                .Returns(new Dictionary<string, string> { [Headers.MessageIntent] = MessageIntentEnum.Send.ToString() });

            await _uow.Invoke(context.Object, next.Object);
            next.Verify(x => x(), Moq.Times.Once);
        }
        [Test]
        public async Task no_problem_one_units_of_work()
        {
            var bag = new ContextBag();
            var context = new Moq.Mock<IIncomingLogicalMessageContext>();
            var next = new Moq.Mock<Func<Task>>();
            var builder = new Moq.Mock<IBuilder>();
            var uow = new Moq.Mock<IApplicationUnitOfWork>();
            builder.Setup(x => x.BuildAll<IApplicationUnitOfWork>()).Returns(new IApplicationUnitOfWork[] {uow.Object});
            context.Setup(x => x.MessageId).Returns("1");
            context.Setup(x => x.Message).Returns(new LogicalMessage(new NServiceBus.Unicast.Messages.MessageMetadata(typeof(object)), new object()));
            context.Setup(x => x.Extensions).Returns(bag);
            context.Setup(x => x.Builder).Returns(builder.Object);
            context.Setup(x => x.Headers).Returns(new Dictionary<string, string>());
            context.Setup(x => x.MessageHeaders)
                .Returns(new Dictionary<string, string> { [Headers.MessageIntent] = MessageIntentEnum.Send.ToString() });

            await _uow.Invoke(context.Object, next.Object);
            next.Verify(x => x(), Moq.Times.Once);
            uow.Verify(x => x.Begin(), Moq.Times.Once);
            uow.Verify(x => x.End(null), Moq.Times.Once);
        }
        [Test]
        public Task persistence_uow_bag_stored_on_fail()
        {
            var bag = new ContextBag();
            bag.Set("test", "test");
            var context = new Moq.Mock<IIncomingLogicalMessageContext>();
            var next = new Moq.Mock<Func<Task>>();
            var builder = new Moq.Mock<IBuilder>();
            var uow = new Moq.Mock<IApplicationUnitOfWork>();
            builder.Setup(x => x.BuildAll<IApplicationUnitOfWork>()).Returns(new IApplicationUnitOfWork[] { uow.Object });
            context.Setup(x => x.MessageId).Returns("1");
            context.Setup(x => x.Message).Returns(new LogicalMessage(new NServiceBus.Unicast.Messages.MessageMetadata(typeof(object)), new object()));
            context.Setup(x => x.Extensions).Returns(bag);
            context.Setup(x => x.Builder).Returns(builder.Object);
            context.Setup(x => x.Headers).Returns(new Dictionary<string, string>());
            context.Setup(x => x.MessageHeaders)
                .Returns(new Dictionary<string, string> { [Headers.MessageIntent] = MessageIntentEnum.Send.ToString() });
            uow.Setup(x => x.End(null)).Throws(new Exception("test"));
            uow.Setup(x => x.Bag).Returns(bag);

            Assert.ThrowsAsync<Exception>(() => _uow.Invoke(context.Object, next.Object));

            Dictionary<Type, ContextBag> holder;
            Assert.True(bag.TryGet("contextbags.1", out holder));
            
            next.Verify(x => x(), Moq.Times.Once);
            return Task.CompletedTask;
        }
        [Test]
        public async Task persistence_uow_retreive_bag()
        {
            // Verifies that uows are supplied a bag from the contextbag if it exists

            Dictionary<Type, ContextBag> holder = new Dictionary<Type, ContextBag>();

            var bag = new ContextBag();
            bag.Set("test", "test");
            holder[typeof(FakeUnitOfWork)] = bag;

            bag.Set("contextbags.1", holder);

            var context = new Moq.Mock<IIncomingLogicalMessageContext>();
            var next = new Moq.Mock<Func<Task>>();
            var builder = new Moq.Mock<IBuilder>();
            var uow = new FakeUnitOfWork();
            builder.Setup(x => x.BuildAll<IApplicationUnitOfWork>()).Returns(new IApplicationUnitOfWork[] { uow });
            context.Setup(x => x.MessageId).Returns("1");
            context.Setup(x => x.Message).Returns(new LogicalMessage(new NServiceBus.Unicast.Messages.MessageMetadata(typeof(object)), new object()));
            context.Setup(x => x.Extensions).Returns(bag);
            context.Setup(x => x.Builder).Returns(builder.Object);
            context.Setup(x => x.Headers).Returns(new Dictionary<string, string>());
            context.Setup(x => x.MessageHeaders)
                .Returns(new Dictionary<string, string> { [Headers.MessageIntent] = MessageIntentEnum.Send.ToString() });

            await _uow.Invoke(context.Object, next.Object);

            next.Verify(x => x(), Moq.Times.Once);

            string test;
            Assert.True(uow.Bag.TryGet<string>("test", out test));
            Assert.AreEqual("test", test);
        }
        [Test]
        public async Task no_problem_multiple_units_of_work()
        {
            var bag = new ContextBag();
            var context = new Moq.Mock<IIncomingLogicalMessageContext>();
            var next = new Moq.Mock<Func<Task>>();
            var builder = new Moq.Mock<IBuilder>();
            var uow = new Moq.Mock<IApplicationUnitOfWork>();
            var uow2 = new Moq.Mock<IApplicationUnitOfWork>();
            builder.Setup(x => x.BuildAll<IApplicationUnitOfWork>()).Returns(new IApplicationUnitOfWork[] { uow.Object, uow2.Object });
            context.Setup(x => x.MessageId).Returns("1");
            context.Setup(x => x.Message).Returns(new LogicalMessage(new NServiceBus.Unicast.Messages.MessageMetadata(typeof(object)), new object()));
            context.Setup(x => x.Extensions).Returns(bag);
            context.Setup(x => x.Builder).Returns(builder.Object);
            context.Setup(x => x.Headers).Returns(new Dictionary<string, string>());
            context.Setup(x => x.MessageHeaders)
                .Returns(new Dictionary<string, string> { [Headers.MessageIntent] = MessageIntentEnum.Send.ToString() });

            await _uow.Invoke(context.Object, next.Object);
            next.Verify(x => x(), Moq.Times.Once);
            uow.Verify(x => x.Begin(), Moq.Times.Once);
            uow2.Verify(x => x.Begin(), Moq.Times.Once);
            uow.Verify(x => x.End(null), Moq.Times.Once);
            uow2.Verify(x => x.End(null), Moq.Times.Once);
        }
        [Test]
        public async Task last_unit_of_work_is_last()
        {
            var bag = new ContextBag();
            var context = new Moq.Mock<IIncomingLogicalMessageContext>();
            var next = new Moq.Mock<Func<Task>>();
            var builder = new Moq.Mock<IBuilder>();
            var uow = new Moq.Mock<IApplicationUnitOfWork>();
            var uow2 = new Moq.Mock<ILastApplicationUnitOfWork>();
            builder.Setup(x => x.BuildAll<IApplicationUnitOfWork>()).Returns(new IApplicationUnitOfWork[] { uow.Object, uow2.Object, uow.Object, uow.Object });
            context.Setup(x => x.MessageId).Returns("1");
            context.Setup(x => x.Message).Returns(new LogicalMessage(new NServiceBus.Unicast.Messages.MessageMetadata(typeof(object)), new object()));
            context.Setup(x => x.Extensions).Returns(bag);
            context.Setup(x => x.Builder).Returns(builder.Object);
            context.Setup(x => x.Headers).Returns(new Dictionary<string, string>());
            context.Setup(x => x.MessageHeaders)
                .Returns(new Dictionary<string, string> { [Headers.MessageIntent] = MessageIntentEnum.Send.ToString() });
            var ordering = new List<bool>();
            uow.Setup(x => x.End(null)).Returns(Task.CompletedTask).Callback(() => ordering.Add(false));
            uow2.Setup(x => x.End(null)).Returns(Task.CompletedTask).Callback(() => ordering.Add(true));

            await _uow.Invoke(context.Object, next.Object);
            next.Verify(x => x(), Moq.Times.Once);
            Assert.AreEqual(true, ordering.Last());
        }
        [Test]
        public Task first_end_exception_rest_are_called()
        {
            var bag = new ContextBag();
            var context = new Moq.Mock<IIncomingLogicalMessageContext>();
            var next = new Moq.Mock<Func<Task>>();
            var builder = new Moq.Mock<IBuilder>();
            var uow = new Moq.Mock<IApplicationUnitOfWork>();
            var uow2 = new Moq.Mock<ILastApplicationUnitOfWork>();
            builder.Setup(x => x.BuildAll<IApplicationUnitOfWork>()).Returns(new IApplicationUnitOfWork[] { uow.Object, uow2.Object, uow2.Object, uow2.Object });
            context.Setup(x => x.MessageId).Returns("1");
            context.Setup(x => x.Message).Returns(new LogicalMessage(new NServiceBus.Unicast.Messages.MessageMetadata(typeof(object)), new object()));
            context.Setup(x => x.Extensions).Returns(bag);
            context.Setup(x => x.Builder).Returns(builder.Object);
            context.Setup(x => x.Headers).Returns(new Dictionary<string, string>());
            context.Setup(x => x.MessageHeaders)
                .Returns(new Dictionary<string, string> { [Headers.MessageIntent] = MessageIntentEnum.Send.ToString() });
            uow.Setup(x => x.End(null)).Throws(new Exception());

            Assert.ThrowsAsync<Exception>(() => _uow.Invoke(context.Object, next.Object));
            next.Verify(x => x(), Moq.Times.Once);
            uow2.Verify(x => x.End(Moq.It.IsAny<Exception>()), Moq.Times.Exactly(3));
            return Task.CompletedTask;
        }

        [Test]
        public async Task retries_are_set()
        {
            var bag = new ContextBag();
            bag.Set(Defaults.Retries, 1);
            var context = new Moq.Mock<IIncomingLogicalMessageContext>();
            var next = new Moq.Mock<Func<Task>>();
            var builder = new Moq.Mock<IBuilder>();
            var uow = new FakeUnitOfWork();
            builder.Setup(x => x.BuildAll<IApplicationUnitOfWork>()).Returns(new IApplicationUnitOfWork[] { uow });
            context.Setup(x => x.MessageId).Returns("1");
            context.Setup(x => x.Message).Returns(new LogicalMessage(new NServiceBus.Unicast.Messages.MessageMetadata(typeof(object)), new object()));
            context.Setup(x => x.Extensions).Returns(bag);
            context.Setup(x => x.Builder).Returns(builder.Object);
            context.Setup(x => x.Headers).Returns(new Dictionary<string, string>());
            context.Setup(x => x.MessageHeaders)
                .Returns(new Dictionary<string, string> { [Headers.MessageIntent] = MessageIntentEnum.Send.ToString() });


            await _uow.Invoke(context.Object, next.Object);
            next.Verify(x => x(), Moq.Times.Once);
            Assert.AreEqual(1, uow.Retries);
        }
        [Test]
        public Task all_ends_throw()
        {
            var bag = new ContextBag();
            var context = new Moq.Mock<IIncomingLogicalMessageContext>();
            var next = new Moq.Mock<Func<Task>>();
            var builder = new Moq.Mock<IBuilder>();
            var uow = new Moq.Mock<IApplicationUnitOfWork>();
            var uow2 = new Moq.Mock<ILastApplicationUnitOfWork>();
            builder.Setup(x => x.BuildAll<IApplicationUnitOfWork>()).Returns(new IApplicationUnitOfWork[] { uow.Object, uow2.Object });
            context.Setup(x => x.MessageId).Returns("1");
            context.Setup(x => x.Message).Returns(new LogicalMessage(new NServiceBus.Unicast.Messages.MessageMetadata(typeof(object)), new object()));
            context.Setup(x => x.Extensions).Returns(bag);
            context.Setup(x => x.Builder).Returns(builder.Object);
            context.Setup(x => x.Headers).Returns(new Dictionary<string, string>());
            context.Setup(x => x.MessageHeaders)
                .Returns(new Dictionary<string, string> {[Headers.MessageIntent] = MessageIntentEnum.Send.ToString()});
            uow.Setup(x => x.End(null)).Throws(new Exception());
            uow2.Setup(x => x.End(Moq.It.IsAny<Exception>())).Throws(new Exception());

            Assert.ThrowsAsync<AggregateException>(() => _uow.Invoke(context.Object, next.Object));
            next.Verify(x => x(), Moq.Times.Once);
            uow2.Verify(x => x.End(Moq.It.IsAny<Exception>()), Moq.Times.Once);
            return Task.CompletedTask;
        }
        [Test]
        public Task persistence_save_after_end_exception()
        {
            var bag = new ContextBag();
            var context = new Moq.Mock<IIncomingLogicalMessageContext>();
            var next = new Moq.Mock<Func<Task>>();
            var builder = new Moq.Mock<IBuilder>();
            var uow = new Moq.Mock<IApplicationUnitOfWork>();
            var uow2 = new Moq.Mock<ILastApplicationUnitOfWork>();
            builder.Setup(x => x.BuildAll<IApplicationUnitOfWork>()).Returns(new IApplicationUnitOfWork[] { uow.Object, uow2.Object });
            context.Setup(x => x.MessageId).Returns("1");
            context.Setup(x => x.Message).Returns(new LogicalMessage(new NServiceBus.Unicast.Messages.MessageMetadata(typeof(object)), new object()));
            context.Setup(x => x.Extensions).Returns(bag);
            context.Setup(x => x.Builder).Returns(builder.Object);
            context.Setup(x => x.Headers).Returns(new Dictionary<string, string>());
            context.Setup(x => x.MessageHeaders)
                .Returns(new Dictionary<string, string> { [Headers.MessageIntent] = MessageIntentEnum.Send.ToString() });
            uow.Setup(x => x.End(null)).Throws(new Exception());
            uow2.Setup(x => x.End(Moq.It.IsAny<Exception>())).Throws(new Exception());
            uow2.Setup(x => x.Bag).Returns(bag);

            Assert.ThrowsAsync<AggregateException>(() => _uow.Invoke(context.Object, next.Object));
            next.Verify(x => x(), Moq.Times.Once);

            Dictionary<Type, ContextBag> holder;
            Assert.True(bag.TryGet("contextbags.1", out holder));
            
            return Task.CompletedTask;
        }

        [Test]
        public async Task no_uow_if_not_send()
        {
            var bag = new ContextBag();
            var context = new Moq.Mock<IIncomingLogicalMessageContext>();
            var next = new Moq.Mock<Func<Task>>();
            var builder = new Moq.Mock<IBuilder>();
            var uow = new Moq.Mock<IApplicationUnitOfWork>();
            builder.Setup(x => x.BuildAll<IApplicationUnitOfWork>()).Returns(new IApplicationUnitOfWork[] { uow.Object });
            context.Setup(x => x.MessageId).Returns("1");
            context.Setup(x => x.Message).Returns(new LogicalMessage(new NServiceBus.Unicast.Messages.MessageMetadata(typeof(object)), new object()));
            context.Setup(x => x.Extensions).Returns(bag);
            context.Setup(x => x.Builder).Returns(builder.Object);
            context.Setup(x => x.Headers).Returns(new Dictionary<string, string>());
            context.Setup(x => x.MessageHeaders)
                .Returns(new Dictionary<string, string> { [Headers.MessageIntent] = MessageIntentEnum.Reply.ToString() });

            await _uow.Invoke(context.Object, next.Object);
            next.Verify(x => x(), Moq.Times.Once);
            uow.Verify(x => x.Begin(), Moq.Times.Never);
            uow.Verify(x => x.End(null), Moq.Times.Never);
        }
    }
}
