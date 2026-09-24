namespace OstrunAuthService.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string providedPassword);

    // Does the same work as Verify against a throwaway hash, so a login for an
    // unknown email takes as long as one with a wrong password.
    void SimulateVerify(string providedPassword);
}
