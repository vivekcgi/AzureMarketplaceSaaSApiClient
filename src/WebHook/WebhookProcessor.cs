﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SaaSFulfillmentClient.AzureAD;

namespace SaaSFulfillmentClient.WebHook
{
    public class WebhookProcessor : IWebhookProcessor
    {

        private readonly IFulfillmentClient fulfillmentClient;

        private readonly ILogger<WebhookProcessor> logger;

        private readonly SecuredFulfillmentClientConfiguration options;

        private readonly IWebhookHandler webhookHandler;

        public WebhookProcessor(IOptionsMonitor<SecuredFulfillmentClientConfiguration> options,
            IFulfillmentClient fulfillmentClient,
            IWebhookHandler webhookHandler,
            ILogger<WebhookProcessor> logger) : this(options.CurrentValue,
            fulfillmentClient,
            webhookHandler,
            logger)
        {
        }

        public WebhookProcessor(
            SecuredFulfillmentClientConfiguration options,
            IFulfillmentClient fulfillmentClient,
            IWebhookHandler webhookHandler,
            ILogger<WebhookProcessor> logger)
        {
            this.options = options;

            this.fulfillmentClient = fulfillmentClient;
            this.logger = logger;
            this.webhookHandler = webhookHandler;
        }

        public async Task ProcessWebhookNotificationAsync(WebhookPayload payload,
            CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            // Always query the fulfillment API for the received Operation for security reasons. Webhook endpoint is not authenticated.
            var operationDetails = await this.fulfillmentClient.GetSubscriptionOperationAsync(payload.SubscriptionId,
                payload.OperationId,
                requestId,
                correlationId,
                cancellationToken);

            if (!operationDetails.Success)
            {
                this.logger.LogError(
                    $"Operation query returned {JsonConvert.SerializeObject(operationDetails)} for subscription {payload.SubscriptionId} operation {payload.OperationId}");
                return;
            }

            this.logger.LogInformation(
                $"Received webhook notification with payload, {JsonConvert.SerializeObject(payload)}");

            switch (payload.Action)
            {
                case WebhookAction.Unsubscribe:
                    await this.webhookHandler.UnsubscribedAsync(payload);
                    break;

                case WebhookAction.ChangePlan:
                    await this.webhookHandler.ChangePlanAsync(payload);
                    break;

                case WebhookAction.ChangeQuantity:
                    await this.webhookHandler.ChangeQuantityAsync(payload);
                    break;

                case WebhookAction.Suspend:
                    await this.webhookHandler.SuspendedAsync(payload);
                    break;

                case WebhookAction.Reinstate:
                    await this.webhookHandler.ReinstatedAsync(payload);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
