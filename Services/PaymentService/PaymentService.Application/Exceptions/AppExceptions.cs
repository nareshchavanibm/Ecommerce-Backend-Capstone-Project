namespace PaymentService.Application.Exceptions;

// Request is understood but breaks a business rule; surfaces as HTTP 400.
public class BusinessRuleException(string message) : Exception(message);

public class NotFoundException(string message) : Exception(message);

public class ForbiddenException(string message) : Exception(message);

// A dependency (another service) failed; surfaces as HTTP 502.
public class DependencyUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
