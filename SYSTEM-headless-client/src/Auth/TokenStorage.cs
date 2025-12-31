namespace SYSTEM.HeadlessClient.Auth;

/// <summary>
/// File-based token storage, replacing Unity's PlayerPrefs
/// </summary>
public class TokenStorage
{
    private readonly string _tokenFilePath;

    public TokenStorage(string tokenFilePath)
    {
        _tokenFilePath = tokenFilePath;
    }

    public void SaveToken(string token)
    {
        File.WriteAllText(_tokenFilePath, token);
        Console.WriteLine($"[TokenStorage] Token saved to {_tokenFilePath}");
    }

    public string? LoadToken()
    {
        if (!File.Exists(_tokenFilePath))
            return null;

        var token = File.ReadAllText(_tokenFilePath).Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }

    public void ClearToken()
    {
        if (File.Exists(_tokenFilePath))
            File.Delete(_tokenFilePath);
    }
}
