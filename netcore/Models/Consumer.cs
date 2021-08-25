using Com.Hus.Cdc;
using Confluent.Kafka;
using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace netcore.Models
{
    public class QuestionConsumerService : BackgroundService
    {
        private readonly string topic;
        private readonly IConsumer<string, long> kafkaConsumer;

        public QuestionConsumerService()
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = "localhost:9094",
                GroupId = "hus-dotnet-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest
            };
            this.topic = "outbox.event.question";
            this.kafkaConsumer = new ConsumerBuilder<string, long>(consumerConfig).Build();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            new Thread(() => StartConsumerLoop(stoppingToken)).Start();

            return Task.CompletedTask;
        }

        private void StartConsumerLoop(CancellationToken cancellationToken)
        {
            kafkaConsumer.Subscribe(this.topic);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var cr = this.kafkaConsumer.Consume(cancellationToken);

                    // Handle message...
                    Console.WriteLine($"{cr.Message.Key}: {cr.Message.Value}");

                    SaveMessage(cr.Message.Value);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException e)
                {
                    // Consumer errors should generally be ignored (or logged) unless fatal.
                    Console.WriteLine($"Consume error: {e.Error.Reason}");

                    if (e.Error.IsFatal)
                    {
                        // https://github.com/edenhill/librdkafka/blob/master/INTRODUCTION.md#fatal-consumer-errors
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

        void SaveMessage(long val)
        {
            using (var db = new LiteDatabase(@"MyData.db"))
            {
                var questions = db.GetCollection<Question>("questions");
                // Create your new user instance
                var question = new Question
                {
                    Id = 123,
                    QuestionText = "asf",
                    PubDate = null,
                };
                // Insert new user document (Id will be auto-incremented)
                questions.Insert(question);
            }
        }

        public override void Dispose()
        {
            this.kafkaConsumer.Close(); // Commit offsets and leave the group cleanly.
            this.kafkaConsumer.Dispose();

            base.Dispose();
        }
    }
}
