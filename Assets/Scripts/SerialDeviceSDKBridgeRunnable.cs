using UnityEngine;
using System;
using UnityEngine.Android;
using System.Collections;

public class SerialDeviceSDKBridge : MonoBehaviour
{
    [Header("Auto Config After Connected")]
    public bool AutoConfigureAfterConnected = true;
    public bool EnableNMEA = true;
    public bool EnableLocation = true;
    public bool SetIntervalHz5 = true;

    [Header("Debug")]
    public bool VerboseLog = true;

    private AndroidJavaClass _scanManagerClass;      // com.woncan.device.ScanManager
    private AndroidJavaObject _currentSerialDevice;  // com.woncan.device.device.SerialDevice (或 Device 的子类)
    private AndroidJavaObject _unityContext;         // UnityPlayer.currentActivity

    private bool _isAndroid;
    private bool _isConnected;
    private bool _configuredOnce;
    public event Action<bool> OnConnectionChanged;
    public event Action<string> OnNMEAReceived;
    public event Action<LocationSnapshot> OnLocationReceived;

    [Serializable]
    public struct LocationSnapshot
    {
        public int fixStatus;
        public double latitude;
        public double longitude;
        public float accuracy;
        public long timeMillis;

        public override string ToString()
            => $"fix={fixStatus}, lat={latitude}, lon={longitude}, acc={accuracy}, t={timeMillis}";
    }

