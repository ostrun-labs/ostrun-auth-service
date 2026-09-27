namespace OstrunAuthService.Application.Exceptions;

public sealed class SocialEmailNotVerifiedException(string providerId)
    : Exception($"The {providerId} account has no verified email address.");
