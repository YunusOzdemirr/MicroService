using System;
using EventBus.Base.Abstraction;
using EventBus.Base.SubManagers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace EventBus.Base.Events
{
    public abstract class BaseEventBus:IEventBus
    {
        public readonly IServiceProvider ServiceProvider;

        public readonly IEventBusSubscriptionManager SubManager;
        public EventBusConfig EventBusConfig { get; private set; }

        protected BaseEventBus(EventBusConfig config,IServiceProvider serviceProvider)
        {
            EventBusConfig = config;
            ServiceProvider = serviceProvider;
            SubManager = new InMemoryEventBusSubscriptionManager(ProcessEventName);
        }

        public virtual void Dispose()
        {
            EventBusConfig = null;
            SubManager.Clear();
        }

        public virtual string ProcessEventName(string eventName)
        {
            if (EventBusConfig.DeleteEventPrefix)
                eventName = eventName.TrimStart(EventBusConfig.EventNamePrefix.ToArray());
            if (EventBusConfig.DeleteEventSuffix)
                eventName = eventName.TrimStart(EventBusConfig.EventNameSuffix.ToArray());

            return eventName;
        }

        public virtual string GetSubName(string eventName)
        {
            return $"{EventBusConfig.SubscriberClientAppName}.{ProcessEventName(eventName)}";
        }
        public async Task<bool> ProcessEvent(string eventName,string message)
        {
            eventName = ProcessEventName(eventName);
            var processed = false;
            if (SubManager.HasSubscriptionsForEvent(eventName))
            {
                var subscriptions = SubManager.GetHandlersForEvent(eventName);
                using (var scope = ServiceProvider.CreateScope())
                {
                    foreach (var subscription in subscriptions)
                    {
                        var handler = ServiceProvider.GetService(subscription.HandlerType);
                        if (handler == null) continue;
                        var eventType = SubManager.GetEventTypeByName($"{EventBusConfig.EventNamePrefix}{eventName}{EventBusConfig.EventNameSuffix}");
                        var integrationEvent = JsonConvert.DeserializeObject(message, eventType);


                        var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                        await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });
                    }
                }
                processed = true;
            }
            return processed;
        }

        public abstract void Publish(IntegrationEvent @event);


        public abstract void Subscription<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;

        public abstract void UnSubscription<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
    }
}