    private void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _isAndroid = true;
#else
        _isAndroid = false;
#endif
    }

    private void Start()
    {
        if (!_isAndroid)
        {
            LogW("当前非 Android 真机环境（或在 Editor），桥接不会执行。");
            return;
        }

        try
        {
            _unityContext = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");

            _scanManagerClass = new AndroidJavaClass("com.woncan.device.ScanManager");

            LogI("SerialDeviceSDKBridge 初始化完成。");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SerialDeviceSDKBridge] 初始化失败：{e}");
        }
    }

    public void StartScanSerialDevice()
    {
        if (!EnsureAndroidReady()) return;

        LogI("开始扫描串口设备（scanSerialDevice）... 请确保设备已通过 USB 连接，并在系统弹窗中授权。");

        // device-s 文档里 ScanManager.scanSerialDevice(context, listener)
        TryCallStatic(_scanManagerClass, "scanSerialDevice",
            _unityContext,
            new SerialDeviceCallback(serialDevice =>
            {
                _currentSerialDevice = serialDevice;
                _configuredOnce = false;

                string deviceName = SafeCall<string>(_currentSerialDevice, "getName", "(unknown)");
                LogI($"扫描到串口设备：{deviceName}");

                // 你也可以在这里自动连接（按需）
                // ConnectSerialDevice();
            })
        );
    }

    public void StopSerialDeviceScan()
    {
        if (!EnsureAndroidReady()) return;

        LogI("停止扫描串口设备（stopScan）...");
        TryCallStatic(_scanManagerClass, "stopScan", _unityContext);
    }

    private class SerialDeviceCallback : AndroidJavaProxy
    {
        private readonly Action<AndroidJavaObject> _onFound;

        public SerialDeviceCallback(Action<AndroidJavaObject> onFound)
            : base("com.woncan.device.listener.OnScanListener")
        {
            _onFound = onFound;
        }

        public void onScanDevice(AndroidJavaObject serialDevice)
        {
            _onFound?.Invoke(serialDevice);
        }
    }

    public void ConnectSerialDevice()
    {
        if (!EnsureAndroidReady()) return;

        if (_currentSerialDevice == null)
        {
            Debug.LogError("[SerialDeviceSDKBridge] 未找到串口设备，请先扫描 StartScanSerialDevice()");
            return;
        }

        RegisterDeviceStateListener();

        LogI("调用 device.connect(context) ...");
        TryCall(_currentSerialDevice, "connect", _unityContext);
    }

    public void DisconnectSerialDevice()
    {
        if (!EnsureAndroidReady()) return;

        if (_currentSerialDevice == null) return;

        LogI("调用 device.disconnect() ...");
        TryCall(_currentSerialDevice, "disconnect");

        _isConnected = false;
        _configuredOnce = false;
        OnConnectionChanged?.Invoke(false);
    }


    private void RegisterDeviceStateListener()
    {
        if (_currentSerialDevice == null) return;

        LogI("注册 device.registerSatesListener(...) ...");

        var stateListener = new DeviceStatesListener(
            onConnectionChange: isConnected =>
            {
                _isConnected = isConnected;
                LogI($"连接状态回调：{isConnected}");

                OnConnectionChanged?.Invoke(isConnected);

                if (isConnected && AutoConfigureAfterConnected && !_configuredOnce)
                {
                    _configuredOnce = true;
                    StartCoroutine(ConfigureAfterConnected());
                }
            },
            onDeviceInfoChange: deviceInfo =>
            {
                try
                {
                    string model = SafeCall<string>(deviceInfo, "getModel", "");
                    string deviceId = SafeCall<string>(deviceInfo, "getDeviceID", "");
                    string productName = SafeCall<string>(deviceInfo, "getProductNameZH", "");
                    LogI($"设备信息：model={model}, id={deviceId}, product={productName}");
                }
                catch (Exception e)
                {
                    LogW("解析设备信息失败：" + e.Message);
                }
            },
            onDeviceAccountChange: account =>
            {
                LogI("设备账户信息回调");
            },
            onLaserStateChange: isOpen =>
            {
                LogI($"激光状态回调：{isOpen}");
            }
        );

        TryCall(_currentSerialDevice, "registerSatesListener", stateListener);
    }

    private class DeviceStatesListener : AndroidJavaProxy
    {
        private readonly Action<bool> _onConnectionChange;
        private readonly Action<AndroidJavaObject> _onDeviceInfoChange;
        private readonly Action<AndroidJavaObject> _onDeviceAccountChange;
        private readonly Action<bool> _onLaserStateChange;

        public DeviceStatesListener(
            Action<bool> onConnectionChange,
            Action<AndroidJavaObject> onDeviceInfoChange,
            Action<AndroidJavaObject> onDeviceAccountChange,
            Action<bool> onLaserStateChange)
            : base("com.woncan.device.listener.DeviceStatesListener")
        {
            _onConnectionChange = onConnectionChange;
            _onDeviceInfoChange = onDeviceInfoChange;
            _onDeviceAccountChange = onDeviceAccountChange;
            _onLaserStateChange = onLaserStateChange;
        }

        public void onConnectionStateChange(bool isConnected) => _onConnectionChange?.Invoke(isConnected);
        public void onDeviceInfoChange(AndroidJavaObject deviceInfo) => _onDeviceInfoChange?.Invoke(deviceInfo);
        public void onDeviceAccountChange(AndroidJavaObject account) => _onDeviceAccountChange?.Invoke(account);
        public void onLaserStateChange(bool isOpen) => _onLaserStateChange?.Invoke(isOpen);
    }

    private IEnumerator ConfigureAfterConnected()
    {
        yield return new WaitForSeconds(0.2f);

        if (_currentSerialDevice == null || !_isConnected)
            yield break;

        LogI("开始连接后配置：Interval / NMEA / Location ...");

        if (SetIntervalHz5) SetDataFrequencyHz5();

        if (EnableNMEA)
        {
            EnableNMEAData();
            RegisterNMEAListener();
        }

        if (EnableLocation)
        {
            RegisterLocationListener();
        }

        LogI("连接后配置完成。");
    }

    private void SetDataFrequencyHz5()
    {
        try
        {
            AndroidJavaClass intervalClass = new AndroidJavaClass("com.woncan.device.device.DeviceInterval");
            AndroidJavaObject hz5 = intervalClass.GetStatic<AndroidJavaObject>("HZ_5");
            TryCall(_currentSerialDevice, "setInterval", hz5);
            LogI("数据频率设置为 5Hz");
        }
        catch (Exception e)
        {
            Debug.LogError("[SerialDeviceSDKBridge] setInterval 失败：" + e);
        }
    }

    private void RegisterNMEAListener()
    {
        if (_currentSerialDevice == null) return;

        try
        {
            var nmeaListener = new NMEAListener(nmeaData =>
            {
                if (string.IsNullOrEmpty(nmeaData)) return;
                LogI($"NMEA: {nmeaData}");
                OnNMEAReceived?.Invoke(nmeaData);
            });

            TryCall(_currentSerialDevice, "setNMEAListener", nmeaListener);
            LogI("NMEA 监听器注册成功");
        }
        catch (Exception e)
        {
            Debug.LogError("[SerialDeviceSDKBridge] 注册 NMEA 监听器失败：" + e);
        }
    }

    private class NMEAListener : AndroidJavaProxy
    {
        private readonly Action<string> _onReceive;

        public NMEAListener(Action<string> onReceive)
            : base("com.woncan.device.listener.NMEAListener")
        {
            _onReceive = onReceive;
        }

        public void onReceiveNMEA(string nmeaData) => _onReceive?.Invoke(nmeaData);
    }

    private void EnableNMEAData()
    {
        if (_currentSerialDevice == null) return;

        try
        {
            AndroidJavaClass nmeaClass = new AndroidJavaClass("com.woncan.device.NMEA");

            // 你需要哪些就开哪些
            string[] nmeaTypes = { "GSV", "GSA", "GLL", "GMC", "VTG", "GGA" };

            foreach (string nmeaType in nmeaTypes)
            {
                AndroidJavaObject nmeaEnum = nmeaClass.GetStatic<AndroidJavaObject>(nmeaType);
                TryCall(_currentSerialDevice, "setNMEAEnable", nmeaEnum, true);
            }

            LogI("NMEA 数据已启用");
        }
        catch (Exception e)
        {
            Debug.LogError("[SerialDeviceSDKBridge] 启用 NMEA 失败：" + e);
        }
    }

    public void RegisterLocationListener()
    {
        if (_currentSerialDevice == null)
        {
            Debug.LogError("[SerialDeviceSDKBridge] RegisterLocationListener 失败：未找到设备/未连接");
            return;
        }

        try
        {
            var locationListener = new LocationListener(
                onReceiveLocation: wLocation =>
                {
                    var snapshot = ParseWLocation(wLocation);
                    LogI("Location: " + snapshot);
                    OnLocationReceived?.Invoke(snapshot);
                },
                onError: (code, msg) =>
                {
                    Debug.LogError($"[SerialDeviceSDKBridge] LocationListener error: {code}, {msg}");
                });

            TryCall(_currentSerialDevice, "registerLocationListener", locationListener);
            LogI("定位监听器注册成功");
        }
        catch (Exception e)
        {
            Debug.LogError("[SerialDeviceSDKBridge] 注册定位监听器失败：" + e);
        }
    }

    private class LocationListener : AndroidJavaProxy
    {
        private readonly Action<AndroidJavaObject> _onReceiveLocation;
        private readonly Action<int, string> _onError;

        public LocationListener(Action<AndroidJavaObject> onReceiveLocation, Action<int, string> onError)
            : base("com.woncan.device.listener.LocationListener")
        {
            _onReceiveLocation = onReceiveLocation;
            _onError = onError;
        }

        public void onReceiveLocation(AndroidJavaObject wLocation) => _onReceiveLocation?.Invoke(wLocation);
        public void onError(int errorCode, string errorMessage) => _onError?.Invoke(errorCode, errorMessage);
    }

    private LocationSnapshot ParseWLocation(AndroidJavaObject wLocation)
    {
        // 注意：WLocation 的字段/方法在不同版本可能不一样，这里做“getter 优先、field 兜底”
        var s = new LocationSnapshot();

        // fixStatus
        s.fixStatus = SafeCallInt(wLocation, "getFixStatus",
            fallbackFieldName: "fixStatus",
            fallbackValue: -1);

        // latitude / longitude（按常见命名兜底）
        s.latitude = SafeCallDouble(wLocation, "getLatitude",
            fallbackFieldName: "latitude",
            fallbackValue: 0);

        s.longitude = SafeCallDouble(wLocation, "getLongitude",
            fallbackFieldName: "longitude",
            fallbackValue: 0);

        // accuracy
        s.accuracy = SafeCallFloat(wLocation, "getAccuracy",
            fallbackFieldName: "accuracy",
            fallbackValue: 0);

        // time
        s.timeMillis = SafeCallLong(wLocation, "getTime",
            fallbackFieldName: "time",
            fallbackValue: 0);

        return s;
    }

    private bool EnsureAndroidReady()
    {
        if (!_isAndroid) return false;
        if (_unityContext == null || _scanManagerClass == null)
        {
            Debug.LogError("[SerialDeviceSDKBridge] Android 上下文或 ScanManager 未初始化（Start 未执行或初始化失败）");
            return false;
        }
        return true;
    }

    private void LogI(string msg)
    {
        if (VerboseLog) Debug.Log("[SerialDeviceSDKBridge] " + msg);
    }

    private void LogW(string msg)
    {
        if (VerboseLog) Debug.LogWarning("[SerialDeviceSDKBridge] " + msg);
    }

    private static void TryCall(AndroidJavaObject obj, string method, params object[] args)
    {
        if (obj == null) return;
        try
        {
            obj.Call(method, args);
        }
        catch (AndroidJavaException aje)
        {
            Debug.LogError($"[SerialDeviceSDKBridge] JNI Call 失败：{method}\n{aje}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SerialDeviceSDKBridge] Call 失败：{method}\n{e}");
        }
    }

    private static void TryCallStatic(AndroidJavaClass cls, string method, params object[] args)
    {
        if (cls == null) return;
        try
        {
            cls.CallStatic(method, args);
        }
        catch (AndroidJavaException aje)
        {
            Debug.LogError($"[SerialDeviceSDKBridge] JNI CallStatic 失败：{method}\n{aje}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SerialDeviceSDKBridge] CallStatic 失败：{method}\n{e}");
        }
    }

    private static T SafeCall<T>(AndroidJavaObject obj, string method, T fallback)
    {
        if (obj == null) return fallback;
        try { return obj.Call<T>(method); }
        catch { return fallback; }
    }

    private static int SafeCallInt(AndroidJavaObject obj, string getter, string fallbackFieldName, int fallbackValue)
    {
        if (obj == null) return fallbackValue;

        try { return obj.Call<int>(getter); }
        catch { /* ignore */ }

        try { return obj.Get<int>(fallbackFieldName); }
        catch { return fallbackValue; }
    }

    private static long SafeCallLong(AndroidJavaObject obj, string getter, string fallbackFieldName, long fallbackValue)
    {
        if (obj == null) return fallbackValue;

        try { return obj.Call<long>(getter); }
        catch { /* ignore */ }

        try { return obj.Get<long>(fallbackFieldName); }
        catch { return fallbackValue; }
    }

    private static float SafeCallFloat(AndroidJavaObject obj, string getter, string fallbackFieldName, float fallbackValue)
    {
        if (obj == null) return fallbackValue;

        try { return obj.Call<float>(getter); }
        catch { /* ignore */ }

        try { return obj.Get<float>(fallbackFieldName); }
        catch { return fallbackValue; }
    }

    private static double SafeCallDouble(AndroidJavaObject obj, string getter, string fallbackFieldName, double fallbackValue)
    {
        if (obj == null) return fallbackValue;

        try { return obj.Call<double>(getter); }
        catch { /* ignore */ }

        try { return obj.Get<double>(fallbackFieldName); }
        catch { return fallbackValue; }
    }

    private void Toast(string message)
    {
        if (!_isAndroid) return;

        try
        {
            AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
            AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", _unityContext, message, 0);
            toastObject.Call("show");
        }
        catch { /* ignore */ }
    }
}