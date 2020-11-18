using BrailleIO.Interface;
using Metec.MVBDClient;
using MVBDAdapter.DeviceMapper;
using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static MVBDAdapter.MVBD.MVBDHandler;

namespace MVBDAdapter.MVBD
{
    /// <summary>
    /// Wrapper class to control the metec MVBD server application
    /// </summary>
    internal sealed class MVBDHandler
    {
        #region Members

        /// <summary>
        /// Flag for enabling debug output for MVBD codes and events on the std-out.
        /// </summary>
        internal bool DEBUG = true;

        /// <summary>
        /// The MVBD process.
        /// </summary>
        private Process _mvbdProcess = null;

        /// <summary>
        /// The MVBD process name.
        /// </summary>
        public const string MVBD_PROCESS_NAME = "MVBD";
        /// <summary>
        /// Gets or sets the TCP/IP port for Connection.
        /// </summary>
        /// <value>
        /// The port.
        /// </value>
        public int Port { get; set; }

        /// <summary>
        /// The list of connected devices.
        /// </summary>
        readonly ConcurrentDictionary<string, IMVBD_DeviceMapper> devices = new ConcurrentDictionary<string, IMVBD_DeviceMapper>();

        /// <summary>
        /// The MVBD TCP/IP Connection handler.
        /// </summary>
        MVBDConnection _connection = null;
        /// <summary>
        /// Gets the Connection to the MVBD application.
        /// </summary>
        /// <value>
        /// The Connection.
        /// </value>
        public MVBDConnection Connection { get { return _connection; } }


        /// <summary>
        /// The current active mapper
        /// </summary>
        IMVBD_DeviceMapper currentActiveMapper = null;

        bool _run = true;

        #endregion

        #region Singleton

        private static readonly Lazy<MVBDHandler> lazy = new Lazy<MVBDHandler>(() => new MVBDHandler());

