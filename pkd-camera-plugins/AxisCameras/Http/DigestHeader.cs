namespace AxisCameras.Http;

public struct DigestHeader
{
    public DigestHeader()
    {
    }

    public string Realm { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string Qop { get; set; } = string.Empty;
    public string Sd { get; set; } = string.Empty;
    
    public string Username { get; set; } = string.Empty;
    
    public string Password { get; set; } = string.Empty;
    
    public string ResponseNonce { get; set; } = string.Empty;

    public int NonceCount { get; set; } = 1;
}