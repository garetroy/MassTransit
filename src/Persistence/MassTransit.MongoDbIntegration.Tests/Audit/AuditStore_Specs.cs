﻿// Copyright 2007-2017 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the License for the
// specific language governing permissions and limitations under the License.
namespace MassTransit.MongoDbIntegration.Tests.Audit
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MongoDbIntegration.Audit;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Testing;
    using static MongoDbAuditStoreFixture;


    [TestFixture]
    public class Produces_an_audit_record_for_a_sent_message
    {
        [Test]
        public async Task Audit_document_gets_created()
        {
            Assert.AreEqual("Send", _auditDocument.ContextType);
            Assert.AreEqual(_sent.Context.MessageId.Value.ToString(), _auditDocument.MessageId);
            Assert.AreEqual(_sent.Context.ConversationId.Value.ToString(), _auditDocument.ConversationId);
            Assert.AreEqual(_sent.Context.DestinationAddress.ToString(), _auditDocument.DestinationAddress);
            Assert.AreEqual(typeof(A).FullName, _auditDocument.MessageType);
        }

        [Test]
        public void Message_payload_matches_sent_message()
        {
            Assert.AreEqual(_sent.Context.Message.Data, JsonConvert.DeserializeObject<A>(_auditDocument.Message).Data);
        }

        [Test]
        public void Metadata_deserializes_and_matches()
        {
            Assert.That(
                _sent.Context.Headers.Select(x => (x.Key, x.Value)),
                Is.EquivalentTo(_auditDocument.Headers.Select(x => (x.Key, x.Value)))
            );
        }

        InMemoryTestHarness _harness;
        ISentMessage<A> _sent;
        AuditDocument _auditDocument;

        const string TestData = "test data";

        [OneTimeSetUp]
        public async Task Setup()
        {
            _harness = new InMemoryTestHarness();
            _harness.OnConnectObservers += bus => bus.ConnectSendAuditObservers(AuditStore);
            _harness.Consumer<TestConsumer>();

            await _harness.Start();

            await _harness.InputQueueSendEndpoint.Send(new A {Data = TestData});

            _sent = _harness.Sent.Select<A>().First();
            List<AuditDocument> audit = await GetAuditRecords("Send");
            _auditDocument = audit.Single();
        }

        [OneTimeTearDown]
        public Task Teardown()
        {
            return Task.WhenAll(_harness.Stop(), Cleanup());
        }
    }


    [TestFixture]
    public class Produces_an_audit_record_for_a_consumed_message
    {
        [Test]
        public async Task Audit_document_gets_created()
        {
            Assert.AreEqual("Consume", _auditDocument.ContextType);
            Assert.AreEqual(_consumed.Context.MessageId.Value.ToString(), _auditDocument.MessageId);
            Assert.AreEqual(_consumed.Context.ConversationId.Value.ToString(), _auditDocument.ConversationId);
            Assert.AreEqual(_consumed.Context.ReceiveContext.InputAddress.ToString(), _auditDocument.InputAddress);
            Assert.AreEqual(_consumed.Context.DestinationAddress.ToString(), _auditDocument.DestinationAddress);
            Assert.AreEqual(typeof(A).FullName, _auditDocument.MessageType);
        }

        [Test]
        public void Message_payload_matches_sent_message()
        {
            Assert.AreEqual(_consumed.Context.Message.Data, JsonConvert.DeserializeObject<A>(_auditDocument.Message).Data);
        }

        [Test]
        public void Metadata_deserializes_and_matches()
        {
            Assert.That(
                _consumed.Context.Headers.Select(x => (x.Key, x.Value)),
                Is.EquivalentTo(_auditDocument.Headers.Select(x => (x.Key, x.Value)))
            );
        }

        IReceivedMessage<A> _consumed;
        AuditDocument _auditDocument;
        InMemoryTestHarness _harness;

        [OneTimeSetUp]
        public async Task Send_message_to_test_consumer()
        {
            _harness =  new InMemoryTestHarness();
            _harness.OnConnectObservers += bus => bus.ConnectConsumeAuditObserver(AuditStore);

            ConsumerTestHarness<TestConsumer> consumer = _harness.Consumer<TestConsumer>();

            await _harness.Start();

            await _harness.InputQueueSendEndpoint.Send(new A());

            _consumed = consumer.Consumed.Select<A>().First();
            List<AuditDocument> audit = await GetAuditRecords("Consume");
            _auditDocument = audit.Single();
        }

        [OneTimeTearDown]
        public Task Teardown()
        {
            return Task.WhenAll(_harness.Stop(), Cleanup());
        }
    }


    class TestConsumer : IConsumer<A>
    {
        public Task Consume(ConsumeContext<A> context)
        {
            return Task.CompletedTask;
        }
    }


    class A
    {
        public string Data { get; set; }
    }
}