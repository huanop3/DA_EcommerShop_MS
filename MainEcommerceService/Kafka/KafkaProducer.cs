using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace MainEcommerceService.Kafka
{
    public interface IKafkaProducerService
    {
        Task SendMessageAsync<T>(string topic, T message);
        Task SendMessageAsync<T>(string topic, string key, T message);
    }

    public class KafkaProducerService : IKafkaProducerService, IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaProducerService> _logger;

        public KafkaProducerService(IConfiguration configuration, ILogger<KafkaProducerService> logger)
        {
            _logger = logger;

            var config = new ProducerConfig
            {
                BootstrapServers = "localhost:9092", // Replace with your Kafka broker address
            };

            _producer = new ProducerBuilder<string, string>(config).Build();
        }

        public async Task SendMessageAsync<T>(string topic, T message)
        {
            await SendMessageAsync(topic, null, message);
        }

        public async Task SendMessageAsync<T>(string topic, string key, T message)
        {
            try
            {
                var serializedMessage = JsonConvert.SerializeObject(message);

                var kafkaMessage = new Message<string, string>
                {
                    Key = key,
                    Value = serializedMessage,
                    Timestamp = new Timestamp(DateTime.UtcNow)
                };

                var result = await _producer.ProduceAsync(topic, kafkaMessage);

                _logger.LogInformation(
                    "Message sent to Kafka topic {Topic} at partition {Partition} with offset {Offset}",
                    result.Topic, result.Partition.Value, result.Offset.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to Kafka topic {Topic}", topic);
                throw;
            }
        }

        public void Dispose()
        {
            _producer?.Flush(TimeSpan.FromSeconds(10));
            _producer?.Dispose();
        }
    }
}