        /// <summary>
        /// Gets the singleton instance for this MVBD controller class.
        /// </summary>
        /// <value>
        /// The singleton instance.
        /// </value>
        public static MVBDHandler Instance
        {
            get
            {
                try
                {
                    return lazy.Value;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        bool _mvbdWasRunningOnStartup = false;

        private MVBDHandler()
        {
            configureMVBD();
            // Check MVBD configuration before starting
            _mvbdWasRunningOnStartup = IsMVBDRunning();
            StartMVBD();

            System.Threading.Tasks.Task t = new Task(() =>
            {
                Thread.Sleep(2000);
                establishMVBDConnection();
                // Already done in connection
                //Thread.Sleep(200);
                //configureMVBDbyTCP();
            }
            );
            t.Start();

        }

        ~MVBDHandler()
        {
            CloseMVBD();
        }

        #endregion

        #region Events

        internal event EventHandler<DeviceEventArgs> NewDeviceConnected;
        internal event EventHandler<KeyEventArgs> KeyUp;
        internal event EventHandler<KeyEventArgs> KeyDown;
        internal event EventHandler<FingerEventArgs> FingerChanged;

        #region event throwing

        void throw_NewDeviceConnected(IMVBD_DeviceMapper _mapper, MVBDDeviceInfo _info)
        {
            if (NewDeviceConnected != null)
            {
                try
                {
                    NewDeviceConnected.Invoke(this, new DeviceEventArgs(_mapper, _info));
                }
                catch { }
            }
        }

        void throw_KeyUp(IMVBD_DeviceMapper _mapper, BrailleIO_DeviceButton general, BrailleIO_BrailleKeyboardButton keyboard, BrailleIO_AdditionalButton[] additional, String generic)
        {
            if (KeyUp != null)
            {
                try
                {
                    KeyUp.Invoke(this, new KeyEventArgs(_mapper, general, keyboard, additional, generic));
                }
                catch { }
            }
        }

        void throw_KeyDown(IMVBD_DeviceMapper _mapper, BrailleIO_DeviceButton general, BrailleIO_BrailleKeyboardButton keyboard, BrailleIO_AdditionalButton[] additional, String generic)
        {
            if (KeyDown != null)
            {
                try
                {
                    KeyDown.Invoke(this, new KeyEventArgs(_mapper, general, keyboard, additional, generic));
                }
                catch { }
            }
        }

        #endregion

        #endregion

        #region MVDB Process/Application Handling (START / STOP)

        private void configureMVBD()
        {
            // Load TCP/IP port from app.config
            var _portConfig = ConfigurationManager.AppSettings["MVBD_TCPIP_Port"];
            int _port = !String.IsNullOrEmpty(_portConfig) ? Int32.Parse(_portConfig) : 2018;
            Port = _port;
        }

        /// <summary>
        /// Gets the MVBD process.
        /// </summary>
        /// <returns>The fist found MVBD process or <c>null</c></returns>
        Process getMVBDProcess()
        {
            var pros = Process.GetProcessesByName(MVBD_PROCESS_NAME);
            if (pros != null && pros.Length > 0) return pros[0];
            return null;
        }

        /// <summary>
        /// Determines whether the MVBD application is running or not.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if is MVBD already running; otherwise, <c>false</c>.
        /// </returns>
        public bool IsMVBDRunning()
        {
            // check if process of MVBD is running
            return Process.GetProcessesByName(MVBD_PROCESS_NAME).Length > 0;
        }

        /// <summary>
        /// Starts the MVBD.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public bool StartMVBD(string path = "", String arguments = "")
        {
            if (IsMVBDRunning()) return true;


            System.Threading.Tasks.Task t = new System.Threading.Tasks.Task(
                () =>
                {
                    if (String.IsNullOrWhiteSpace(path))
                        path = getDefaultMVBDLocation();

                    try
                    {
                        if (!String.IsNullOrWhiteSpace(path))
                        {
                            // sniped by "sfuqua" Oct 27 '08 
                            //url:[https://stackoverflow.com/questions/240171/launching-an-application-exe-from-c/240610#240610]

                            // Prepare the process to run
                            ProcessStartInfo start = new ProcessStartInfo
                            {
                                // Enter in the command line arguments, everything you would enter after the executable name itself
                                Arguments = arguments,
                                // Enter the executable to run, including the complete path
                                FileName = path,
                                // Do you want to show a console window?
                                WindowStyle = ProcessWindowStyle.Hidden
                            };

                            _mvbdProcess = Process.Start(start);

                            Thread.Sleep(200);
                        }

                        int i = 0;
                        while (!IsMVBDRunning() && i++ < 10)
                        {
                            Thread.Sleep(100);
                        }
                    }
                    catch { }


                }
                );
            t.Start();
            t.Wait();


            return IsMVBDRunning();
        }

        /// <summary>
        /// Try Closes the MVBD application.
        /// </summary>
        /// <returns><c>true</c> if the MVBD was closed successfully; otherwise, <c>false</c>.</returns>
        public bool CloseMVBD()
        {
            _run = false;

            restoreOldConfig();

            if (!_mvbdWasRunningOnStartup)
            {

                if (this.Connection != null && this.Connection.IsConnected()) {
                    this.Connection.SendExit();
                    this.Connection.Close();
                    this.Connection.Dispose();
                }

                var process = getMVBDProcess();
                if (process != null)
                {
                    try
                    {
                        process.CloseMainWindow();
                        Thread.Sleep(10);
                        process.Kill();
                        process = null;
                    }
                    catch { }
                    finally
                    {
                        try { if (process != null) process.Kill(); }
                        catch { }
                    }
                }
            }
            return !IsMVBDRunning();
        }

        /// <summary>
        /// Gets the current DLL path.
        /// </summary>
        /// <returns>the path of the folder of this dll</returns>
        private static string getCurrentDllPath()
        {
            String path = String.Empty;
            path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return path;
        }

        /// <summary>
        /// Gets the default MVBD location in the MVBD folder of this extension.
        /// </summary>
        /// <returns>the execution path of the MVBD application</returns>
        static string getDefaultMVBDLocation()
        {
            return getCurrentDllPath() + "\\MVBD\\MVBD.exe";
        }

        /// <summary>
        /// Starts an TCP/IP Connection to the MVBD application to forward the events and connections
        /// </summary>
        private void establishMVBDConnection()
        {
            _run = true;
            if (Connection == null)
                _connection = new MVBDConnection(IPAddress.Loopback, Port);
            if (Connection != null)
            {
                Connection.Autoconnect = true;

                int i = 0;
                while (!Connection.IsConnected() && i++ < 5)
                {
                    Connection.Connect();
                    Thread.Sleep(200);
                }

                if (!Connection.IsConnected())
                {
                    int _port = 2018;
                    Port = _port;
                    Connection.Close();
                    CloseMVBD();
                    Thread.Sleep(200);
                    StartMVBD();
                    Thread.Sleep(200);
                    Connection.Connect();
                }

                // read the initial available device list
                if (Connection != null && Connection.IsConnected())
                {
                    _run = true;
                    Connection.SendGetDeviceTypes();
                        // .SendGetDeviceList();
                }
                else
                    Thread.Sleep(500);

                initMVBD_EventRegistration();

                configureMVBDbyTCP();

                // get the connected device information 
                var success = Connection.SendGetDeviceInfo();
                if (Connection.DeviceInfo != null)
                {
                    this.connection_DeviceInfoChanged(Connection, new MVBDDeviceInfoEventArgs(
                        Connection.DeviceInfo
                        ));
                }

            }
        }

        private void initMVBD_EventRegistration()
        {
            if (Connection != null)
            {
                // register to events
                Connection.DeviceInfoChanged += connection_DeviceInfoChanged;
                Connection.BrailleBytes += connection_BrailleBytes;
                Connection.FingerChanged += connection_FingerChanged;
                Connection.KeyDown += connection_KeyDown;
                Connection.KeyUp += connection_KeyUp;
                // Connection.NVDAGesture += connection_NVDAGesture;
                Connection.PinsChanged += connection_PinsChanged;
                //Connection.KeyboardKeyDown += connection_KeyboardKeyDown;
                //Connection.KeyboardKeyUp += connection_KeyboardKeyUp;
                //Connection.MouseMove += connection_MouseMove;
                Connection.PinsChanged += connection_PinsChanged;

                // configure the TCP routing matrix - so we can get events and can send data
                configureMVBD_TCP_Routing(Connection);
                // configure Event listening 
                SetNotificationMask();
            }
        }

        #endregion

        #region Event Notification Mask

        bool _nvdaPinsNotification = false;
        /// <summary>
        /// Receive or ignore notifications about the change of the pin from the NVDA output area.
        /// </summary>
        public bool EnableMVBD_NvdaPinsNotification
        {
            get { return _nvdaPinsNotification; }
            set
            {
                _nvdaPinsNotification = value;
                if (Connection != null) Connection.SendSetNotificationsMask(NotificationsMask.NVDAPins, _nvdaPinsNotification);
            }
        }

        bool _deviceInfoNotification = true;
        /// <summary>
        /// Receive or ignore notifications about changes of the currently used/connected/simulated hardware device.
        /// </summary>
        public bool EnableMVBD_DeviceInfoNotification
        {
            get { return _deviceInfoNotification; }
            set
            {
                _deviceInfoNotification = value;
                if (Connection != null) Connection.SendSetNotificationsMask(NotificationsMask.DeviceInfo, _deviceInfoNotification);
            }
        }

        bool _pinsNotification = false;
        /// <summary>
        /// Receive or ignore notifications about the change of the pin-states.
        /// </summary>
        public bool EnableMVBD_PinsNotification
        {
            get { return _pinsNotification; }
            set
            {
                _pinsNotification = value;
                if (Connection != null) Connection.SendSetNotificationsMask(NotificationsMask.Pins, _pinsNotification);
            }
        }

        bool _keyDownNotification = true;
        /// <summary>
        /// Receive or ignore notifications about pressed hardware keys.
        /// </summary>
        public bool EnableMVBD_KeyDownNotification
        {
            get { return _keyDownNotification; }
            set
            {
                _keyDownNotification = value;
                if (Connection != null) Connection.SendSetNotificationsMask(NotificationsMask.KeyDown, _keyDownNotification);
            }
        }

        bool _keyUpNotification = true;
        /// <summary>
        /// Receive or ignore notifications about released hardware keys.
        /// </summary>
        public bool EnableMVBD_KeyUpNotification
        {
            get { return _keyUpNotification; }
            set
            {
                _keyUpNotification = value;
                if (Connection != null) Connection.SendSetNotificationsMask(NotificationsMask.KeyUp, _keyUpNotification);
            }
        }

        bool _fingersNotification = true;
        /// <summary>
        /// Receive or ignore notifications about the change of touch-sensor date.
        /// </summary>
        public bool EnableMVBD_FingersNotification
        {
            get { return _fingersNotification; }
            set
            {
                _fingersNotification = value;
                if (Connection != null) Connection.SendSetNotificationsMask(NotificationsMask.Fingers, _fingersNotification);
            }
        }

        bool _nvdaGesturesNotification = false;
        /// <summary>
        /// Receive or ignore notifications about the occurrence of NVDA Gestures.
        /// </summary>
        public bool EnableMVBD_NvdaGesturesNotification
        {
            get { return _nvdaGesturesNotification; }
            set
            {
                _nvdaGesturesNotification = value;
                if (Connection != null) Connection.SendSetNotificationsMask(NotificationsMask.NVDAGestures, _nvdaGesturesNotification);
            }
        }

        bool _keyboardKeyDownNotification = false;
        /// <summary>
        /// Receive or ignore notifications about pressed standard-keyboard buttons.
        /// </summary>
        public bool EnableMVBD_KeyboardKeyDownNotification
        {
            get { return _keyboardKeyDownNotification; }
            set
            {
                _keyboardKeyDownNotification = value;
                if (Connection != null) Connection.SendSetNotificationsMask(NotificationsMask.KeyboardKeyDown, _keyboardKeyDownNotification);
            }
        }

        bool _keyboardKeyUpNotification = false;
        /// <summary>
        /// Receive or ignore notifications about released standard-keyboard buttons.
        /// </summary>
        public bool EnableMVBD_KeyboardKeyUpNotification
        {
            get { return _keyboardKeyUpNotification; }
            set
            {
                _keyboardKeyUpNotification = value;
                if (Connection != null) Connection.SendSetNotificationsMask(NotificationsMask.KeyboardKeyUp, _keyboardKeyUpNotification);
            }
        }

        bool _mouseMoveNotification = false;
        /// <summary>
        /// Receive or ignore notifications about changes in mouse position.
        /// </summary>
        public bool EnableMVBD_MouseMoveNotification
        {
            get { return _mouseMoveNotification; }
            set
            {
                _mouseMoveNotification = value;
                if (Connection != null) Connection.SendSetNotificationsMask(NotificationsMask.MouseMove, _mouseMoveNotification);
            }
        }

        /// <summary>
        /// Set the event notification mask for the MVBD. 
        /// The marked MVBD events are requested, others are ignored.
        /// </summary>
        public void SetNotificationMask()
        {

            NotificationsMask true_mask = 0;
            NotificationsMask false_mask = 0;

            if (EnableMVBD_NvdaPinsNotification) true_mask |= NotificationsMask.NVDAPins;
            else false_mask |= NotificationsMask.NVDAPins;

            if (EnableMVBD_DeviceInfoNotification) true_mask |= NotificationsMask.DeviceInfo;
            else false_mask |= NotificationsMask.DeviceInfo;

            if (EnableMVBD_PinsNotification) true_mask |= NotificationsMask.Pins;
            else false_mask |= NotificationsMask.Pins;

            if (EnableMVBD_KeyDownNotification) true_mask |= NotificationsMask.KeyDown;
            else false_mask |= NotificationsMask.KeyDown;

            if (EnableMVBD_KeyUpNotification) true_mask |= NotificationsMask.KeyUp;
            else false_mask |= NotificationsMask.KeyUp;

            if (EnableMVBD_FingersNotification) true_mask |= NotificationsMask.Fingers;
            else false_mask |= NotificationsMask.Fingers;

            if (EnableMVBD_NvdaGesturesNotification) true_mask |= NotificationsMask.NVDAGestures;
            else false_mask |= NotificationsMask.NVDAGestures;

            if (EnableMVBD_KeyboardKeyDownNotification) true_mask |= NotificationsMask.KeyboardKeyDown;
            else false_mask |= NotificationsMask.KeyboardKeyDown;

            if (EnableMVBD_KeyboardKeyUpNotification) true_mask |= NotificationsMask.KeyboardKeyUp;
            else false_mask |= NotificationsMask.KeyboardKeyUp;

            if (EnableMVBD_MouseMoveNotification) true_mask |= NotificationsMask.MouseMove;
            else false_mask |= NotificationsMask.MouseMove;

            if (Connection != null)
            {
                Connection.SendSetNotificationsMask(false_mask, false);
                Connection.SendSetNotificationsMask(true_mask, true);
                UpdateNotificationMask();
            }
        }

        /// <summary>
        /// Reads the currently set notification mask from the MVBD. 
        /// The mask defines which MVBD Notification events are forward or ignored.
        /// </summary>
        public void UpdateNotificationMask()
        {
            if (Connection != null)
            {
                Connection.SendGetNotificationsMask();
                var mask = Connection.NotificationsMask;

                if ((mask & NotificationsMask.DeviceInfo) > 0) _deviceInfoNotification = true;
                else _deviceInfoNotification = false;
                if ((mask & NotificationsMask.Fingers) > 0) _fingersNotification = true;
                else _fingersNotification = false;
                if ((mask & NotificationsMask.KeyboardKeyDown) > 0) _keyboardKeyDownNotification = true;
                else _keyboardKeyDownNotification = false;
                if ((mask & NotificationsMask.KeyboardKeyUp) > 0) _keyboardKeyUpNotification = true;
                else _keyboardKeyUpNotification = false;
                if ((mask & NotificationsMask.KeyDown) > 0) _keyDownNotification = true;
                else _keyDownNotification = false;
                if ((mask & NotificationsMask.KeyUp) > 0) _keyUpNotification = true;
                else _keyUpNotification = false;
                if ((mask & NotificationsMask.MouseMove) > 0) _mouseMoveNotification = true;
                else _mouseMoveNotification = false;
                if ((mask & NotificationsMask.NVDAGestures) > 0) _nvdaGesturesNotification = true;
                else _nvdaGesturesNotification = false;
                if ((mask & NotificationsMask.NVDAPins) > 0) _nvdaPinsNotification = true;
                else _nvdaPinsNotification = false;
                if ((mask & NotificationsMask.Pins) > 0) _pinsNotification = true;
                else _pinsNotification = false;
            }
        }

        #endregion

        #region Configure MVBD

        ConfigurationsMask _oldActiveConfigurationMask = ConfigurationsMask.None;
        ConfigurationsMask _oldDeactivatedConfigurationMask = ConfigurationsMask.None;

        private void configureMVBDbyTCP()
        {
            // get the configuration settings from start;
            GetMVBD_ConfigurationSettings(out _oldActiveConfigurationMask, out _oldDeactivatedConfigurationMask);
            UpdateMVBD_ConfigurationSettings();

            // change the configuration settings so they will fit the needs
            // SetMVBD_ConfigurationSettings();

            EnableMVBD_ConfigScreenCaptureInvert = false;
            EnableMVBD_ConfigScreenCaptureFollowFocus = false;
            EnableMVBD_ConfigScreenCaptureFollowMousepointer = false;
            EnableMVBD_ConfigScreenCaptureShowMousepointer = false;
            EnableMVBD_ConfigScreenCaptureFollowFinger = false;
            EnableMVBD_ConfigScreenCaptureControlWithFingers = false;
            EnableMVBD_ConfigDeviceKeyShortcutsActive = false;

            // TODO: check if necessary anymore to enable this feature
            EnableMVBD_ConfigTCPIPRooting = true;

            hideMVBD();
        }

        private void restoreOldConfig()
        {
            if(Connection != null && Connection.IsConnected() )
            {
                if (_oldActiveConfigurationMask != ConfigurationsMask.None) Connection.SendSetConfigurations(_oldActiveConfigurationMask, true);
                if (_oldDeactivatedConfigurationMask!= ConfigurationsMask.None) Connection.SendSetConfigurations(_oldDeactivatedConfigurationMask, false);
            }
        }

        #region MVBD TCP Routing

        /// <summary>
        /// Configures the MVBD TCP routing matrix.
        /// Set the input and output flags for unknown and MVBD
        /// </summary>
        /// <param name="Connection">The Connection.</param>
        private static void configureMVBD_TCP_Routing(MVBDConnection Connection)
        {
            if (Connection.IsConnected())
            {
                try
                {
                    Connection.SendGetTcpRoots();
                    bool[,,] tcpRoots = Connection.TcpRoots;

                    // forward all pins to
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Pins, (int)MVBD_RootIdentifier.Unknown, (int)MVBD_RootIdentifier.Unknown, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Pins, (int)MVBD_RootIdentifier.MVBD, (int)MVBD_RootIdentifier.Unknown, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Pins, (int)MVBD_RootIdentifier.MVBD, (int)MVBD_RootIdentifier.MVBD, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Pins, (int)MVBD_RootIdentifier.Unknown, (int)MVBD_RootIdentifier.MVBD, true);

                    // forward all keys to
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Keys, (int)MVBD_RootIdentifier.Unknown, (int)MVBD_RootIdentifier.Unknown, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Keys, (int)MVBD_RootIdentifier.MVBD, (int)MVBD_RootIdentifier.MVBD, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Keys, (int)MVBD_RootIdentifier.MVBD, (int)MVBD_RootIdentifier.Unknown, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Keys, (int)MVBD_RootIdentifier.Unknown, (int)MVBD_RootIdentifier.MVBD, true);

                    // forward all Touches to
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Fingers, (int)MVBD_RootIdentifier.Unknown, (int)MVBD_RootIdentifier.Unknown, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Fingers, (int)MVBD_RootIdentifier.MVBD, (int)MVBD_RootIdentifier.MVBD, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Fingers, (int)MVBD_RootIdentifier.MVBD, (int)MVBD_RootIdentifier.Unknown, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.Fingers, (int)MVBD_RootIdentifier.Unknown, (int)MVBD_RootIdentifier.MVBD, true);

                    // forward all Device connections to
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.DeviceInfo, (int)MVBD_RootIdentifier.Unknown, (int)MVBD_RootIdentifier.Unknown, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.DeviceInfo, (int)MVBD_RootIdentifier.MVBD, (int)MVBD_RootIdentifier.MVBD, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.DeviceInfo, (int)MVBD_RootIdentifier.MVBD, (int)MVBD_RootIdentifier.Unknown, true);
                    Connection.SendSetTcpRootsValue((int)MVBD_RootCommands.DeviceInfo, (int)MVBD_RootIdentifier.Unknown, (int)MVBD_RootIdentifier.MVBD, true);

                }
                catch { }
            }
        }

