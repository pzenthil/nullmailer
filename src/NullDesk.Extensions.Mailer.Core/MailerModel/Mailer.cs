﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NullDesk.Extensions.Mailer.Core.Fluent;

// ReSharper disable CheckNamespace
namespace NullDesk.Extensions.Mailer.Core
{
    /// <summary>
    ///     Base IMailer implementation.
    /// </summary>
    /// <seealso cref="IMailer" />
    public abstract class Mailer<TSettings> : IMailer<TSettings>, IHistoryMailer where TSettings : class, IMailerSettings
    {
        private readonly AsyncLock _deliverablesLock = new AsyncLock();


        /// <summary>
        ///     Gets the history store.
        /// </summary>
        /// <value>The history store.</value>
        public IHistoryStore HistoryStore { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mailer" /> class.
        /// </summary>
        /// <param name="settings">The mailer settings.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="historyStore">The history store.</param>
        protected Mailer(TSettings settings, ILogger logger = null, IHistoryStore historyStore = null)
        {
            Settings = settings;
            Logger = logger ?? NullLogger.Instance;
            HistoryStore = historyStore ?? NullHistoryStore.Instance;
        }

        /// <summary>
        ///     A collection of all messages tracked by this mailer instance.
        /// </summary>
        /// <value>The messages.</value>
        protected ICollection<DeliveryItem> DeliveryItems => ((IMailer)this).Deliverables;

        /// <summary>
        ///     Settings for the mailer service
        /// </summary>
        /// <value>The settings.</value>
        public TSettings Settings { get; set; }

        /// <summary>
        ///     Optional logger
        /// </summary>
        /// <value>The logger.</value>
        public ILogger Logger { get; }

        /// <summary>
        ///     Use the fluent builder API to add a message to the list of pending messages tracked by the mailer.
        /// </summary>
        /// <param name="messageBuilder">The message builder.</param>
        /// <returns>A collection of delivery item identifiers.</returns>
        public virtual IEnumerable<Guid> CreateMessage(
            Expression<Func<MessageBuilder.BuildSubjectStep, IBuilderStepsCompleted>> messageBuilder)
        {
            var message = messageBuilder.Compile().Invoke(new MessageBuilder().ForSettings(Settings)).Build();
            return AddMessage(message);
        }

        /// <summary>
        ///     Use the fluent builder API to add a message to the list of pending messages tracked by the mailer.
        /// </summary>
        /// <param name="messageBuilder">The message builder.</param>
        /// <returns>A collection of delivery item identifiers.</returns>
        public virtual IEnumerable<Guid> CreateMessage(
            Expression<Func<MessageBuilder.BuildSubjectStep, MailerMessage>> messageBuilder)
        {
            var message = messageBuilder.Compile().Invoke(new MessageBuilder().ForSettings(Settings));
            return AddMessage(message);
        }

        /// <summary>
        ///     Adds a collection of messages to the list of pending messages tracked by the mailer.
        /// </summary>
        /// <param name="messages">The messages to add.</param>
        /// <returns>A collection of delivery item identifiers.</returns>
        public virtual IEnumerable<Guid> AddMessages(IEnumerable<MailerMessage> messages)
        {
            var ids = new List<Guid>();
            foreach (var m in messages)
            {
                ids.AddRange(AddMessage(m));
            }
            return ids;
        }

        /// <summary>
        ///     Adds a message to the list of pending messages tracked by the mailer.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>A collection of delivery item identifiers.</returns>
        public virtual IEnumerable<Guid> AddMessage(MailerMessage message)
        {
            CheckIsDeliverable(message);
            var items = message.Recipients.Select(recipient => new DeliveryItem(message, recipient)).ToList();

            using (_deliverablesLock.LockAsync().Result)
            {
                foreach (var i in items)
                {
                    ((IMailer)this).Deliverables.Add(i);
                }
            }
            return items.Select(i => i.Id);
        }

        /// <summary>
        ///     Send all pending messages tracked by the mailer instance.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns>Task&lt;IEnumerable&lt;MessageDeliveryItem&gt;&gt; for each message sent.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual async Task<IEnumerable<DeliveryItem>> SendAllAsync(
            CancellationToken token = new CancellationToken())
        {
            var sentItems = new List<DeliveryItem>();
            var sendIds = new List<Guid>();
            using (await _deliverablesLock.LockAsync())
            {
                foreach (var message in
                    ((IMailer)this).Deliverables.Where(m => !m.IsSuccess && string.IsNullOrEmpty(m.ExceptionMessage)))
                {
                    sendIds.Add(message.Id);
                }
            }
            foreach (var id in sendIds)
            {
                sentItems.Add(await SendAsync(id, token));
            }
            return sentItems;
        }

        /// <summary>
        ///     Sends one pending delivery item with the specified identifier.
        /// </summary>
        /// <param name="id">The delivery item identifier.</param>
        /// <param name="token">The token.</param>
        /// <returns>Task&lt;IEnumerable&lt;MessageDeliveryItem&gt;&gt;.</returns>
        public virtual async Task<DeliveryItem> SendAsync(Guid id, CancellationToken token = new CancellationToken())
        {
            using (await _deliverablesLock.LockAsync())
            {
                var deliveryItem = ((IMailer)this).Deliverables.FirstOrDefault(d => d.Id == id);
                try
                {
                    deliveryItem = await DeliverMessageAsync(deliveryItem, token);
                    deliveryItem.IsSuccess = true;
                    deliveryItem.DeliveryProvider = GetType().Name;
                }
                catch (Exception ex)
                {
                    Logger.LogError(1, ex, ex.Message);
                    deliveryItem.ExceptionMessage = ex.Message;
                }
                finally
                {

                    await HistoryStore.AddAsync(deliveryItem, token);
                }
                return deliveryItem;
            }
        }

        /// <summary>
        /// ReSends the message from history data.
        /// </summary>
        /// <param name="id">The delivery item identifier to resend.</param>
        /// <param name="token">The token.</param>
        /// <returns>Task&lt;System.Boolean&gt;.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual async Task<bool> ReSend(Guid id, CancellationToken token)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        ICollection<DeliveryItem> IMailer.Deliverables { get; set; } = new Collection<DeliveryItem>();


        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        ///     When overridden in a derived class, uses the mailer's underlying mail delivery service to send the specified
        ///     message .
        /// </summary>
        /// <param name="deliveryItem">The delivery item containing the message you wish to send.</param>
        /// <param name="token">The token.</param>
        /// <returns>Task&lt;DeliveryItem&gt;.</returns>
        protected abstract Task<DeliveryItem> DeliverMessageAsync(DeliveryItem deliveryItem,
            CancellationToken token = new CancellationToken());

        private void CheckIsDeliverable(MailerMessage message)
        {
            if (!message.IsDeliverable)
            {
                var ex =
                    new ArgumentException(
                        "Unable to add one or more messages to the mailer instance, message is not valid for delivery. Make sure all messages have a sender, body, and at least one recipient specified");
                Logger.LogError(1, ex, ex.Message);
                throw ex;
            }
        }
    }
}