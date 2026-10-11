using System.Text.RegularExpressions;
using ChatService.Api.Models;
using ChatService.Api.Options;
using Microsoft.Extensions.Options;

namespace ChatService.Api.Governance
{
    public static partial class PiiRedactor
    {
        [GeneratedRegex(@"(?<![\w-])(?:\d[ -]?){13,19}(?![\w-])")]
        private static partial Regex Card();

        [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
        private static partial Regex Email();

        // A 10-digit number with optional separators, so dates like 2026-10-11 are not matched.
        [GeneratedRegex(@"(?<![\w-])(?:\+\d{1,3}[\s.-]?)?(?:\d[\s.-]?){9}\d(?![\w-])")]
        private static partial Regex Phone();

        [GeneratedRegex(@"\b(password|passcode|cvv|cvc|otp|pin)\b\s*(?:is|:|=)?\s*\S+", RegexOptions.IgnoreCase)]
        private static partial Regex Secret();

        // Order numbers (ORD-XXXXXXXX) are intentionally left alone.
        public static string Redact(string text)
        {
            text = Card().Replace(text, "[card number removed]");
            text = Email().Replace(text, "[email removed]");
            text = Phone().Replace(text, "[phone removed]");
            return Secret().Replace(text, "$1 [removed]");
        }
    }

    public record InputResult(bool Allowed, string? Reason, string? RefusalMessage, string Message, List<ChatTurn> History);

    // Validates and sanitises what reaches the model.
    public partial class InputGuard
    {
        [GeneratedRegex(
            @"ignore\s+(all\s+|any\s+|the\s+|your\s+)?(previous|prior|above|earlier)\s+(instructions|rules|prompts?)|" +
            @"disregard\s+(all\s+|any\s+|the\s+|your\s+)?(previous|prior|above|earlier|system)|" +
            @"(reveal|show|print|repeat|output)\s+(me\s+)?(your|the)\s+(system|hidden|initial)\s+(prompt|instructions|message)|" +
            @"you\s+are\s+now\s+(?!looking|interested)|act\s+as\s+(an?\s+)?(unrestricted|jailbroken|dan)|developer\s+mode|jailbreak|do\s+anything\s+now",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex Injection();

        [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F]")]
        private static partial Regex ControlChars();

        private readonly GovernanceOptions _options;

        public InputGuard(IOptions<GovernanceOptions> options)
        {
            _options = options.Value;
        }

        public InputResult Validate(ChatRequest request)
        {
            var message = ControlChars().Replace(request.Message ?? string.Empty, string.Empty).Trim();

            if (message.Length == 0)
            {
                return Refuse("empty", "Please type a question.");
            }

            if (message.Length > _options.MaxMessageLength)
            {
                return Refuse("too_long", $"Please keep messages under {_options.MaxMessageLength} characters.");
            }

            if (Injection().IsMatch(message))
            {
                return Refuse(
                    "prompt_injection",
                    "I can only help with orders, store questions and product suggestions.");
            }

            // Only user/assistant turns are accepted, so a client cannot inject a system or tool message.
            var history = (request.History ?? new List<ChatTurn>())
                .Where(t => t.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(t.Content))
                .TakeLast(_options.MaxHistoryMessages)
                .Select(t => new ChatTurn
                {
                    Role = t.Role,
                    Content = PiiRedactor.Redact(Truncate(ControlChars().Replace(t.Content, string.Empty), _options.MaxMessageLength))
                })
                .ToList();

            return new InputResult(true, null, null, PiiRedactor.Redact(message), history);
        }

        private static InputResult Refuse(string reason, string message) =>
            new(false, reason, message, string.Empty, new List<ChatTurn>());

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }

    public partial class OutputGuard
    {
        [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
        private static partial Regex MarkdownImage();

        public const string Fallback =
            "Sorry, I can't share that. I can help with orders, store questions and product suggestions.";

        // Returns the safe reply and whether the original had to be replaced.
        public (string Reply, bool Blocked) Check(string reply, string canary)
        {
            if (reply.Contains(canary, StringComparison.OrdinalIgnoreCase) ||
                reply.Contains("system prompt", StringComparison.OrdinalIgnoreCase))
            {
                return (Fallback, true);
            }

            // Images in a reply could be used to leak data through a URL.
            reply = MarkdownImage().Replace(reply, string.Empty);

            return (PiiRedactor.Redact(reply).Trim(), false);
        }
    }

    public class AuditLog
    {
        private readonly ILogger<AuditLog> _logger;
        private readonly GovernanceOptions _options;

        public AuditLog(ILogger<AuditLog> logger, IOptions<GovernanceOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        // Metadata only; message text is logged (redacted) just when LogRedactedContent is on.
        public void Turn(
            string conversationId,
            string? userId,
            ChatRole role,
            string agent,
            string outcome,
            IReadOnlyCollection<string> tools,
            int promptTokens,
            int completionTokens,
            long elapsedMs,
            string? redactedUser = null,
            string? redactedReply = null)
        {
            _logger.LogInformation(
                "ChatTurn conversation={ConversationId} user={UserId} role={Role} agent={Agent} outcome={Outcome} tools={Tools} promptTokens={PromptTokens} completionTokens={CompletionTokens} elapsedMs={ElapsedMs}",
                conversationId, userId ?? "anonymous", role, agent, outcome, string.Join(',', tools),
                promptTokens, completionTokens, elapsedMs);

            if (_options.LogRedactedContent && redactedUser is not null)
            {
                _logger.LogInformation(
                    "ChatContent conversation={ConversationId} user={User} reply={Reply}",
                    conversationId, redactedUser, redactedReply);
            }
        }

        public void Feedback(string conversationId, string? userId, string agent, string rating)
        {
            _logger.LogInformation(
                "ChatFeedback conversation={ConversationId} user={UserId} agent={Agent} rating={Rating}",
                conversationId, userId ?? "anonymous", agent, rating);
        }
    }
}