        /// <summary>
        /// Sets the TCP input routing matrix flags.
        /// </summary>
        /// <param name="tcpRoots">The TCP roots matrix.</param>
        /// <param name="command">The command (index 0).</param>
        /// <param name="id">The identifier the input identifier to set (index 2).</param>
        /// <param name="_self">if set to <c>true</c> the diagonal flags are set as well.</param>
        static void setTCPInputRouting(ref bool[,,] tcpRoots,
            MVBD_RootCommands command, MVBD_RootIdentifier id,
            bool _self = false)
        {
            if (tcpRoots != null)
            {
                for (int i = 0; i < tcpRoots.GetLength(2); i++)
                    if (!_self && i == (int)command) continue;
                    else tcpRoots[(int)command, (int)id, i] = true;
            }
        }

        /// <summary>
        /// Sets the TCP output routing matrix flags.
        /// </summary>
        /// <param name="tcpRoots">The TCP roots matrix.</param>
        /// <param name="command">The command (index 0).</param>
        /// <param name="id">The identifier the output identifier to set (index 1).</param>
        /// <param name="_self">if set to <c>true</c> the diagonal flags are set as well.</param>
        static void setTCPOuiputRouting(ref bool[,,] tcpRoots,
             MVBD_RootCommands command, MVBD_RootIdentifier id,
             bool _self = false)
        {
            if (tcpRoots != null)
            {
                for (int i = 0; i < tcpRoots.GetLength(1); i++)
                    if (!_self && i == (int)command) continue;
                    else tcpRoots[(int)command, i, (int)id] = true;
            }
        }

