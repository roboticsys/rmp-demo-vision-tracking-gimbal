﻿namespace RapidLaser.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    /** UI **/
    private Window? _mainWindow;


    /** SERVICES **/
    private readonly ISshService _sshService;
    private readonly IRmpGrpcService _rmpGrpcService;
    private readonly ICameraService _cameraService;


    /** FIELDS **/
    //camera streaming
    private const double CAMERA_TARGET_FPS = 60.0; // Target camera refresh rate - change this to adjust streaming speed

    //polling
    private double _updateIntervalMs = 100;
    private System.Timers.Timer? _updateTimer;
    private readonly Random _random = new();

    //rmp
    [ObservableProperty]
    private MotionControllerStatus? _controllerStatus;

    [ObservableProperty]
    private double? _controllerSampleRate_khz;

    [ObservableProperty]
    private NetworkStatus? _networkStatus;

    [ObservableProperty]
    private RTTaskManagerStatus? _taskManagerStatus;

    [ObservableProperty]
    private ObservableCollection<int> _taskIds = new();

    [ObservableProperty]
    private ObservableCollection<RtTask> _tasks = new();

    //globals
    [ObservableProperty]
    private ObservableCollection<GlobalValueItem> _globalValues = [];
    partial void OnGlobalValuesChanged(ObservableCollection<GlobalValueItem> value)
    {
        OnGlobal_BallXChanged(Global_BallX);
        OnGlobal_BallYChanged(Global_BallY);
        OnGlobal_BallRadiusChanged(Global_BallRadius);
        OnGlobal_IsMotionEnabledChanged(Global_IsMotionEnabled);
        OnGlobal_CameraFpsChanged(Global_CameraFps);
    }

    //global names
    [ObservableProperty]
    private string? _global_BallX;
    partial void OnGlobal_BallXChanged(string? value)
    {
        Global_BallXValid = GlobalValues.Any(g => string.Equals(g.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    [ObservableProperty]
    private bool _global_BallXValid;

    [ObservableProperty]
    private string? _global_BallY;
    partial void OnGlobal_BallYChanged(string? value)
    {
        Global_BallYValid = GlobalValues.Any(g => string.Equals(g.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    [ObservableProperty]
    private bool _global_BallYValid;

    [ObservableProperty]
    private string? _global_BallRadius;
    partial void OnGlobal_BallRadiusChanged(string? value)
    {
        Global_BallRadiusValid = GlobalValues.Any(g => string.Equals(g.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    [ObservableProperty]
    private bool _global_BallRadiusValid;

    [ObservableProperty]
    private string? _global_IsMotionEnabled;
    partial void OnGlobal_IsMotionEnabledChanged(string? value)
    {
        Global_IsMotionEnabledValid = GlobalValues.Any(g => string.Equals(g.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    [ObservableProperty]
    private bool _global_IsMotionEnabledValid;

    [ObservableProperty]
    private string? _global_CameraFps;
    partial void OnGlobal_CameraFpsChanged(string? value)
    {
        Global_IsCameraFpsValid = GlobalValues.Any(g => string.Equals(g.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    [ObservableProperty]
    private bool _global_IsCameraFpsValid;

    //program mappings (these hold global values)
    [ObservableProperty]
    private double? _program_BallX = 320.0; // Default center position

    [ObservableProperty]
    private double? _program_BallY = 240.0; // Default center position

    [ObservableProperty]
    private double? _program_BallRadius = 10.0; // Default radius

    [ObservableProperty]
    private bool? _program_IsMotionEnabled;

    [ObservableProperty]
    private double? _program_CameraFps;

    [ObservableProperty]
    private double _detectionConfidence = 95.0;

    [ObservableProperty]
    private bool _isCanvasResponsive = true; // Default to responsive mode

    //program
    [ObservableProperty]
    private bool _isProgramPaused = false;

    [ObservableProperty]
    private bool _isProgramRunning = false;

    //camera frame data
    [ObservableProperty]
    private WriteableBitmap? _cameraImage;

    [ObservableProperty]
    private double _cameraCenterX;

    [ObservableProperty]
    private double _cameraCenterY;

    [ObservableProperty]
    private double _cameraRadius;

    [ObservableProperty]
    private int _cameraFrameNumber = 0;

    [ObservableProperty]
    private double _cameraTimestamp = 0;

    [ObservableProperty]
    private bool _cameraBallDetected = false;

    [ObservableProperty]
    private double _cameraTargetX = 0;

    [ObservableProperty]
    private double _cameraTargetY = 0;

    //camera 
    [ObservableProperty]
    private int _cameraPort = 50080;

    [ObservableProperty]
    private bool _isCameraConnected = false;

    [ObservableProperty]
    private bool _isCameraConnecting = false;

    [ObservableProperty]
    private double _frameRate = CAMERA_TARGET_FPS;

    [ObservableProperty]
    private int _binaryThreshold = 128;

    [ObservableProperty]
    private bool _isCameraStreaming = false;
    // partial void OnIsCameraStreamingChanged(bool value)
    // {
    //     if (value is false)
    //     {
    //         try
    //         {
    //             _ = Task.Run(async () => await DisconnectCameraAsync());
    //         }
    //         catch (Exception ex)
    //         {
    //             LogMessage($"Camera Streaming Disconnect Error: {ex.Message}");
    //         }
    //     }
    // }

    private CancellationTokenSource? _cameraStreamCancellation;

    [ObservableProperty]
    private string _cameraLastError = string.Empty;

    //server
    [ObservableProperty]
    private string _ipAddress = string.Empty;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private bool _isConnected = false;
    partial void OnIsConnectedChanged(bool value)
    {
        if (value)
        {
            // start polling
            _updateTimer = new System.Timers.Timer(_updateIntervalMs);
            _updateTimer.Elapsed += OnUpdateTimerElapsed;
            _updateTimer.Start();
        }
        else
        {
            IsProgramRunning = false;

            // stop polling
            _updateTimer?.Stop();
            _updateTimer = null;
        }
    }

    [ObservableProperty]
    private bool _isConnecting = false;

    [ObservableProperty]
    private string _lastError = string.Empty;

    //mocks
    [ObservableProperty]
    private bool _isSimulatingBallPosition = false;

    //ssh
    [ObservableProperty]
    private string _sshUser = "";
    partial void OnSshUserChanged(string value)
    {
        IsSshAuthenticated = false;
    }

    [ObservableProperty]
    private string _sshPassword = "";
    partial void OnSshPasswordChanged(string value)
    {
        IsSshAuthenticated = false;
    }

    [ObservableProperty]
    private string _sshCommand = "whoami";

    [ObservableProperty]
    private string _sshRunCommand = "";

    [ObservableProperty]
    private bool _isSshCommandRunning = false;

    [ObservableProperty]
    private bool _isSshAuthenticated = false;

    //logging
    [ObservableProperty]
    private string _logOutput = string.Empty;


    /** COMMANDS **/
    //program
    [RelayCommand]
    private async Task ProgramRunAsync()
    {
        try
        {
            try
            {
                var sshResult = await ExecuteSshCommandAsync(SshRunCommand, updateSshOutput: true);

                if (sshResult == null)
                {
                    LogMessage("Program run failed: SSH command returned null");
                }
                else
                {
                    LogMessage(sshResult);

                    // start camera streaming if not already started
                    if (!IsCameraStreaming)
                    {
                        _ = StartCameraStreamAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Program Run Error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Program Run Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ProgramStopAsync()
    {
        try
        {
            if (_rmpGrpcService != null)
            {
                // stop camera streaming
                await _cameraService.StopGrabbingAsync();
                IsCameraStreaming = false;
                CameraImage = null;

                // shut down task manager
                LogMessage("Stopping task manager...");
                var result = await _rmpGrpcService.StopTaskManagerAsync();

                if (result)
                {
                    //reset global variables
                    await Dispatcher.UIThread.InvokeAsync(GlobalValues.Clear);
                    LogMessage("Task manager stopped successfully");
                }
            }
            else
            {
                LogMessage("Program stop failed: RMP service not available");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Program Stop Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleMotionPause()
    {
        // Example: Toggle a global value for motion pause
        IsProgramPaused = !IsProgramPaused;
    }

    [RelayCommand]
    private async Task ToggleProgramMotionEnabledAsync(object isMotionEnabled)
    {
        try
        {
            if (string.IsNullOrEmpty(Global_IsMotionEnabled) || _rmpGrpcService == null)
                return;

            var response = await _rmpGrpcService.SetBoolGlobalValueAsync(Global_IsMotionEnabled, (bool)isMotionEnabled);
            LogMessage($"Motion enabled set to: {(bool)isMotionEnabled}");
        }
        catch (Exception ex)
        {
            LogMessage($"Motion Control Error: {ex.Message}");
        }

    }

    //camera
    [RelayCommand]
    private async Task ConnectCameraAsync()
    {
        // verify ip address is valid 
        if (!IsValidIpAddress(IpAddress))
        {
            CameraLastError = "Invalid IP address. Connect to RapidServer first.";
            return;
        }

        // verify camera port is valid
        if (CameraPort <= 0)
        {
            CameraLastError = "Invalid camera port.";
            return;
        }

        if (IsCameraConnecting) return;

        try
        {
            IsCameraConnecting = true;
            CameraLastError = string.Empty;

            LogMessage($"Connecting to camera at {IpAddress}:{CameraPort}...");

            // Use enhanced connection method with SSH fallback
            bool success = await _cameraService.ConnectWithFallbackAsync(
                IpAddress,
                CameraPort,
                _sshService,
                SshUser,
                SshPassword);

            if (success)
            {
                IsCameraConnected = true;
                LogMessage($"Connected to camera server at {IpAddress}:{CameraPort}");

                // try to start camera stream
                if (!IsCameraStreaming)
                {
                    await StartCameraStreamAsync();
                }
                CameraLastError = string.Empty;
                LogMessage("Camera connected successfully");
            }
            else
            {
                IsCameraConnected = false;
                CameraLastError = "Failed to connect to camera server.";
                LogMessage($"Failed to connect to camera server at {IpAddress}:{CameraPort}");
            }
        }
        catch (Exception ex)
        {
            IsCameraConnected = false;
            CameraLastError = ex.Message;
            LogMessage($"Camera connection error: {ex.Message}");
        }
        finally
        {
            IsCameraConnecting = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectCameraAsync()
    {
        try
        {
            if (IsCameraStreaming)
            {
                await StopCameraStreamAsync();
            }

            await _cameraService.DisconnectAsync();
            IsCameraConnected = false;
            LogMessage("Disconnected from camera server");
        }
        catch (Exception ex)
        {
            CameraLastError = ex.Message;
            LogMessage($"Camera disconnect error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ToggleCameraStreamAsync()
    {
        if (IsCameraStreaming)
        {
            await StopCameraStreamAsync();
        }
        else
        {
            await StartCameraStreamAsync();
        }
    }

    [RelayCommand]
    private async Task StartCameraStreamAsync()
    {
        if (IsCameraStreaming) return;

        try
        {
            if (!_cameraService.IsConnected)
            {
                LogMessage("Camera: Not connected. Please connect to camera first.");
                return;
            }

            // Initialize camera service
            if (!_cameraService.IsInitialized)
            {
                var initialized = await _cameraService.InitializeAsync();
                if (!initialized)
                {
                    LogMessage("Camera: Failed to initialize HTTP camera service");
                    return;
                }
            }

            // Start grabbing frames
            var started = await _cameraService.StartGrabbingAsync();
            if (!started)
            {
                LogMessage("Camera: Failed to start grabbing frames");
                return;
            }

            IsCameraStreaming = true;
            _cameraStreamCancellation = new CancellationTokenSource();

            LogMessage("Camera: Started streaming");

            // Start streaming frames
            _ = Task.Run(async () => await StreamCameraFramesAsync(_cameraStreamCancellation.Token));
        }
        catch (Exception ex)
        {
            LogMessage($"Camera Start Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StopCameraStreamAsync()
    {
        if (!IsCameraStreaming) return;

        try
        {
            _cameraStreamCancellation?.Cancel();

            await _cameraService.StopGrabbingAsync();

            IsCameraStreaming = false;
            CameraImage = null;

            LogMessage("Camera: Stopped streaming");
        }
        catch (Exception ex)
        {
            LogMessage($"Camera Stop Error: {ex.Message}");
        }
    }

    //connection
    [RelayCommand]
    private async Task ConnectAsync()
    {
        // verify ip address is valid 
        if (!IsValidIpAddress(IpAddress))
        {
            LastError = "Invalid IP address.";
            return;
        }

        // verify port is valid
        if (Port <= 0)
        {
            LastError = "Invalid port.";
            return;
        }

        if (IsConnecting) return;

        try
        {
            IsConnecting = true;
            LastError = string.Empty;

            var success = await _rmpGrpcService.ConnectAsync(IpAddress, Port);

            if (success)
            {
                // try to connect to camera server
                if (!IsCameraConnected)
                {
                    await ConnectCameraAsync();
                }

                // Test SSH authentication after successful server connection
                await TestSshAuthenticationAsync();
            }
            else
            {
                LastError = $"Failed to connect to rapidserver {IpAddress}:{Port}";
                LogMessage($"Connection failed: {LastError}");
                IsSshAuthenticated = false;
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            LogMessage($"Connection Error: {ex.Message}");
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private static bool IsValidIpAddress(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;

        // Allow localhost
        if (string.Equals(ipAddress, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        // Allow standard IP address formats
        return System.Net.IPAddress.TryParse(ipAddress, out _);
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            // Stop camera streaming if active
            if (IsCameraStreaming)
            {
                await StopCameraStreamAsync();
            }

            // Disconnect camera if connected
            if (IsCameraConnected)
            {
                await DisconnectCameraAsync();
            }

            await _rmpGrpcService.DisconnectAsync();
            IsConnected = false;
            IsSshAuthenticated = false;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            LogMessage($"Disconnect Error: {ex.Message}");
        }
    }

    //ssh
    [RelayCommand]
    private void ClearLogOutput()
    {
        LogOutput = string.Empty;
    }

    [RelayCommand]
    private async Task TestSshConnectionAsync()
    {
        if (!IsConnected)
        {
            LogOutput += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SSH: Not connected to server\n";
            return;
        }

        await TestSshAuthenticationAsync();

        if (IsSshAuthenticated)
        {
            LogOutput += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SSH: Authentication successful\n";
        }
        else
        {
            LogOutput += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SSH: Authentication failed\n";
        }
    }

    //window
    [RelayCommand]
    private void MinimizeWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.WindowState = WindowState.Minimized;
        }
    }

    [RelayCommand]
    private void MaximizeWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.WindowState = _mainWindow.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }

    [RelayCommand]
    private void CloseWindow()
    {
        _mainWindow?.Close();
    }

    //tasks
    [RelayCommand]
    private void Tasks_TimingReset()
    {
        try
        {
            if (_rmpGrpcService != null && TaskIds.Count > 0)
            {
                _ = _rmpGrpcService.Tasks_TimingResetAsync(TaskIds);
                LogMessage("Task timing metrics reset");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Reset Timing Error: {ex.Message}");
        }
    }


    /** CONSTRUCTOR **/
    public MainViewModel(ISshService sshService, ICameraService cameraService, IRmpGrpcService rmpGrpcService)
    {
        // services
        _sshService     = sshService ?? throw new ArgumentNullException(nameof(sshService));
        _cameraService  = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
        _rmpGrpcService = rmpGrpcService ?? throw new ArgumentNullException(nameof(rmpGrpcService));

        // load ui settings from file
        StorageLoad();

        // try to connect to a previous rapidserver
        if (!string.IsNullOrEmpty(IpAddress) && Port > 0)
        {
            try
            {
                _ = ConnectAsync();
                LogMessage($"Auto-connected to rapidserver {IpAddress}:{Port}");
            }
            catch (Exception ex)
            {
                LogMessage($"rapidserver auto-connect failed: {ex.Message}");
            }
        }

        //connection
        _rmpGrpcService.IsConnectedChanged += (s, isConnected) =>
        {
            IsConnected = isConnected;

            if (isConnected)
            {
                ControllerSampleRate_khz = _rmpGrpcService.ControllerConfig?.SampleRate / 1000.0; // convert to khz
            }
        };
    }


    /** POLLING **/
    private readonly SemaphoreSlim _pollingSemaphore = new(1, 1);

    private async void OnUpdateTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // skip if already running
        if (!await _pollingSemaphore.WaitAsync(0))
            return;

        try
        {
            // ping server to see if we are still connected
            IsConnected = await _rmpGrpcService.CheckConnectionAsync();

            // checks
            if (!IsConnected)
                return;

            //controller status - we'll implement this later when we have motion control
            // For now, just simulate some data
            try { ControllerStatus = await _rmpGrpcService.GetControllerStatusAsync(); }
            catch { ControllerStatus = null; }

            //network status
            try { NetworkStatus = (ControllerStatus != null) ? await _rmpGrpcService.GetNetworkStatusAsync() : null; }
            catch { NetworkStatus = null; }

            // ball positions
            if (!IsSimulatingBallPosition)
            {
                //tm status
                try
                {
                    TaskManagerStatus = (ControllerStatus != null) ? await _rmpGrpcService.GetTaskManagerStatusAsync() : null;

                    var taskIds = TaskManagerStatus.TaskIds.ToList();
                    await UpdateTaskCollectionsAsync(taskIds);

                    // update tasks
                    var batch = await _rmpGrpcService.GetTaskBatchResponseAsync(TaskIds);
                    foreach (var response in batch.Responses)
                    {
                        var task = Tasks.FirstOrDefault(t => t.Id == response.Id);
                        var status = response.Status;

                        if (task != null && status != null)
                            task.UpdateTaskInfo(status);
                    }

                    IsProgramRunning = TaskManagerStatus != null && TaskManagerStatus.State == RTTaskManagerState.Running;
                }
                catch
                {
                    TaskManagerStatus = null;
                }

                //tm globals
                if (TaskManagerStatus != null)
                    await UpdateGlobalValues();
            }
            else
            {
                UpdateGlobalValuesWithFakeData();
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Polling Error: {ex.Message}");
        }
        finally
        {
            _pollingSemaphore.Release();
        }
    }


    /** METHODS **/
    //logging
    private void LogMessage(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss tt");
        LogOutput += $"[{timestamp}] {message}\n";
    }

    //camera streaming
    private async Task StreamCameraFramesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var frameTimeTracker = DateTime.Now;

            // Calculate target frame interval for camera streaming
            var targetFrameIntervalMs = (int)(1000.0 / CAMERA_TARGET_FPS);

            while (!cancellationToken.IsCancellationRequested && IsCameraStreaming)
            {
                var frameStartTime = DateTime.Now;

                try
                {
                    // For HttpCameraService, bypass the IsProgramRunning check since it has its own RT task verification
                    if (_cameraService is not HttpCameraService)
                    {
                        // Only stream if program is running for non-HTTP camera services
                        if (!IsProgramRunning)
                        {
                            await Task.Delay(100, cancellationToken); // Wait and check again
                            continue;
                        }
                    }

                    // Check server status before attempting to grab frame
                    var serverStatus = await _cameraService.CheckServerStatusAsync(cancellationToken);
                    if (!serverStatus)
                    {
                        // Server is not responding, stop streaming and update status
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            LogMessage("Camera: HTTP server is not responding, stopping stream");
                        });

                        // Stop streaming gracefully
                        IsCameraStreaming = false;
                        break;
                    }

                    // Get frame from independent camera service
                    var (success, frameData) = await _cameraService.TryGrabFrameAsync(cancellationToken);

                    if (success && frameData.ImageData.Length > 0)
                    {
                        // Process the frame
                        await ProcessHttpCameraFrameAsync(frameData);
                    }

                    // Calculate how long this frame took to process
                    var processingTime = (DateTime.Now - frameStartTime).TotalMilliseconds;

                    // Calculate delay needed to maintain target frame rate
                    var remainingTime = targetFrameIntervalMs - processingTime;

                    if (remainingTime > 0)
                    {
                        // Wait the remaining time to maintain target frame rate
                        await Task.Delay((int)remainingTime, cancellationToken);
                    }
                    else if (!success)
                    {
                        // If frame grab failed and we have no remaining time, add a small delay to prevent tight loop
                        await Task.Delay(Math.Min(targetFrameIntervalMs, 33), cancellationToken);
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    LogMessage($"Camera Frame Error: {ex.Message}");
                    await Task.Delay(100, cancellationToken); // Wait before retrying
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when streaming is cancelled
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LogMessage($"Camera Stream Error: {ex.Message}");
            });
        }
        finally
        {
            // Ensure proper cleanup when streaming stops
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (IsCameraStreaming)
                {
                    IsCameraStreaming = false;
                    CameraImage = null;
                }
            });
        }
    }

    private async Task ProcessHttpCameraFrameAsync(CameraFrameData frameData)
    {
        try
        {
            // Update camera data properties from the frame
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    // Update camera data from frame
                    CameraFrameNumber = frameData.FrameNumber;
                    CameraTimestamp = frameData.Timestamp;
                    CameraBallDetected = frameData.BallDetected;
                    CameraCenterX = frameData.CenterX;
                    CameraCenterY = frameData.CenterY;
                    CameraRadius = frameData.Radius;
                    CameraTargetX = frameData.TargetX;
                    CameraTargetY = frameData.TargetY;

                    // Process image data - For HTTP camera service, imageData is a JPEG byte array
                    // We can load it directly into a bitmap
                    using var stream = new MemoryStream(frameData.ImageData);
                    var bitmap = new Bitmap(stream);

                    // Convert to WriteableBitmap for UI binding
                    var writeableBitmap = new WriteableBitmap(new PixelSize(bitmap.PixelSize.Width, bitmap.PixelSize.Height),
                                                              new Vector(96, 96),
                                                              PixelFormat.Rgba8888,
                                                              AlphaFormat.Premul);

                    // Copy pixels from the bitmap to the WriteableBitmap (lock the bitmap for writing)
                    using var lockedBitmap = writeableBitmap.Lock();
                    var sourceRect = new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
                    var buffer = lockedBitmap.Address;
                    var bufferSize = lockedBitmap.RowBytes * bitmap.PixelSize.Height;
                    var stride = lockedBitmap.RowBytes;

                    bitmap.CopyPixels(sourceRect, buffer, bufferSize, stride);

                    // Update the camera image
                    CameraImage = writeableBitmap;
                }
                catch (Exception ex)
                {
                    LogMessage($"Image Processing Error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            LogMessage($"Frame Processing Error: {ex.Message}");
        }
    }

    //globals
    private async Task UpdateGlobalValues()
    {
        if (_rmpGrpcService == null) return;

        // Update global value names if task manager status is available
        await UpdateGlobalValuesAsync();

        // Update ball position from global values
        UpdateBallPositionFromGlobals();
    }

    private async Task UpdateTaskCollectionsAsync(List<int> taskIds)
    {
        // Update TaskIds collection on UI thread - sync with current task IDs
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // Add IDs & Tasks
            foreach (var id in taskIds.Where(id => !TaskIds.Contains(id)))
            {
                // manage id
                TaskIds.Add(id);

                // manage tasks
                var task = new RtTask(id);

                try
                {
                    var creationParameters = await _rmpGrpcService.GetTaskCreationParametersAsync(id);
                    task.Function = creationParameters.FunctionName ?? $"Task_{id}"; // Fallback if function name is null
                    task.Period = creationParameters.Period;
                    task.Priority = creationParameters.Priority;
                }
                catch (Exception ex)
                {
                    task.Function = $"Task_{id}"; // Fallback if GetTaskFunctionAsync fails
                    LogMessage($"Failed to get task function for ID {id}: {ex.Message}");
                }

                Tasks.Add(task);

                // sort by TaskPriority (highest priority first)
                var sorted = Tasks.OrderByDescending(t => t.Priority).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (!ReferenceEquals(Tasks[i], sorted[i]))
                    {
                        Tasks.Move(Tasks.IndexOf(sorted[i]), i);
                    }
                }

            }

            // Remove IDs & Tasks that no longer exist
            for (int i = TaskIds.Count - 1; i >= 0; i--)
            {
                if (!taskIds.Contains(TaskIds[i]))
                {
                    var taskId = TaskIds[i];

                    // manage id
                    TaskIds.RemoveAt(i);

                    // manage tasks
                    var taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId);
                    if (taskToRemove != null)
                        Tasks.Remove(taskToRemove);
                }
            }
        });
    }

    private async Task UpdateGlobalValuesAsync()
    {
        if (TaskManagerStatus?.GlobalValues == null) return;

        var globalNames = TaskManagerStatus.GlobalValues.Keys.ToHashSet();
        var currentNames = GlobalValues.Select(x => x.Name).ToHashSet();

        // Only rebuild if names actually changed (added/removed globals)
        var namesChanged = !globalNames.SetEquals(currentNames);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (namesChanged)
            {
                // Handle added globals
                var newGlobals = globalNames.Except(currentNames).ToList();
                foreach (var name in newGlobals)
                {
                    var valueItem = new GlobalValueItem
                    {
                        Name = name
                    };

                    if (TaskManagerStatus.GlobalValues.TryGetValue(name, out var globalValue))
                    {
                        valueItem.Value = FormatGlobalValue(globalValue);
                        valueItem.IsNumeric = globalValue.ValueCase != FirmwareValue.ValueOneofCase.BoolValue;
                    }

                    GlobalValues.Add(valueItem);
                }

                // Handle removed globals
                var removedGlobals = currentNames.Except(globalNames).ToList();
                for (int i = GlobalValues.Count - 1; i >= 0; i--)
                {
                    if (removedGlobals.Contains(GlobalValues[i].Name))
                    {
                        GlobalValues.RemoveAt(i);
                    }
                }

                // Sort after any add/remove
                SortGlobalValues();

                // Notify that globalValues have changed
                OnGlobalValuesChanged(GlobalValues);
            }

            // Always update values for existing items (this is the common case)
            foreach (var item in GlobalValues)
            {
                if (TaskManagerStatus.GlobalValues.TryGetValue(item.Name, out var globalValue))
                {
                    var newValue = FormatGlobalValue(globalValue);
                    if (item.Value != newValue)
                    {
                        item.Value = newValue;
                    }
                }
            }
        });
    }

    private void SortGlobalValues()
    {
        var sorted = GlobalValues.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            if (!ReferenceEquals(GlobalValues[i], sorted[i]))
            {
                GlobalValues.Move(GlobalValues.IndexOf(sorted[i]), i);
            }
        }
    }

    private void UpdateBallPositionFromGlobals()
    {
        if (TaskManagerStatus?.GlobalValues == null) return;

        // Extract ball position values with helper method
        Program_BallX           = GetDoubleValueFromGlobal(Global_BallX) ?? Program_BallX;
        Program_BallY           = GetDoubleValueFromGlobal(Global_BallY) ?? Program_BallY;
        Program_BallRadius      = GetDoubleValueFromGlobal(Global_BallRadius) ?? Program_BallRadius;
        Program_IsMotionEnabled = GetBooleanValueFromGlobal(Global_IsMotionEnabled) ?? Program_IsMotionEnabled;
        Program_CameraFps       = GetDoubleValueFromGlobal(Global_CameraFps) ?? Program_CameraFps;

        // Uncomment and use if needed:
        // DetectionConfidence = GetDoubleValueFromGlobal("DetectionConfidence") ?? DetectionConfidence;
    }

    private double? GetDoubleValueFromGlobal(string? globalName)
    {
        if (string.IsNullOrEmpty(globalName) ||
            TaskManagerStatus?.GlobalValues == null ||
            !TaskManagerStatus.GlobalValues.TryGetValue(globalName, out var value))
        {
            return null;
        }

        // Handle different numeric value types that can be converted to double
        return value.ValueCase switch
        {
            FirmwareValue.ValueOneofCase.DoubleValue => value.DoubleValue,
            FirmwareValue.ValueOneofCase.FloatValue => (double)value.FloatValue,
            FirmwareValue.ValueOneofCase.Int32Value => (double)value.Int32Value,
            FirmwareValue.ValueOneofCase.Uint32Value => (double)value.Uint32Value,
            FirmwareValue.ValueOneofCase.Int64Value => (double)value.Int64Value,
            FirmwareValue.ValueOneofCase.Uint64Value => (double)value.Uint64Value,
            FirmwareValue.ValueOneofCase.Int16Value => (double)value.Int16Value,
            FirmwareValue.ValueOneofCase.Uint16Value => (double)value.Uint16Value,
            FirmwareValue.ValueOneofCase.Int8Value => (double)value.Int8Value,
            FirmwareValue.ValueOneofCase.Uint8Value => (double)value.Uint8Value,
            _ => null // Cannot convert to double
        };
    }

    private bool? GetBooleanValueFromGlobal(string? globalName)
    {
        if (string.IsNullOrEmpty(globalName) ||
            TaskManagerStatus?.GlobalValues == null ||
            !TaskManagerStatus.GlobalValues.TryGetValue(globalName, out var value))
        {
            return null;
        }

        // Handle boolean and numeric value types: 0 => false, 1 => true, else null
        return value.ValueCase switch
        {
            FirmwareValue.ValueOneofCase.BoolValue => value.BoolValue,
            FirmwareValue.ValueOneofCase.Int8Value => value.Int8Value == 0 ? false : value.Int8Value == 1 ? true : null,
            FirmwareValue.ValueOneofCase.Uint8Value => value.Uint8Value == 0 ? false : value.Uint8Value == 1 ? true : null,
            FirmwareValue.ValueOneofCase.Int16Value => value.Int16Value == 0 ? false : value.Int16Value == 1 ? true : null,
            FirmwareValue.ValueOneofCase.Uint16Value => value.Uint16Value == 0 ? false : value.Uint16Value == 1 ? true : null,
            FirmwareValue.ValueOneofCase.Int32Value => value.Int32Value == 0 ? false : value.Int32Value == 1 ? true : null,
            FirmwareValue.ValueOneofCase.Uint32Value => value.Uint32Value == 0 ? false : value.Uint32Value == 1 ? true : null,
            FirmwareValue.ValueOneofCase.Int64Value => value.Int64Value == 0 ? false : value.Int64Value == 1 ? true : null,
            FirmwareValue.ValueOneofCase.Uint64Value => value.Uint64Value == 0 ? false : value.Uint64Value == 1 ? true : null,
            FirmwareValue.ValueOneofCase.FloatValue => value.FloatValue == 0f ? false : value.FloatValue == 1f ? true : null,
            FirmwareValue.ValueOneofCase.DoubleValue => value.DoubleValue == 0.0 ? false : value.DoubleValue == 1.0 ? true : null,
            _ => null // Cannot convert to bool
        };
    }

    private string FormatGlobalValue(FirmwareValue value)
    {
        return value.ValueCase switch
        {
            FirmwareValue.ValueOneofCase.None => "None",
            FirmwareValue.ValueOneofCase.BoolValue => value.BoolValue.ToString(),
            FirmwareValue.ValueOneofCase.Int8Value => value.Int8Value.ToString("N0"),
            FirmwareValue.ValueOneofCase.Uint8Value => value.Uint8Value.ToString("N0"),
            FirmwareValue.ValueOneofCase.Int16Value => value.Int16Value.ToString("N0"),
            FirmwareValue.ValueOneofCase.Uint16Value => value.Uint16Value.ToString("N0"),
            FirmwareValue.ValueOneofCase.Int32Value => value.Int32Value.ToString("N0"),
            FirmwareValue.ValueOneofCase.Uint32Value => value.Uint32Value.ToString("N0"),
            FirmwareValue.ValueOneofCase.FloatValue => value.FloatValue.ToString("N3"),
            FirmwareValue.ValueOneofCase.DoubleValue => value.DoubleValue.ToString("N3"),
            FirmwareValue.ValueOneofCase.Int64Value => value.Int64Value.ToString("N0"),
            FirmwareValue.ValueOneofCase.Uint64Value => value.Uint64Value.ToString("N0"),
            _ => value.ToString() // Fallback to string representation
        };
    }

    //simulation
    private void UpdateGlobalValuesWithFakeData()
    {
        // Constants for simulation
        const int CanvasWidth = 640;
        const int CanvasHeight = 480;
        const double MinConfidence = 85.0;
        const double MaxConfidence = 100.0;

        // Generate random ball radius between 10 and 50
        var ballRadius = 10 + _random.NextDouble() * 40; // Random between 10 and 50

        // Calculate valid position bounds  
        var minPosition = ballRadius;
        var maxXPosition = CanvasWidth - ballRadius;
        var maxYPosition = CanvasHeight - ballRadius;

        // Generate random positions and velocities
        Program_BallX = minPosition + _random.NextDouble() * (maxXPosition - minPosition);
        Program_BallY = minPosition + _random.NextDouble() * (maxYPosition - minPosition);
        Program_BallRadius = ballRadius; // Random radius for simulation

        DetectionConfidence = MinConfidence + _random.NextDouble() * (MaxConfidence - MinConfidence);
    }

    //storage
    private void StorageLoad()
    {
        //storage
        var configPath = Path.Combine(AppContext.BaseDirectory, "RapidLaser.json");
        var settings = new ConfigurationBuilder()
                            .AddJsonFile(configPath)
                            .Build()
                            .GetSection("Settings");

        //server
        IpAddress         = settings["server_IpAddress"] ?? "localhost";
        Port              = int.TryParse(settings["server_Port"], out var port) ? port : 50061;
        var autoReconnect = bool.TryParse(settings["server_AutoReconnect"], out var reconnect) && reconnect;

        //camera server
        CameraPort = int.TryParse(settings["cameraServer_Port"], out var cameraPort) ? cameraPort : 50080;

        //polling
        _updateIntervalMs = int.TryParse(settings["polling_IntervalMs"], out var pollingInterval) ? pollingInterval : 100;

        //ssh
        SshUser           = settings["ssh_Username"] ?? "";
        SshPassword       = settings["ssh_Password"] ?? "";
        SshRunCommand     = settings["ssh_RunCommand"] ?? "";

        //globals
        Global_BallX      = settings["global_BallX"] ?? "";
        Global_BallY      = settings["global_BallY"] ?? "";
        Global_BallRadius = settings["global_BallRadius"] ?? "";
        Global_IsMotionEnabled = settings["global_IsMotionEnabled"] ?? "";

        //camera
        FrameRate = double.TryParse(settings["camera_FrameRate"], out var frameRate) ? frameRate : CAMERA_TARGET_FPS;
        BinaryThreshold = int.TryParse(settings["camera_BinaryThreshold"], out var threshold) ? threshold : 128;
    }

    private void StorageSave()
    {
        try
        {
            // Create updated configuration object
            var configData = new
            {
                Settings = new
                {
                    //rapidserver
                    server_ipAddress = IpAddress,
                    server_port = Port.ToString(),

                    //camera server
                    cameraServer_Port = CameraPort.ToString(),

                    //polling
                    polling_IntervalMs = _updateIntervalMs,

                    //ssh
                    ssh_Username = SshUser,
                    ssh_Password = SshPassword,
                    ssh_RunCommand = SshRunCommand,

                    //globals
                    global_BallX = Global_BallX,
                    global_BallY = Global_BallY,
                    global_BallRadius = Global_BallRadius,
                    global_IsMotionEnabled = Global_IsMotionEnabled,
                }
            };

            // Serialize to JSON with proper formatting
            var json = JsonSerializer.Serialize(configData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var configPath = Path.Combine(AppContext.BaseDirectory, "RapidLaser.json");
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save configuration: {ex.Message}");
        }
    }

    //ssh
    private async Task TestSshAuthenticationAsync()
    {
        // Reset authentication status
        IsSshAuthenticated = false;

        // Skip if credentials are not provided
        if (string.IsNullOrWhiteSpace(SshUser) || string.IsNullOrWhiteSpace(SshPassword))
        {
            return;
        }

        try
        {
            // Test SSH connection with a simple command that should work on most systems
            var result = await ExecuteSshCommandAsync("whoami", updateSshOutput: false);

            // If we get a result (not null), SSH authentication was successful
            IsSshAuthenticated = !string.IsNullOrWhiteSpace(result);
        }
        catch
        {
            // Any exception means authentication failed
            IsSshAuthenticated = false;
        }
    }

    private async Task<string?> ExecuteSshCommandAsync(string command, bool updateSshOutput = true)
    {
        if (!IsConnected)
        {
            if (updateSshOutput) LogMessage("SSH: Not connected to server");
            return null;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            if (updateSshOutput)
                LogMessage("SSH: Please enter a command");
            return null;
        }

        if (string.IsNullOrWhiteSpace(SshUser) || string.IsNullOrWhiteSpace(SshPassword))
        {
            if (updateSshOutput)
                LogMessage("SSH: Please enter SSH username and password");
            return null;
        }

        try
        {
            if (updateSshOutput)
            {
                IsSshCommandRunning = true;
                LogMessage($"SSH: Running command '{command}'...");
            }

            var result = await _sshService.RunSshCommandAsync(command, SshUser, SshPassword, IpAddress);

            if (updateSshOutput)
            {
                LogMessage($"SSH Command Output:\n{result}");
                LogMessage("SSH: Command completed");
            }

            return result;
        }
        catch (Exception ex)
        {
            var errorMessage = $"SSH Error: {ex.Message}";
            if (updateSshOutput)
            {
                LogMessage(errorMessage);
            }
            return null;
        }
        finally
        {
            if (updateSshOutput)
            {
                IsSshCommandRunning = false;
            }
        }
    }

    //window 
    public void Dispose()
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();

        // Clean up camera streaming
        _cameraStreamCancellation?.Cancel();
        _cameraService?.Dispose();

        GC.SuppressFinalize(this);
    }

    public void SetMainWindow(Window window)
    {
        _mainWindow = window;
        _mainWindow.Closed += (s, e) =>
        {
            StorageSave();
            Dispose();
        };
    }

}
