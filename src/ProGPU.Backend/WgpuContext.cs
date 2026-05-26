using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;

namespace ProGPU.Backend;

public unsafe class WgpuContext : IDisposable
{
    public WebGPU Wgpu { get; private set; } = null!;
    public Instance* Instance { get; private set; } = null;
    public Adapter* Adapter { get; private set; } = null;
    public Device* Device { get; private set; } = null;
    public Queue* Queue { get; private set; } = null;
    public Surface* Surface { get; private set; } = null;
    public TextureFormat SwapChainFormat { get; private set; } = TextureFormat.Bgra8Unorm;
    
    private bool _isDisposed;
    private uint _lastWidth = 1;
    private uint _lastHeight = 1;
    private bool _vsync = false;

    public bool VSync
    {
        get => _vsync;
        set
        {
            if (_vsync != value)
            {
                _vsync = value;
                if (Surface != null)
                {
                    ConfigureSwapChain(_lastWidth, _lastHeight);
                }
            }
        }
    }

    private static readonly List<WgpuContext> _activeContexts = new();

    public static IReadOnlyList<WgpuContext> ActiveContexts
    {
        get
        {
            lock (_activeContexts)
            {
                return _activeContexts.ToArray();
            }
        }
    }

    private static WgpuContext? _current;

    public static WgpuContext? Current
    {
        get => _current;
        set => _current = value;
    }

    private IWindow? _window;
    public IWindow? Window => _window;

    public void Initialize(IWindow? window)
    {
        lock (_activeContexts)
        {
            if (!_activeContexts.Contains(this))
            {
                _activeContexts.Add(this);
            }
        }
        Current = this;
        _window = window;
        Wgpu = WebGPU.GetApi();
        
        // 1. Create WebGPU Instance
        var instanceDesc = new InstanceDescriptor();
        Instance = Wgpu.CreateInstance(&instanceDesc);
        if (Instance == null)
        {
            throw new InvalidOperationException("Failed to create WebGPU Instance.");
        }

        // 2. Create Surface if window is provided
        if (window != null)
        {
            Surface = window.CreateWebGPUSurface(Wgpu, Instance);
            if (Surface == null)
            {
                throw new InvalidOperationException("Failed to create WebGPU Surface from window.");
            }
        }

        // 3. Request Adapter (synchronously)
        var adapterSignal = new ManualResetEventSlim(false);
        Adapter* requestedAdapter = null;

        var requestAdapterOptions = new RequestAdapterOptions
        {
            CompatibleSurface = Surface,
            PowerPreference = PowerPreference.HighPerformance
        };

        var onAdapterReceived = PfnRequestAdapterCallback.From((status, adapter, message, userData) =>
        {
            if (status == RequestAdapterStatus.Success)
            {
                requestedAdapter = adapter;
            }
            else
            {
                string msg = (message != null ? SilkMarshal.PtrToString((nint)message) : null) ?? "Unknown error";
                Console.WriteLine($"[WebGPU] RequestAdapter failed: {msg}");
            }
            adapterSignal.Set();
        });

        Wgpu.InstanceRequestAdapter(Instance, &requestAdapterOptions, onAdapterReceived, null);
        adapterSignal.Wait();
        
        if (requestedAdapter == null)
        {
            throw new InvalidOperationException("Failed to obtain WebGPU Adapter.");
        }
        Adapter = requestedAdapter;

        // 4. Request Device (synchronously)
        var deviceSignal = new ManualResetEventSlim(false);
        Device* requestedDevice = null;

        var deviceDesc = new DeviceDescriptor
        {
            Label = (byte*)SilkMarshal.StringToPtr("ProGPU Primary Device")
        };

        var onDeviceReceived = PfnRequestDeviceCallback.From((status, device, message, userData) =>
        {
            if (status == RequestDeviceStatus.Success)
            {
                requestedDevice = device;
            }
            else
            {
                string msg = (message != null ? SilkMarshal.PtrToString((nint)message) : null) ?? "Unknown error";
                Console.WriteLine($"[WebGPU] RequestDevice failed: {msg}");
            }
            deviceSignal.Set();
        });

        Wgpu.AdapterRequestDevice(Adapter, &deviceDesc, onDeviceReceived, null);
        deviceSignal.Wait();

        // Free labeled string
        SilkMarshal.Free((nint)deviceDesc.Label);

        if (requestedDevice == null)
        {
            throw new InvalidOperationException("Failed to obtain WebGPU Device.");
        }
        Device = requestedDevice;

        // 5. Retrieve Default Queue
        Queue = Wgpu.DeviceGetQueue(Device);

        // 6. Hook up validation error callback
        Wgpu.DeviceSetUncapturedErrorCallback(Device, PfnErrorCallback.From((type, msg, _) =>
        {
            string errorMsg = (msg != null ? SilkMarshal.PtrToString((nint)msg) : null) ?? "Unknown error";
            Console.WriteLine($"[WebGPU Error] Type: {type}, Message: {errorMsg}");
        }), null);

        // 7. Configure Surface if window exists
        if (window != null && Surface != null)
        {
            ConfigureSwapChain((uint)window.FramebufferSize.X, (uint)window.FramebufferSize.Y);
        }
    }

