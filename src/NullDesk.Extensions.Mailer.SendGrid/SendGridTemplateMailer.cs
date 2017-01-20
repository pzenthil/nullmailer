using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NullDesk.Extensions.Mailer.Core;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NullDesk.Extensions.Mailer.SendGrid
{
    /// <summary>
    /// SendGrid File Template based Email Service.
    /// </summary>
    public class SendGridTemplateMailer : SendGridMailer, ITemplateMailer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SendGridTemplateMailer" /> class.
        /// </summary>
        /// <param name="client">The SendGrid client instance</param>
        /// <param name="settings">The settings.</param>
        /// <remarks>Overload used by unit tests</remarks>
        public SendGridTemplateMailer(
            Client client,
            IOptions<SendGridMailerSettings> settings) :
        base(client, settings)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SendGridTemplateMailer"/> class.
        /// </summary>
        /// <param name="settings"></param>
        public SendGridTemplateMailer(
            IOptions<SendGridMailerSettings> settings) :
        this(new Client(settings.Value.ApiKey), settings)
        { }

        /// <summary>
        /// Send mail using a template file.
        /// </summary>
        /// <remarks>
        /// The template file will be located using the folder and filename from the supplied service settings. 
        /// </remarks>
        /// <param name="template">The template file identifier; should be the filename without extension or file name suffix (specified in settings).</param>
        /// <param name="toEmailAddress">To email address.</param>
        /// <param name="toDisplayName">To display name.</param>
        /// <param name="subject">The subject.</param>
        /// <param name="replacementVariables">The replacement variables. The key should include the delimiters needed to locate text which should be replaced.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>Task&lt;System.Boolean&gt;.</returns>
        public async Task<bool> SendMailAsync(
            string template,
            string toEmailAddress,
            string toDisplayName,
            string subject,
            IDictionary<string, string> replacementVariables,
            CancellationToken token)
        {
            return await SendMailAsync(
                template,
                toEmailAddress,
                toDisplayName,
                subject,
                replacementVariables,
                new List<string>() { },
                token
            );
        }

        /// <summary>
        /// Send mail using a template.
        /// </summary>
        /// <param name="template">The template identifier.</param>
        /// <param name="toEmailAddress">To email address.</param>
        /// <param name="toDisplayName">To display name.</param>
        /// <param name="subject">The subject.</param>
        /// <param name="replacementVariables">The replacement variables to use in the template.</param>
        /// <param name="attachmentFiles">A collection of paths to attachment files to include in the message.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>Task&lt;System.Boolean&gt;.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>It is up to the implementing class to decide how to locate and use the specified template.</remarks>
        public virtual async Task<bool> SendMailAsync(
            string template,
            string toEmailAddress,
            string toDisplayName,
            string subject,
            IDictionary<string, string> replacementVariables,
            IEnumerable<string> attachmentFiles,
            CancellationToken token)
        {
            IDictionary<string, Stream> attachments = null;
            if (attachmentFiles != null)
            {
                attachments = new Dictionary<string, Stream>();
                foreach (var attachmentFile in attachmentFiles)
                {
                    if (!File.Exists(attachmentFile))
                    {
                        throw new ArgumentException($"Unable to find email attachment with file name: {attachmentFile}");
                    }
                    var f = new FileInfo(attachmentFile);
                    attachments.Add(f.Name, f.OpenRead());
                }
            }
            return await SendMailAsync(template, toEmailAddress, toDisplayName, subject, replacementVariables, attachments, token);
        }

        /// <summary>
        /// Send mail using a template.
        /// </summary>
        /// <param name="template">The template identifier.</param>
        /// <param name="toEmailAddress">To email address.</param>
        /// <param name="toDisplayName">To display name.</param>
        /// <param name="subject">The subject.</param>
        /// <param name="replacementVariables">The replacement variables to use in the template.</param>
        /// <param name="attachments">A dictionary of attachments as streams</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>Task&lt;System.Boolean&gt;.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>It is up to the implementing class to decide how to locate and use the specified template.</remarks>
        public virtual async Task<bool> SendMailAsync(
            string template,
            string toEmailAddress,
            string toDisplayName,
            string subject,
            IDictionary<string, string> replacementVariables,
            IDictionary<string, Stream> attachments,
            CancellationToken token)
        {
            var mfrom = new Email(Settings.FromEmailAddress, Settings.FromDisplayName);
            var mto = new Email(toEmailAddress, toDisplayName);

            var mail = new Mail
            {
                From = mfrom,
                Subject = subject,
                TemplateId = "13b8f94f-bcae-4ec6-b752-70d6cb59f932"
            };
            mail.AddPersonalization(new Personalization
            {
                Tos = new List<Email> { mto },
                Substitutions = new Dictionary<string, string>(replacementVariables)
            });

            await AddAttachmentStreamsAsync(mail, attachments, token);

            return await SendMailAsync(mail, token);
        }
    }
}
