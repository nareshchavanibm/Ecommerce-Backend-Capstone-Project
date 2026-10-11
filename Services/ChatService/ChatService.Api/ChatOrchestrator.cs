using System.Diagnostics;
using ChatService.Api.Agents;
using ChatService.Api.Foundry;
using ChatService.Api.Governance;
using ChatService.Api.Models;
using ChatService.Api.Options;
using ChatService.Api.Rag;
using Microsoft.Extensions.Options;

namespace ChatService.Api
{
    public class ChatOrchestrator
    {
        private readonly InputGuard _input;
        private readonly OutputGuard _output;
        private readonly AgentRouter _router;
        private readonly AgentRunner _runner;
        private readonly OfflineResponder _offline;
        private readonly IChatModel _model;
        private readonly AuditLog _audit;
        private readonly GovernanceOptions _options;

        public ChatOrchestrator(
            InputGuard input,
            OutputGuard output,
            AgentRouter router,
            AgentRunner runner,
            OfflineResponder offline,
            IChatModel model,
            AuditLog audit,
            IOptions<GovernanceOptions> options)
        {
            _input = input;
            _output = output;
            _router = router;
            _runner = runner;
            _offline = offline;
            _model = model;
            _audit = audit;
            _options = options.Value;
        }

        public async Task<ChatResponse> HandleAsync(
            ChatRequest request,
            string? userId,
            ChatRole role,
            string? bearerToken,
            CancellationToken cancellationToken)
        {
            var clock = Stopwatch.StartNew();
            var conversationId = ResolveConversationId(request.ConversationId);

            var input = _input.Validate(request);

            if (!input.Allowed)
            {
                _audit.Turn(conversationId, userId, role, "none", $"blocked:{input.Reason}", Array.Empty<string>(), 0, 0, clock.ElapsedMilliseconds);
                return Respond(conversationId, "none", input.RefusalMessage!);
            }

            var restriction = _router.Restriction(input.Message, role);

            if (restriction is not null)
            {
                _audit.Turn(conversationId, userId, role, "none", "blocked:role_restricted", Array.Empty<string>(), 0, 0, clock.ElapsedMilliseconds);
                return Respond(conversationId, "none", restriction);
            }

            var agent = AgentCatalog.ByName(_router.Select(input.Message, request.Agent, role));
            var context = new AgentContext { Role = role, UserId = userId, BearerToken = bearerToken };

            var canary = $"CANARY-{Guid.NewGuid():N}";

            AgentResult result;

            if (_model.IsConfigured)
            {
                try
                {
                    result = await _runner.RunAsync(
                        agent, canary, input.History, input.Message, context, cancellationToken);
                }
                catch (ModelUnavailableException)
                {
                    // A missing, invalid or unreachable model should not take the assistant down.
                    context.Citations.Clear();
                    context.Products.Clear();
                    context.Order = null;
                    result = await _offline.RespondAsync(agent, input.Message, context, cancellationToken);
                }
            }
            else
            {
                result = await _offline.RespondAsync(agent, input.Message, context, cancellationToken);
            }

            string outcome;
            string reply;

            if (result.Filtered)
            {
                outcome = "content_filtered";
                reply = "I can't help with that request. I can help with orders, store questions and product suggestions.";
            }
            else if (string.IsNullOrWhiteSpace(result.Reply))
            {
                outcome = "empty";
                reply = role == ChatRole.Admin
                    ? "Sorry, I could not come up with an answer. Try rephrasing, or use the admin pages directly."
                    : "Sorry, I could not come up with an answer. Would you like to speak to a person?";
                context.Escalate = role != ChatRole.Admin;
            }
            else
            {
                var (safe, blocked) = _output.Check(result.Reply, canary);
                reply = safe;
                outcome = blocked ? "output_blocked" : "ok";
            }

            _audit.Turn(
                conversationId, userId, role, agent.Name, outcome, context.ToolsUsed,
                result.PromptTokens, result.CompletionTokens, clock.ElapsedMilliseconds,
                input.Message, reply);

            return new ChatResponse
            {
                ConversationId = conversationId,
                Reply = reply,
                Agent = agent.Name,
                Citations = context.Citations,
                Products = context.Products,
                Order = context.Order,
                Escalate = context.Escalate,
                SupportEmail = context.Escalate ? _options.SupportEmail : null
            };
        }

        private ChatResponse Respond(string conversationId, string agent, string reply) => new()
        {
            ConversationId = conversationId,
            Agent = agent,
            Reply = reply
        };

        private static string ResolveConversationId(string? supplied) =>
            Guid.TryParse(supplied, out var id) ? id.ToString("N") : Guid.NewGuid().ToString("N");
    }
}
