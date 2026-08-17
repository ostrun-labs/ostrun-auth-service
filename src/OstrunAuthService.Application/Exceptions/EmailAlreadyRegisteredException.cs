namespace OstrunAuthService.Application.Exceptions;

public sealed class EmailAlreadyRegisteredException(string email)
    : Exception($"An account with email '{email}' already exists.");