        #endregion

        #region Configurations

        bool _DeviceKeyShortcutsActiveConfig = false;
        /// <summary>
        /// Enables the device buttons to standard-keyboard virtual key injection
        /// </summary>
        public bool EnableMVBD_ConfigDeviceKeyShortcutsActive
        {
            get { return _DeviceKeyShortcutsActiveConfig; }
            set
            {
                _DeviceKeyShortcutsActiveConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.DeviceKeyShortcutsActive, _DeviceKeyShortcutsActiveConfig);
            }
        }
        bool _ScreenCaptureFollowMousepointerConfig = false;
        /// <summary>
        /// Enable that the focus follows the mouse pointer (only during screen capturing)
        /// </summary>
        public bool EnableMVBD_ConfigScreenCaptureFollowMousepointer
        {
            get { return _ScreenCaptureFollowMousepointerConfig; }
            set
            {
                _ScreenCaptureFollowMousepointerConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.ScreenCaptureFollowMousepointer, _ScreenCaptureFollowMousepointerConfig);
            }
        }
        bool _ScreenCaptureFollowFingerConfig = false;
        /// <summary>
        /// Enables that the focus follows the fingers (only during screen capturing)
        /// </summary>
        public bool EnableMVBD_ConfigScreenCaptureFollowFinger
        {
            get { return _ScreenCaptureFollowFingerConfig; }
            set
            {
                _ScreenCaptureFollowFingerConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.ScreenCaptureSpeakingFinger, _ScreenCaptureFollowFingerConfig);
            }
        }
        bool _ScreenCaptureFollowFocusConfig = false;
        /// <summary>
        /// Enable that the screen capturing follows UIA Focus (only during screen capturing)
        /// </summary>
        public bool EnableMVBD_ConfigScreenCaptureFollowFocus
        {
            get { return _ScreenCaptureFollowFocusConfig; }
            set
            {
                _ScreenCaptureFollowFocusConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.ScreenCaptureFollowFocus, _ScreenCaptureFollowFocusConfig);
            }
        }
        bool _ScreenCaptureInvertConfig = false;
        /// <summary>
        /// Enable inversion for all presented matrices
        /// </summary>
        public bool EnableMVBD_ConfigScreenCaptureInvert
        {
            get { return _ScreenCaptureInvertConfig; }
            set
            {
                _ScreenCaptureInvertConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.ScreenCaptureInvert, _ScreenCaptureInvertConfig);
            }
        }
        bool _ScreenCaptureShowMousepointerConfig = false;
        /// <summary>
        /// Enables the presentation of an animated tacton marking the cursor position
        /// </summary>
        public bool EnableMVBD_ConfigScreenCaptureShowMousepointer
        {
            get { return _ScreenCaptureShowMousepointerConfig; }
            set
            {
                _ScreenCaptureShowMousepointerConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.ScreenCaptureShowMousepointer, _ScreenCaptureShowMousepointerConfig);
            }
        }
        bool _ScreenCaptureControlWithKeysConfig = false;
        /// <summary>
        /// 
        /// </summary>
        public bool EnableMVBD_ConfigScreenCaptureControlWithKeys
        {
            get { return _ScreenCaptureControlWithKeysConfig; }
            set
            {
                _ScreenCaptureControlWithKeysConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.ScreenCaptureControlWithKeys, _ScreenCaptureControlWithKeysConfig);
            }
        }        
        bool _ScreenCaptureControlWithFingersConfig = false;
        /// <summary>
        /// Enable the control of the screen capturing via gestures 
        /// </summary>
        public bool EnableMVBD_ConfigScreenCaptureControlWithFingers
        {
            get { return _ScreenCaptureControlWithFingersConfig; }
            set
            {
                _ScreenCaptureControlWithFingersConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.ScreenCaptureFingersZoom, _ScreenCaptureControlWithFingersConfig);
            }
        }
        bool _speakKeysConfig = false;
        /// <summary>
        /// Enable the return of standard keyboard keys via TTS
        /// </summary>
        public bool EnableMVBD_ConfigSpeakKeys
        {
            get { return _speakKeysConfig; }
            set
            {
                _speakKeysConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.SpeakKeys, _speakKeysConfig);
            }
        }
        bool _speakElementsConfig = false;
        /// <summary>
        /// Enable the return of focused UIA elements via TTS
        /// </summary>
        public bool EnableMVBD_ConfigSpeakElements
        {
            get { return _speakElementsConfig; }
            set
            {
                _speakElementsConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.SpeakElements, _speakElementsConfig);
            }
        }
        bool _ShowBraillelineConfig = false;
        /// <summary>
        /// Enable the build in screen capturing of MVBD (deactivate this!!)
        /// </summary>
        public bool EnableMVBD_ConfigShowBrailleline
        {
            get { return _ShowBraillelineConfig; }
            set
            {
                _ShowBraillelineConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.ShowBrailleline, _ShowBraillelineConfig);
            }
        }
        bool _ShowElementInBraillelineConfig = false;
        /// <summary>
        /// Enable presentation of focused elements in the Screen reader-line
        /// </summary>
        public bool EnableMVBD_ConfigShowElementInBrailleline
        {
            get { return _ShowElementInBraillelineConfig; }
            set
            {
                _ShowElementInBraillelineConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.ShowElementInBrailleline, _ShowElementInBraillelineConfig);
            }
        }

        bool _tcpipRootingConfig = false;
        /// <summary>
        /// Enable presentation of focused elements in the Screen reader-line
        /// </summary>
        public bool EnableMVBD_ConfigTCPIPRooting
        {
            get { return _tcpipRootingConfig; }
            set
            {
                _tcpipRootingConfig = value;
                if (Connection != null) Connection.SendSetConfigurations(ConfigurationsMask.TcpRootingsActive, _tcpipRootingConfig);
            }
        }


        /// <summary>
        /// Get the currently set MVBD configuration mask.
        /// </summary>
        /// <param name="active">all currently active configuration settings</param>
        /// <param name="notActive">all known currently not active configuration settings (to restore them later)</param>
        public void GetMVBD_ConfigurationSettings(out ConfigurationsMask active, out ConfigurationsMask notActive)
        {
            ConfigurationsMask falseMask = ConfigurationsMask.None;
            ConfigurationsMask trueMask = ConfigurationsMask.None;

            if (Connection != null)
            {
                Connection.SendGetConfigurations();
                ConfigurationsMask config = (ConfigurationsMask)Connection.Configurations;

                if (config.HasFlag(ConfigurationsMask.ScreenCaptureFingersZoom)) trueMask |= ConfigurationsMask.ScreenCaptureFingersZoom;
                else falseMask |= ConfigurationsMask.ScreenCaptureFingersZoom;

                if (config.HasFlag(ConfigurationsMask.ScreenCaptureSpeakingFinger)) trueMask |= ConfigurationsMask.ScreenCaptureSpeakingFinger;
                else falseMask |= ConfigurationsMask.ScreenCaptureSpeakingFinger;

                if (config.HasFlag(ConfigurationsMask.ScreenCaptureFollowFocus)) trueMask |= ConfigurationsMask.ScreenCaptureFollowFocus;
                else falseMask |= ConfigurationsMask.ScreenCaptureFollowFocus;

                if (config.HasFlag(ConfigurationsMask.ScreenCaptureInvert)) trueMask |= ConfigurationsMask.ScreenCaptureInvert;
                else falseMask |= ConfigurationsMask.ScreenCaptureInvert;

                if (config.HasFlag(ConfigurationsMask.ScreenCaptureControlWithKeys)) trueMask |= ConfigurationsMask.ScreenCaptureControlWithKeys;
                else falseMask |= ConfigurationsMask.ScreenCaptureControlWithKeys;

                if (config.HasFlag(ConfigurationsMask.ScreenCaptureFollowMousepointer)) trueMask |= ConfigurationsMask.ScreenCaptureFollowMousepointer;
                else falseMask |= ConfigurationsMask.ScreenCaptureFollowMousepointer;

                if (config.HasFlag(ConfigurationsMask.ScreenCaptureShowMousepointer)) trueMask |= ConfigurationsMask.ScreenCaptureShowMousepointer;
                else falseMask |= ConfigurationsMask.ScreenCaptureShowMousepointer;

                if (config.HasFlag(ConfigurationsMask.PinOscilatorsActive)) trueMask |= ConfigurationsMask.PinOscilatorsActive;
                else falseMask |= ConfigurationsMask.PinOscilatorsActive;

                if (config.HasFlag(ConfigurationsMask.ScreenCaptureActive)) trueMask |= ConfigurationsMask.ScreenCaptureFingersZoom;
                else falseMask |= ConfigurationsMask.ScreenCaptureFingersZoom;

                if (config.HasFlag(ConfigurationsMask.DeviceKeyShortcutsActive)) trueMask |= ConfigurationsMask.DeviceKeyShortcutsActive;
                else falseMask |= ConfigurationsMask.DeviceKeyShortcutsActive;

                if (config.HasFlag(ConfigurationsMask.ShowElementInBrailleline)) trueMask |= ConfigurationsMask.ShowElementInBrailleline;
                else falseMask |= ConfigurationsMask.ShowElementInBrailleline;

                if (config.HasFlag(ConfigurationsMask.ShowBrailleline)) trueMask |= ConfigurationsMask.ShowBrailleline;
                else falseMask |= ConfigurationsMask.ShowBrailleline;

                if (config.HasFlag(ConfigurationsMask.SpeakElements)) trueMask |= ConfigurationsMask.SpeakElements;
                else falseMask |= ConfigurationsMask.SpeakElements;

                if (config.HasFlag(ConfigurationsMask.SpeakKeys)) trueMask |= ConfigurationsMask.SpeakKeys;
                else falseMask |= ConfigurationsMask.SpeakKeys;

                if (config.HasFlag(ConfigurationsMask.TcpRootingsActive)) trueMask |= ConfigurationsMask.TcpRootingsActive;
                else falseMask |= ConfigurationsMask.TcpRootingsActive;

            }
            active = trueMask;
            notActive = falseMask;
        }

        /// <summary>
        /// update the configuration fields with the currently set MVBD configuration.
        /// </summary>
        public void UpdateMVBD_ConfigurationSettings()
        {
            Connection.SendGetConfigurations();
            ConfigurationsMask config = (ConfigurationsMask)Connection.Configurations;

            _ScreenCaptureControlWithFingersConfig = config.HasFlag(ConfigurationsMask.ScreenCaptureFingersZoom);
            _ScreenCaptureFollowFingerConfig = config.HasFlag(ConfigurationsMask.ScreenCaptureSpeakingFinger);
            _ScreenCaptureFollowFocusConfig = config.HasFlag(ConfigurationsMask.ScreenCaptureFollowFocus);
            _ScreenCaptureInvertConfig = config.HasFlag(ConfigurationsMask.ScreenCaptureInvert);
            _ScreenCaptureControlWithKeysConfig = config.HasFlag(ConfigurationsMask.ScreenCaptureControlWithKeys);
            _ScreenCaptureFollowMousepointerConfig = config.HasFlag(ConfigurationsMask.ScreenCaptureFollowMousepointer);
            _ScreenCaptureShowMousepointerConfig = config.HasFlag(ConfigurationsMask.ScreenCaptureShowMousepointer);
            _DeviceKeyShortcutsActiveConfig = config.HasFlag(ConfigurationsMask.DeviceKeyShortcutsActive);
            _ShowElementInBraillelineConfig = config.HasFlag(ConfigurationsMask.ShowElementInBrailleline);
            _ShowBraillelineConfig = config.HasFlag(ConfigurationsMask.ShowBrailleline);
            _speakElementsConfig = config.HasFlag(ConfigurationsMask.SpeakElements);
            _speakKeysConfig = config.HasFlag(ConfigurationsMask.SpeakKeys);
            _tcpipRootingConfig = config.HasFlag(ConfigurationsMask.TcpRootingsActive);

        }


        /// <summary>
        /// set the mvbd configuration by the configuration fields
        /// </summary>
        public void SetMVBD_ConfigurationSettings()
        {
            ConfigurationsMask false_mask = ConfigurationsMask.PinOscilatorsActive |
                                            ConfigurationsMask.ScreenCaptureActive |
                                            ConfigurationsMask.ScreenCaptureControlWithKeys |
                                            ConfigurationsMask.PinPlayerActive;
            ConfigurationsMask true_mask = ConfigurationsMask.None;

            if (EnableMVBD_ConfigScreenCaptureControlWithFingers) true_mask |= ConfigurationsMask.ScreenCaptureFingersZoom; else false_mask |= ConfigurationsMask.ScreenCaptureFingersZoom;
            if (EnableMVBD_ConfigScreenCaptureFollowFinger) true_mask |= ConfigurationsMask.ScreenCaptureSpeakingFinger; else false_mask |= ConfigurationsMask.ScreenCaptureSpeakingFinger;
            if (EnableMVBD_ConfigScreenCaptureFollowFocus) true_mask |= ConfigurationsMask.ScreenCaptureFollowFocus; else false_mask |= ConfigurationsMask.ScreenCaptureFollowFocus;
            if (EnableMVBD_ConfigScreenCaptureInvert) true_mask |= ConfigurationsMask.ScreenCaptureInvert; else false_mask |= ConfigurationsMask.ScreenCaptureInvert;
            if (EnableMVBD_ConfigScreenCaptureControlWithKeys) true_mask |= ConfigurationsMask.ScreenCaptureControlWithKeys; else false_mask |= ConfigurationsMask.ScreenCaptureControlWithKeys;
            if (EnableMVBD_ConfigScreenCaptureFollowMousepointer) true_mask |= ConfigurationsMask.ScreenCaptureFollowMousepointer; else false_mask |= ConfigurationsMask.ScreenCaptureFollowMousepointer;
            if (EnableMVBD_ConfigScreenCaptureShowMousepointer) true_mask |= ConfigurationsMask.ScreenCaptureShowMousepointer; else false_mask |= ConfigurationsMask.ScreenCaptureShowMousepointer;
            if (EnableMVBD_ConfigDeviceKeyShortcutsActive) true_mask |= ConfigurationsMask.DeviceKeyShortcutsActive; else false_mask |= ConfigurationsMask.DeviceKeyShortcutsActive;
            if (EnableMVBD_ConfigShowElementInBrailleline) true_mask |= ConfigurationsMask.ShowElementInBrailleline; else false_mask |= ConfigurationsMask.ShowElementInBrailleline;
            if (EnableMVBD_ConfigShowBrailleline) true_mask |= ConfigurationsMask.ShowBrailleline; else false_mask |= ConfigurationsMask.ShowBrailleline;
            if (EnableMVBD_ConfigSpeakElements) true_mask |= ConfigurationsMask.SpeakElements; else false_mask |= ConfigurationsMask.SpeakElements;
            if (EnableMVBD_ConfigSpeakKeys) true_mask |= ConfigurationsMask.SpeakKeys; else false_mask |= ConfigurationsMask.SpeakKeys;
            if (EnableMVBD_ConfigTCPIPRooting) true_mask |= ConfigurationsMask.TcpRootingsActive; else false_mask |= ConfigurationsMask.TcpRootingsActive;

            SetMVBD_ConfigurationSettings(true_mask, false_mask);
        }

        /// <summary>
        /// Set the MVBD configuration by the given values to activate and deactivate.
        /// </summary>
        /// <param name="active">settings to be activated (first)</param>
        /// <param name="deactivated">settings to be deactivated (second)</param>
        public void SetMVBD_ConfigurationSettings(ConfigurationsMask active, ConfigurationsMask deactivated)
        {
            if (Connection != null)
            {
                Connection.SendSetConfigurations(active, true);
                Connection.SendSetConfigurations(deactivated, false);
            }
        }

        #endregion

        #region Main Window

        void hideMVBD()
        {
            if (Connection != null)
            {
                Connection.SendSetVisibility(Metec.MVBDClient.Visibility.NotifyIcon);
            }
        }

        #endregion

        #endregion

        #region MVBD Events

        void connection_DeviceInfoChanged(object sender, MVBDDeviceInfoEventArgs e)
        {
            if (e != null && e.Info != null)
            {
                if (DEBUG) System.Diagnostics.Debug.WriteLine("connection_DeviceInfoChanged: " + e.Info);

                string deviceID = getDeviceIDFromInfo(e.Info);

                if (devices.ContainsKey(deviceID))
                {
                    currentActiveMapper = devices[deviceID];
                    throw_NewDeviceConnected(currentActiveMapper, e.Info);
                }
                else
                {
                    // TODO: handle how this works if more than one device is connected
                    var mapper = MVBD_DeviceMapper_Factory.GetMVBD_DeviceMapper(e.Info);
                    if (mapper != null && !devices.ContainsKey(deviceID))
                    {
                        newDeviceConnected(mapper, e.Info);
                    }
                }
            }

        }

        void connection_PinsChanged(object sender, MVBDPinsEventArgs e)
        {
            // if (DEBUG) System.Diagnostics.Debug.WriteLine("connection_PinsChanged: " + e.Data);
        }

        void connection_NVDAGesture(object sender, MVBDNVDAGestureEventArgs e)
        {
            //  if (DEBUG) System.Diagnostics.Debug.WriteLine("connection_NVDAGesture: " + e.Data);
        }

        void connection_KeyUp(object sender, MVBDKeyEventArgs e)
        {
            if (DEBUG) System.Diagnostics.Debug.WriteLine("connection_KeyUp: " + e.Key);

            if (currentActiveMapper == null) connection_DeviceInfoChanged(sender, new MVBDDeviceInfoEventArgs(Connection.DeviceInfo));

            if (currentActiveMapper != null)
            {
                BrailleIO_DeviceButton genBtn;
                BrailleIO_AdditionalButton[] addBtn;
                BrailleIO_BrailleKeyboardButton kbBtn;
                string generic;
                var mapper = currentActiveMapper;
                mapper.ConvertToBioButton(e.Key, out genBtn, out kbBtn, out addBtn, out generic);
                throw_KeyUp(mapper, genBtn, kbBtn, addBtn, generic);
            }

        }

        void connection_KeyDown(object sender, MVBDKeyEventArgs e)
        {
            if (DEBUG) System.Diagnostics.Debug.WriteLine("connection_KeyDown: " + e.Key);

            if (currentActiveMapper == null) connection_DeviceInfoChanged(sender, new MVBDDeviceInfoEventArgs(Connection.DeviceInfo));

            if (currentActiveMapper != null)
            {
                BrailleIO_DeviceButton genBtn;
                BrailleIO_AdditionalButton[] addBtn;
                BrailleIO_BrailleKeyboardButton kbBtn;
                string generic;
                var mapper = currentActiveMapper;
                mapper.ConvertToBioButton(e.Key, out genBtn, out kbBtn, out addBtn, out generic);
                throw_KeyDown(mapper, genBtn, kbBtn, addBtn, generic);
            }
        }

        void connection_FingerChanged(object sender, MVBDFingerEventArgs e)
        {
            if (currentActiveMapper == null) connection_DeviceInfoChanged(sender, new MVBDDeviceInfoEventArgs(Connection.DeviceInfo));

            if (currentActiveMapper != null)
            {
                throw_FingerChanged(currentActiveMapper, e.Finger);
                // var mapper = currentActiveMapper;
                //Task t = new Task(() => {
                //    throw_FingerChanged(mapper, e.Finger);
                //});
                //t.Start();
            }
            if (DEBUG) System.Diagnostics.Debug.WriteLine("connection_FingerChanged: " + e.Finger);
        }

        private void throw_FingerChanged(IMVBD_DeviceMapper mapper, MVBDFinger mVBDFinger)
        {
            if (FingerChanged != null)
            {
                try
                {
                    _fingerStack.Enqueue(new FingerEventMapping(mapper, mVBDFinger));
                    var _t = touchThread;
                    if (_t != null && _t.ThreadState == System.Threading.ThreadState.Unstarted)
                        _t.Start();

                    // FingerChanged.DynamicInvoke(this, new FingerEventArgs(mapper, mVBDFinger));
                }
                catch (Exception) { }
            }
        }
                
        #region touch thread

        Thread _tt = null;
        Thread touchThread
        {
            get
            {
                if (_tt == null)
                {
                    _tt = new Thread(_sendFingerChanged);
                }
                return _tt;
            }
        }

        readonly ConcurrentQueue<FingerEventMapping> _fingerStack = new ConcurrentQueue<FingerEventMapping>();

        void _sendFingerChanged()
        {
            while (_run)
            {
                if (_fingerStack.IsEmpty)
                {
                    Thread.Sleep(5);
                }
                else
                {
                    FingerEventMapping f;
                    var success = _fingerStack.TryDequeue(out f);
                    if (success)
                    {
                        if (FingerChanged != null)
                        {
                            try
                            {
                                // if (DEBUG) System.Diagnostics.Debug.WriteLine("connection_FingerChanged: " + f.mVBDFinger);
                                FingerChanged.Invoke(this, new FingerEventArgs(f.mapper, f.mVBDFinger));
                            }
                            catch (Exception) { }
                        }
                    }
                }
            }
            _tt = null;
        }

        struct FingerEventMapping
        {
            public MVBDFinger mVBDFinger;
            public IMVBD_DeviceMapper mapper;

            public FingerEventMapping(IMVBD_DeviceMapper mapper, MVBDFinger mVBDFinger)
            {
                this.mapper = mapper;
                this.mVBDFinger = mVBDFinger;
            }
        }

        #endregion

        void connection_BrailleBytes(object sender, MVBDBrailleBytesEventArgs e)
        {
            if (DEBUG) System.Diagnostics.Debug.WriteLine("connection_BrailleBytes: " + e.Data);
        }

        #region NEW DEVICE

        void newDeviceConnected(IMVBD_DeviceMapper mapper, MVBDDeviceInfo info)
        {
            // add to the dictionary
            devices.AddOrUpdate(getDeviceIDFromInfo(info), mapper, (key, value) => value);
            currentActiveMapper = mapper;
            throw_NewDeviceConnected(mapper, info);
        }

        /// <summary>
        /// Gets the device identifier string from the device information.
        /// </summary>
        /// <param name="info">The device information.</param>
        /// <returns>an unique identifier string for the device based on its ID in combination with he current with and height.</returns>
        static string getDeviceIDFromInfo(MVBDDeviceInfo info)
        {
            string id = String.Empty;
            if (info != null)
            {
                id = info.ID.ToString();
                id += "_" + info.Width;
                id += "-" + info.Height;
            }
            return id;
        }

        #endregion

        #endregion

        #region enums

        /// <summary>
        /// The dimension 0 in the TCP Root matrix
        /// </summary>
        enum MVBD_RootCommands : int
        {
            /// <summary>
            /// Root all commands of pin data. Command: 1 (Braille-string), 21 (bool-array)
            /// </summary>
            Pins = 0,
            /// <summary>
            /// Root key data commands. Command: 22 (KeyDown), 23 (KeyUp)
            /// </summary>
            Keys = 1,
            /// <summary>
            /// Root finger data commands. Command: 24 (Touch)
            /// </summary>
            Fingers = 2,
            /// <summary>
            /// NVDA Gesture data commands. Command: 30 (NVDA Gesture)
            /// </summary>
            NVDA_Gestures = 3,
            /// <summary>
            /// The device has changed. Command: 20
            /// </summary>
            DeviceInfo = 4
        }

        /// <summary>
        /// In (receiver) and out (sender) parameters for TCP root matrix (index 1 and 2)
        /// </summary>
        enum MVBD_RootIdentifier
        {
            /// <summary>
            /// The type of client is unknown
            /// </summary>
            Unknown = 0,
            /// <summary>
            /// The MVBD itself
            /// </summary>
            MVBD = 1,
            /// <summary>
            /// It's the NVDA screenreader
            /// </summary>
            NVDA = 2,
            /// <summary>
            /// It's the GRANT application from University Potsdam
            /// </summary>
            GRANT = 3,
            /// <summary>
            /// The client is a Hyperbraille Geo (Geogebra) application by metec
            /// </summary>
            HyperBrailleGeo = 4,
            /// <summary>
            /// The client is a viewer monitor like a TV in a future to show a device in 3D
            /// </summary>
            Monitor = 5,
            /// <summary>
            /// The client is MATLAB from MathWorks
            /// </summary>
            MATLAB = 6,
            /// <summary>
            /// The client is Presentation from Neurobehavioral Systems
            /// </summary>
            Presentation = 7,
            /// <summary>
            /// The client is E-Prime from Psychology Software Tools
            /// </summary>
            EPrime = 8,
            /// <summary>
            /// Free for custom use
            /// </summary>
            Client9 = 9,
            /// <summary>
            /// Free for custom use
            /// </summary>
            Client10 = 10,
            /// <summary>
            /// Free for custom use
            /// </summary>
            Client11 = 11,
            /// <summary>
            /// Free for custom use
            /// </summary>
            Client12 = 12,
            /// <summary>
            /// Free for custom use
            /// </summary>
            Client13 = 13,
            /// <summary>
            /// Free for custom use
            /// </summary>
            Client14 = 14,
            /// <summary>
            /// Free for custom use
            /// </summary>
            Client15 = 15
        }

        #endregion

        #region EventArgs

        /// <summary>
        /// Event args for device related events
        /// </summary>
        /// <seealso cref="System.EventArgs" />
        internal class DeviceEventArgs : EventArgs
        {
            /// <summary>
            /// Gets or sets the corresponding key mapper for the sending device.
            /// </summary>
            /// <value>
            /// The key mapper.
            /// </value>
            public IMVBD_DeviceMapper Mapper { private set; get; }
            /// <summary>
            /// Gets or sets the device information struct.
            /// </summary>
            /// <value>
            /// The information.
            /// </value>
            public MVBDDeviceInfo Info { private set; get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="DeviceEventArgs"/> class.
            /// </summary>
            /// <param name="_mapper">The key mapper.</param>
            /// <param name="_info">The device information.</param>
            public DeviceEventArgs(IMVBD_DeviceMapper _mapper, MVBDDeviceInfo _info)
            {
                Mapper = _mapper;
                Info = _info;
            }
        }

        internal class KeyEventArgs : EventArgs
        {
            public IMVBD_DeviceMapper Mapper { get; private set; }
            public BrailleIO_DeviceButton GeneralButton { get; private set; }
            public BrailleIO_BrailleKeyboardButton KeyboardButton { get; private set; }
            public BrailleIO_AdditionalButton[] AdditionalButtons { get; private set; }
            public String GenericID { get; private set; }

            public KeyEventArgs(
                IMVBD_DeviceMapper _mapper,
                BrailleIO_DeviceButton _general,
                BrailleIO_BrailleKeyboardButton _keyboard,
                BrailleIO_AdditionalButton[] _additional,
                String _generic)
            {
                Mapper = _mapper;
                GeneralButton = _general;
                KeyboardButton = _keyboard;
                AdditionalButtons = _additional;
                GenericID = _generic;
            }
        }

        internal class FingerEventArgs : EventArgs
        {
            public IMVBD_DeviceMapper Mapper { get; private set; }
            public MVBDFinger Finger { get; private set; }

            public FingerEventArgs(IMVBD_DeviceMapper mapper, MVBDFinger mVBDFinger)
            {
                Mapper = mapper;
                Finger = mVBDFinger;
            }
        }


        #endregion

    }
}
