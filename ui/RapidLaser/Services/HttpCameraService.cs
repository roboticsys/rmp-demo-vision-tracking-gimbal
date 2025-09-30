namespace RapidLaser.Services;

public class CameraFrameData
{
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
    public int FrameNumber { get; set; }
    public double Timestamp { get; set; }
    public bool BallDetected { get; set; }
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Radius { get; set; }
    public double TargetX { get; set; }
    public double TargetY { get; set; }
}

public interface ICameraService
{
    bool IsConnected { get; }
    bool IsInitialized { get; }
    bool IsGrabbing { get; }
    int ImageWidth { get; }
    int ImageHeight { get; }

    Task<bool> ConnectAsync(string ip, int port);
    Task<bool> ConnectWithFallbackAsync(string ip, int port, ISshService sshService, string sshUser, string sshPassword);
    Task DisconnectAsync();
    Task<bool> InitializeAsync();
    Task<bool> StartGrabbingAsync();
    Task StopGrabbingAsync();
    Task<bool> CheckServerStatusAsync(CancellationToken cancellationToken = default);
    Task<(bool success, CameraFrameData frameData)> TryGrabFrameAsync(CancellationToken cancellationToken = default);
    void Dispose();
}

public class HttpCameraService : ICameraService
{
    /** FIELDS **/
    //private
    private readonly HttpClient _httpClient;
    private string _serverUrl;
    private bool _isInitialized;
    private bool _isGrabbing;
    private bool _isConnected = false;

    //public
    public bool IsInitialized => _isInitialized;
    public bool IsGrabbing => _isGrabbing;
    public int ImageWidth { get; private set; } = 640;
    public int ImageHeight { get; private set; } = 480;
    public bool IsConnected => _isConnected;


