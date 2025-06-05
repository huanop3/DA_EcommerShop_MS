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
            // 🔥 FIX: Đổi topic name để match với ProductService
            await ConsumeAsync("seller-request", stoppingToken);
        }, stoppingToken);
    }

    private async Task ConsumeAsync(string topic, CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = "main-ecommerce-seller-request", // 🔥 FIX: Đổi group name
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            SessionTimeoutMs = 6000,
            HeartbeatIntervalMs = 2000
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers,
            Acks = Acks.All
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) => _logger.LogError("Consumer error: {Error}", e.Reason))
            .Build();
        using var producer = new ProducerBuilder<string, string>(producerConfig)
            .SetErrorHandler((_, e) => _logger.LogError("Producer error: {Error}", e.Reason))
            .Build();

        // Retry subscription
        var retryCount = 0;
        const int maxRetries = 5;
        
        while (retryCount < maxRetries && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                consumer.Subscribe(topic);
                _logger.LogInformation("✅ MainEcommerce: Successfully subscribed to {Topic}", topic);
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "⚠️ MainEcommerce: Failed to subscribe to {Topic}. Retry {RetryCount}/{MaxRetries}", topic, retryCount, maxRetries);
                
                if (retryCount < maxRetries)
                {
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }

        if (retryCount >= maxRetries)
        {
            _logger.LogError("❌ MainEcommerce: Failed to subscribe to {Topic} after {MaxRetries} attempts", topic, maxRetries);
            return;
        }

        _logger.LogInformation("🔄 MainEcommerce: Starting seller request listener for topic {Topic}...", topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(1000));
                    
                    if (consumeResult != null)
                    {
                        _logger.LogInformation("📨 MainEcommerce: Received request: Key={Key}, Value={Value}", 
                            consumeResult.Message.Key, consumeResult.Message.Value);

                        var scope = _scopeFactory.CreateScope();
                        try
                        {
                            await ProcessMessageAsync(scope, producer, consumeResult.Message.Value);
                            consumer.Commit(consumeResult);
                            _logger.LogInformation("✅ MainEcommerce: Successfully processed and committed message");
                        }
                        finally
                        {
                            if (scope is IAsyncDisposable asyncDisposable)
                                await asyncDisposable.DisposeAsync();
                            else
                                scope.Dispose();
                        }
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                {
                    _logger.LogWarning("⚠️ MainEcommerce: Topic '{Topic}' not found. Waiting...", topic);
                    await Task.Delay(5000, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "❌ MainEcommerce: Error consuming Kafka message from {Topic}", topic);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ MainEcommerce: Unexpected error in consumer loop");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("🛑 MainEcommerce: Kafka consumer service is stopping.");
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessMessageAsync(IServiceScope scope, IProducer<string, string> producer, string messageValue)
    {
        try
        {
            _logger.LogInformation("🔍 MainEcommerce: Processing message: {Message}", messageValue);
            
            var request = JsonSerializer.Deserialize<SellerRequestMessage>(messageValue);
            
            if (request == null)
            {
                _logger.LogWarning("⚠️ MainEcommerce: Failed to deserialize request message");
                return;
            }

            _logger.LogInformation("📋 MainEcommerce: Parsed request - RequestId={RequestId}, Action={Action}, UserId={UserId}", 
                request.RequestId, request.Action, request.UserId);
            
            if (request.Action == "GET_SELLER_BY_USER_ID")
            {
                var sellerProfileService = scope.ServiceProvider.GetRequiredService<ISellerProfileService>();

                _logger.LogInformation("🔍 MainEcommerce: Getting seller for UserId={UserId}", request.UserId);

                // Lấy thông tin seller
                var sellerResponse = await sellerProfileService.GetSellerProfileByUserId(request.UserId);

                // 🔥 FIX: Tạo response message với đầy đủ thông tin
                var responseMessage = new SellerResponseMessage
                {
                    RequestId = request.RequestId,
                    Success = sellerResponse.Success
                };

                if (sellerResponse.Success && sellerResponse.Data != null)
                {
                    // 🔥 FIX: Map đầy đủ data từ seller response
                    responseMessage.Data = new SellerProfileVM
                    {
                        SellerId = sellerResponse.Data.SellerId,
                        StoreName = sellerResponse.Data.StoreName,
                        UserId = sellerResponse.Data.UserId,
                        // Map thêm các field khác nếu cần
                    };
                    responseMessage.ErrorMessage = null;
                    
                    _logger.LogInformation("✅ MainEcommerce: Found seller - SellerId={SellerId}, StoreName={StoreName}", 
                        sellerResponse.Data.SellerId, sellerResponse.Data.StoreName);
                }
                else
                {
                    responseMessage.Data = null;
                    responseMessage.ErrorMessage = sellerResponse.Message ?? $"Seller not found for UserId: {request.UserId}";
                    
                    _logger.LogWarning("⚠️ MainEcommerce: Seller not found for UserId={UserId}: {Message}", 
                        request.UserId, sellerResponse.Message);
                }

                // Gửi response
                await SendResponseAsync(producer, responseMessage);

                _logger.LogInformation("✅ MainEcommerce: Processed request {RequestId} for user {UserId} with success={Success}", 
                    request.RequestId, request.UserId, responseMessage.Success);
            }
            else
            {
                _logger.LogWarning("⚠️ MainEcommerce: Unknown action: {Action}", request.Action);
                
                var errorResponse = new SellerResponseMessage
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Data = null,
                    ErrorMessage = $"Unknown action: {request.Action}"
                };
                
                await SendResponseAsync(producer, errorResponse);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ MainEcommerce: Error processing message: {Message}", messageValue);
            throw; // Re-throw để không commit message khi có lỗi
        }
    }

    private async Task SendResponseAsync(IProducer<string, string> producer, SellerResponseMessage response)
    {
        try
        {
            var responseJson = JsonSerializer.Serialize(response);
            
            _logger.LogInformation("📤 MainEcommerce: Sending response: RequestId={RequestId}, Success={Success}, Json={Json}", 
                response.RequestId, response.Success, responseJson);

            var message = new Message<string, string>
            {
                Key = response.RequestId,
                Value = responseJson
            };

            var result = await producer.ProduceAsync("seller-response", message);
            
            _logger.LogInformation("✅ MainEcommerce: Response sent successfully - RequestId={RequestId}, Topic={Topic}, Partition={Partition}, Offset={Offset}", 
                response.RequestId, result.Topic, result.Partition.Value, result.Offset.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ MainEcommerce: Failed to send response for request {RequestId}", response.RequestId);
            throw;
        }
    }

    private async Task EnsureTopicsExistAsync()
    {
        var config = new AdminClientConfig { BootstrapServers = _bootstrapServers };
        
        using var adminClient = new AdminClientBuilder(config).Build();
        
        // 🔥 FIX: Sửa tên topics để match với ProductService
        var requiredTopics = new[] { "seller-request", "seller-response" };

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
                _logger.LogInformation("✅ MainEcommerce: Created topics: {Topics}", string.Join(", ", topicsToCreate));
            }
            else
            {
                _logger.LogInformation("✅ MainEcommerce: All required topics already exist: {Topics}", string.Join(", ", requiredTopics));
            }
        }
        catch (CreateTopicsException ex)
        {
            foreach (var result in ex.Results)
            {
                if (result.Error.Code != ErrorCode.TopicAlreadyExists)
                {
                    _logger.LogError("❌ MainEcommerce: Failed to create topic {Topic}: {Error}", result.Topic, result.Error.Reason);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ MainEcommerce: Error ensuring topics exist");
        }
    }
}