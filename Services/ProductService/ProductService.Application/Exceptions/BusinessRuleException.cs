namespace ProductService.Application.Exceptions;

// An expected rule violation that the caller can fix; the API answers 400 with the message.
public class BusinessRuleException(string message) : Exception(message);