    /** CONSTRUCTOR **/
    public HttpCameraService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
        _serverUrl = string.Empty; // Will be set when connecting
    }


    /** METHODS **/
    // Connection methods
    public async Task<bool> ConnectAsync(string ip, int port)
    {
        try
        {
            _serverUrl = $"http://{ip}:{port}";
            Console.WriteLine($"HttpCameraService: Attempting to connect to {_serverUrl}/status");

            // Test connection to camera server
            var response = await _httpClient.GetAsync($"{_serverUrl}/status");
            _isConnected = response.IsSuccessStatusCode;

            Console.WriteLine($"HttpCameraService: Connection test result - StatusCode: {response.StatusCode}, IsSuccess: {response.IsSuccessStatusCode}");
            return _isConnected;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to HTTP camera service at {_serverUrl}: {ex.Message}");
            _isConnected = false;
            return false;
        }
    }

    /// <summary>
    /// Enhanced connection method that attempts to start the camera server via SSH if initial connection fails.
    /// This provides a seamless fallback mechanism for users when the camera server is not running.
    /// </summary>
    /// <param name="ip">IP address of the camera server</param>
    /// <param name="port">Port of the camera server</param>
    /// <param name="sshService">SSH service for remote command execution</param>
    /// <param name="sshUser">SSH username</param>
    /// <param name="sshPassword">SSH password</param>
    /// <returns>True if connection successful, false otherwise</returns>
    public async Task<bool> ConnectWithFallbackAsync(string ip, int port, ISshService sshService, string sshUser, string sshPassword)
    {
        Console.WriteLine($"HttpCameraService: Attempting enhanced connection to {ip}:{port}");

        // First attempt: Try direct connection
        bool initialConnectionSuccess = await ConnectAsync(ip, port);
        if (initialConnectionSuccess)
        {
            Console.WriteLine("HttpCameraService: Direct connection successful");
            return true;
        }

        Console.WriteLine("HttpCameraService: Initial connection failed, attempting to start server via SSH");

        // Validate required parameters for SSH fallback
        if (sshService == null)
        {
            Console.WriteLine("HttpCameraService: SSH service not provided, cannot start server remotely");
            return false;
        }

        if (string.IsNullOrWhiteSpace(sshUser) || string.IsNullOrWhiteSpace(sshPassword))
        {
            Console.WriteLine("HttpCameraService: SSH credentials not provided, cannot start server remotely");
            return false;
        }

        try
        {
            // Attempt to start the camera server via SSH as a background process
            const string startServerCommand = "nohup bash -c 'cd ~/Documents/rsi-laser-demo && bash scripts/server_camera_run.sh' > ~/camera_server.log 2>&1 &";
            Console.WriteLine($"HttpCameraService: Executing SSH background command: {startServerCommand}");

            string sshResult = await sshService.RunSshCommandAsync(startServerCommand, sshUser, sshPassword, ip, waitForOutput: false);
            Console.WriteLine($"HttpCameraService: SSH background command result: {(string.IsNullOrEmpty(sshResult) ? "No output" : sshResult.Substring(0, Math.Min(100, sshResult.Length)))}");

            // Give the server more time to start up (camera server needs time to initialize)
            Console.WriteLine("HttpCameraService: Waiting 10 seconds for background server to start...");
            await Task.Delay(10000); // Longer delay for server startup

            // Second attempt: Try connecting again after starting the server
            Console.WriteLine("HttpCameraService: Retrying connection after background server startup");
            bool retryConnectionSuccess = await ConnectAsync(ip, port);

            if (retryConnectionSuccess)
            {
                Console.WriteLine("HttpCameraService: Connection successful after background server startup");
                return true;
            }
            else
            {
                Console.WriteLine("HttpCameraService: Connection still failed after attempting to start background server");
                Console.WriteLine("HttpCameraService: The server may need more time to start or there may be an issue");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HttpCameraService: Failed to start server via SSH: {ex.Message}");
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        _isConnected = false;
        _isInitialized = false;
        _isGrabbing = false;
        Console.WriteLine("HttpCameraService: Disconnected from camera server");
        return Task.CompletedTask;
    }

    public async Task<bool> InitializeAsync()
    {
        if (!_isConnected)
        {
            Console.WriteLine("HttpCameraService: Cannot initialize, not connected to server");
            return false;
        }

        try
        {
            Console.WriteLine($"HttpCameraService: Attempting to initialize at {_serverUrl}/status");
            var response = await _httpClient.GetAsync($"{_serverUrl}/status");
            _isInitialized = response.IsSuccessStatusCode;
            Console.WriteLine($"HttpCameraService: Initialization result - StatusCode: {response.StatusCode}, IsSuccess: {response.IsSuccessStatusCode}");
            return _isInitialized;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize HTTP camera service at {_serverUrl}: {ex.Message}");
            _isInitialized = false;
            return false;
        }
    }

    public Task<bool> StartGrabbingAsync()
    {
        if (!_isInitialized)
            return Task.FromResult(false);

        _isGrabbing = true;
        return Task.FromResult(true);
    }

    public Task StopGrabbingAsync()
    {
        _isGrabbing = false;
        return Task.CompletedTask;
    }

    public async Task<bool> CheckServerStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/status", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool success, CameraFrameData frameData)> TryGrabFrameAsync(CancellationToken cancellationToken = default)
    {
        var emptyFrame = new CameraFrameData();

        if (!_isGrabbing)
        {
            Console.WriteLine($"HttpCameraService: Not grabbing (_isGrabbing = {_isGrabbing})");
            return (false, emptyFrame);
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/camera/frame", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"HttpCameraService: HTTP request failed with status {response.StatusCode}: {response.ReasonPhrase}");
                return (false, emptyFrame);
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDocument = JsonDocument.Parse(jsonContent);
            var root = jsonDocument.RootElement;

            // Parse image data
            var imageDataString = root.GetProperty("imageData").GetString();

            if (string.IsNullOrEmpty(imageDataString))
            {
                Console.WriteLine($"HttpCameraService: No image data in response");
                return (false, emptyFrame);
            }

            // Convert base64 JPEG to byte array
            var base64Data = imageDataString;
            if (base64Data.StartsWith("data:image/jpeg;base64,"))
            {
                base64Data = base64Data.Substring("data:image/jpeg;base64,".Length);
            }
            var imageBytes = Convert.FromBase64String(base64Data);

            // Parse all frame data from JSON
            var frameData = new CameraFrameData
            {
                ImageData = imageBytes,
                Width = root.TryGetProperty("width", out var widthProp) ? widthProp.GetInt32() : 640,
                Height = root.TryGetProperty("height", out var heightProp) ? heightProp.GetInt32() : 480,
                FrameNumber = root.TryGetProperty("frameNumber", out var frameNumProp) ? frameNumProp.GetInt32() : 0,
                Timestamp = root.TryGetProperty("timestamp", out var timestampProp) ? timestampProp.GetDouble() : 0,
                BallDetected = root.TryGetProperty("ballDetected", out var ballDetectedProp) ? ballDetectedProp.GetBoolean() : false,
                CenterX = root.TryGetProperty("centerX", out var centerXProp) ? centerXProp.GetDouble() : 0,
                CenterY = root.TryGetProperty("centerY", out var centerYProp) ? centerYProp.GetDouble() : 0,
                Radius = root.TryGetProperty("radius", out var radiusProp) ? radiusProp.GetDouble() : 0,
                TargetX = root.TryGetProperty("targetX", out var targetXProp) ? targetXProp.GetDouble() : 0,
                TargetY = root.TryGetProperty("targetY", out var targetYProp) ? targetYProp.GetDouble() : 0
            };

            // Update dimensions
            ImageWidth = frameData.Width;
            ImageHeight = frameData.Height;

            return (true, frameData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to grab frame from HTTP camera service: {ex.Message}");
            return (false, emptyFrame);
        }
    }

    public void Dispose()
    {
        _isGrabbing = false;
        _isInitialized = false;
        _httpClient?.Dispose();
    }
}