    public void ConfigureSwapChain(uint width, uint height)
    {
        if (Surface == null || Device == null) return;

        _lastWidth = width;
        _lastHeight = height;

        // Synchronize GLFW window VSync state with WebGPU context VSync state dynamically
        if (_window != null)
        {
            _window.VSync = _vsync;
        }

        // 7a. Query supported formats
        var capabilities = new SurfaceCapabilities();
        Wgpu.SurfaceGetCapabilities(Surface, Adapter, &capabilities);
        
        SwapChainFormat = TextureFormat.Bgra8Unorm; // Default fallback
        if (capabilities.FormatCount > 0 && capabilities.Formats != null)
        {
            // Prefer Bgra8Unorm or Rgba8Unorm
            SwapChainFormat = capabilities.Formats[0];
            for (uint i = 0; i < capabilities.FormatCount; i++)
            {
                if (capabilities.Formats[i] == TextureFormat.Bgra8Unorm)
                {
                    SwapChainFormat = TextureFormat.Bgra8Unorm;
                    break;
                }
            }
        }

        // Get standard composite alpha mode
        var alphaMode = CompositeAlphaMode.Opaque;
        if (capabilities.AlphaModeCount > 0 && capabilities.AlphaModes != null)
        {
            alphaMode = capabilities.AlphaModes[0];
        }

        PresentMode presentMode = PresentMode.Fifo;
        if (!_vsync)
        {
            // Prefer Immediate mode directly for truly uncapped framerates (tearing allowed),
            // which bypasses driver and OS compositor presentation queuing / mailbox locks.
            bool foundImmediate = false;
            if (capabilities.PresentModeCount > 0 && capabilities.PresentModes != null)
            {
                for (uint i = 0; i < capabilities.PresentModeCount; i++)
                {
                    if (capabilities.PresentModes[i] == PresentMode.Immediate)
                    {
                        presentMode = PresentMode.Immediate;
                        foundImmediate = true;
                        break;
                    }
                }
            }

            if (!foundImmediate)
            {
                // Force Immediate when VSync is off to guarantee uncapped framerates
                presentMode = PresentMode.Immediate;
            }
        }

        Console.WriteLine($"[WebGPU Context] Configuring SwapChain: {width}x{height}, VSync: {_vsync}, Selected Mode: {presentMode}");

        Wgpu.SurfaceCapabilitiesFreeMembers(capabilities);

        // 7b. Surface Configuration
        var config = new SurfaceConfiguration
        {
            Device = Device,
            Format = SwapChainFormat,
            Usage = TextureUsage.RenderAttachment,
            AlphaMode = alphaMode,
            PresentMode = presentMode,
            Width = width > 0 ? width : 1,
            Height = height > 0 ? height : 1
        };

        Wgpu.SurfaceConfigure(Surface, &config);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        lock (_activeContexts)
        {
            _activeContexts.Remove(this);
        }
        
        if (Device != null)
        {
            Wgpu.DeviceRelease(Device);
            Device = null;
        }
        if (Adapter != null)
        {
            Wgpu.AdapterRelease(Adapter);
            Adapter = null;
        }
        if (Surface != null)
        {
            Wgpu.SurfaceRelease(Surface);
            Surface = null;
        }
        if (Instance != null)
        {
            Wgpu.InstanceRelease(Instance);
            Instance = null;
        }
        
        _isDisposed = true;
        
        GC.SuppressFinalize(this);
    }

    ~WgpuContext()
    {
        Dispose();
    }
}
