using QuestionProto = Com.Hus.Cdc.Question;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.Hosting;
using Nest;
using System;
using System.Threading;
using System.Threading.Tasks;
using netCoreClient.Models;

namespace netCoreClient.Services
{
    public class QuestionConsumerService : BackgroundService
    {
        private readonly string topic;
        private readonly IConsumer<string, QuestionProto> kafkaConsumer;

        public QuestionConsumerService()
        {
            topic = "outbox.event.question";
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = "host.docker.internal:9092",
                GroupId = "hus-dotnet-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
            };
            kafkaConsumer = new ConsumerBuilder<string, QuestionProto>(consumerConfig)
                .SetValueDeserializer(new MyDeserializer())
                .SetErrorHandler((_, e) => Console.WriteLine($"Consumer Error at SetErrorHandler: {e.Reason}"))
                .Build();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            new Thread(() => StartConsumerLoop(stoppingToken)).Start();
            return Task.CompletedTask;
        }

        private void StartConsumerLoop(CancellationToken cancellationToken)
        {
            kafkaConsumer.Subscribe(topic);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var cr = kafkaConsumer.Consume(cancellationToken);
                    Console.WriteLine($"{cr.Message.Key}: {cr.Message.Value.QuestionText}");
                    SaveMessage(cr.Message.Value);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException e)
                {
                    Console.WriteLine($"Consume error at try/catch: {e.Error.Reason}");

                    if (e.Error.IsFatal)
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Unexpected error: {e}");
                    break;
                }
            }
        }

        void SaveMessage(QuestionProto val)
        {
            var question = new Question
            {
                Id = val.Id,
                QuestionText = val.QuestionText,
                PubDate = val.PubDate?.ToDateTime()
            };
            var settings = new ConnectionSettings().DefaultIndex("question");
            var client = new ElasticClient(settings);
            client.IndexDocument(question);
        }

        public override void Dispose()
        {
            // Commit offsets and leave the group cleanly.
            kafkaConsumer.Close();
            kafkaConsumer.Dispose();

            base.Dispose();
        }
    }
}
