using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MainEcommerceService.Models.dbMainEcommer;
using System.Text.Json;

public class KafkaConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly string _bootstrapServers = "localhost:9092";

    public KafkaConsumerService(IServiceScopeFactory scopeFactory, ILogger<KafkaConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            await EnsureTopicsExistAsync();
            await ConsumeAsync("seller-events", stoppingToken);
        }, stoppingToken);
    }

    private async Task ConsumeAsync(string topic, CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            GroupId = "product-service-consumer",
            BootstrapServers = _bootstrapServers,
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
            SessionTimeoutMs = 6000,
            HeartbeatIntervalMs = 2000
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        
        // Retry subscription
        var retryCount = 0;
        const int maxRetries = 5;
        
        while (retryCount < maxRetries && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                consumer.Subscribe(topic);
                _logger.LogInformation("Successfully subscribed to {Topic}", topic);
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "Failed to subscribe to {Topic}. Retry {RetryCount}/{MaxRetries}", topic, retryCount, maxRetries);
                
                if (retryCount < maxRetries)
                {
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }

        if (retryCount >= maxRetries)
        {
            _logger.LogError("Failed to subscribe to {Topic} after {MaxRetries} attempts", topic, maxRetries);
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(1000));
                    
                    if (consumeResult != null)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        await ProcessMessageAsync(scope, consumeResult.Message.Value);
                        consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming Kafka message from {Topic}", topic);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in consumer loop");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Kafka consumer service is stopping.");
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessMessageAsync(IServiceScope scope, string messageValue)
    {
        try
        {
            var message = JsonSerializer.Deserialize<SellerProfileVM>(messageValue);

            if (message?.IsDeleted == true)
            {
                var productService = scope.ServiceProvider.GetRequiredService<IProdService>();
                
                // Gọi method để xóa sản phẩm của seller
                await productService.DeleteProductsBySellerId(message.SellerId);
                
                _logger.LogInformation("Successfully deleted products for seller {SellerId}", message.SellerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing seller deleted message: {Message}", messageValue);
            throw; // Re-throw để không commit message khi có lỗi
        }
    }

    private async Task EnsureTopicsExistAsync()
    {
        var config = new AdminClientConfig { BootstrapServers = _bootstrapServers };
        
        using var adminClient = new AdminClientBuilder(config).Build();
        
        var requiredTopics = new[] { "seller-events" };

        try
        {
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
            var existingTopics = metadata.Topics.Select(t => t.Topic).ToHashSet();

            var topicsToCreate = requiredTopics.Where(topic => !existingTopics.Contains(topic)).ToList();

            if (topicsToCreate.Any())
            {
                var topicSpecs = topicsToCreate.Select(topic => new TopicSpecification
                {
                    Name = topic,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }).ToList();

                await adminClient.CreateTopicsAsync(topicSpecs);
                _logger.LogInformation("Created topics: {Topics}", string.Join(", ", topicsToCreate));
            }
        }
        catch (CreateTopicsException ex)
        {
            foreach (var result in ex.Results)
            {
                if (result.Error.Code != ErrorCode.TopicAlreadyExists)
                {
                    _logger.LogError("Failed to create topic {Topic}: {Error}", result.Topic, result.Error.Reason);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring topics exist");
        }
    }
}