using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Metec.MVBDClient
{
    /// <summary>Connection client to the MVBD. Encoding is UTF8</summary>
    public class MVBDConnection : IDisposable
    {

        bool DEBUG = true;
        bool SPAM = false;

        /// <summary>Boolean array of pressed pc keyboard keys. Limited to 256 keys</summary>
        public bool[] KeyboardKeys;

        /// <summary>The count of horizontal pins</summary>
        public int PinCountX;

        /// <summary>The count of vertical pins</summary>
        public int PinCountY;

        /// <summary>Working or view position of the user at the device.</summary>
        public Position WorkingPosition;


        /// <summary>Connects automatically</summary>
        public bool Autoconnect;

        /// <summary>List all device types. Call SendGetDeviceTypes (cmd 26) to get it.</summary>
        public MVBDDeviceType[] DeviceTypes;

        /// <summary>List all virtual devices. Call SendGetBrailleDevices (cmd 58) to get it.</summary>
        public MVBDBrailleDevice[] BrailleDevices;


        /// <summary>Information of the current device in the MVBD. Call SendGetDeviceInfo (cmd 20) to get it manual.</summary>
        public MVBDDeviceInfo DeviceInfo;

        /// <summary>Array of the TcpRoots. Call SendGetTcpRoots (cmd 31) to get it.</summary>
        public bool[,,] TcpRoots;

        /// <summary>The version number of the MVBD. Call SendGetVersion (cmd 34) to get it.</summary>
        public int Version;

        /// <summary>The mask for notifications (events). Call SendGetNotificationsMask (cmd 56) to get it.</summary>
        public NotificationsMask NotificationsMask;

        /// <summary>32 bit values for the boolean configurations. Call SendGetConfigurations (cmd 72) to get it.</summary>
        public ConfigurationsMask Configurations;


        /// <summary>The current virtual device in the MVBD</summary>
        public MVBDVirtualDevice VirtualDevice;

        /// <summary>Graphic data to draw a computer keyboard</summary>
        public MVBDComputerKeyboard KeyboardGraphic;

        /// <summary>Graphic data to draw computer screens</summary>
        public byte[] ScreensGraphic;

        /// <summary>The Position of the mous pointer</summary>
        public Point MousePosition;

        protected IPEndPoint _ep;
        protected TcpClient _tcpClient;
        protected bool[] _received;
        protected Encoding _encoding;


        /// <summary>Creates a new connection to the MVBD with the standard port 2018</summary>
        public MVBDConnection(string ipString, int port) : this(IPAddress.Parse(ipString), 2018)
        {
        }

        /// <summary>Creates a new connection to the MVBD with the standard port 2018</summary>
        public MVBDConnection() : this(IPAddress.Loopback, 2018)
        {
        }

        /// <summary>Creates a new connection to the MVBD on a remote computer</summary>
        public MVBDConnection(IPAddress address, int port)
        {
            VirtualDevice = new MVBDVirtualDevice();
            KeyboardKeys = new bool[256];

            _encoding = Encoding.UTF8;
            _ep = new IPEndPoint(address, port);
            _received = new bool[256];
            Autoconnect = true;

            ThreadPool.QueueUserWorkItem(new WaitCallback(Thread_DoWork));
        }


        ~MVBDConnection()
        {
            Dispose();
        }

        public void Dispose()
        {
            Close();
        }

        /// <summary>Open the connection</summary>
        public void Connect()
        {
            Close();

            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = 500;
                _tcpClient.SendTimeout = 500;
                _tcpClient.Connect(_ep);
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
                Close();
            }

        }

        /// <summary>Check if the connection is alive</summary>
        public bool IsConnected()
        {
            if (_tcpClient == null) return false;   // -->

            Socket client = _tcpClient.Client;


            bool blocking = client.Blocking;

            try
            {
                client.Blocking = false;
                client.Send(new byte[1], 0, 0);
            }
            catch (SocketException)
            {
            }
            finally
            {
                client.Blocking = blocking;
            }

            return client.Connected;
        }


        /// <summary>Close the connection</summary>
        public void Close()
        {
            if (_tcpClient != null)
            {
                if (_tcpClient.Connected == true)
                {
                    _tcpClient.Close();
                }

                _tcpClient = null;
            }

        }

        /// <summary>Returns our ip and port when connected, else null</summary>
        public EndPoint LocalEndPoint
        {
            get
            {
                if ((_tcpClient != null) && (_tcpClient.Connected))
                {
                    return _tcpClient.Client.LocalEndPoint;
                }
                else
                {
                    return null;
                }
            }
        }



        /// <summary>Working thread</summary>
        protected void Thread_DoWork(object o)
        {
            while (true)
            {

                try
                {
                    // TryConnect
                    if (Autoconnect == true)
                    {
                        if (IsConnected() == false)
                        {
                            Connect();
                        }
                    }


                    if ((_tcpClient != null) && (_tcpClient.Connected == true))
                    {

                        if (_tcpClient.Available > 4)
                        {
                            NetworkStream ns = _tcpClient.GetStream();
                            int cmd = ns.ReadByte();
                            int len0 = ns.ReadByte();
                            int len1 = ns.ReadByte();
                            int len2 = ns.ReadByte();
                            int len = (len0 << 0) | (len1 << 8);       // | (len2 << 16);

                            byte[] ba = new byte[len];
                            ns.Read(ba, 0, len);

                            // Set received before On raise the UI event!
                            if ((cmd > 0) && (cmd < _received.Length))
                            {
                                _received[cmd] = true;
                            }

                            if (cmd == 1) { if (DEBUG) Debug.Print("-->  1 NVDA BrailleBytes"); ReceiveBrailleBytes(ba); }
                            else if (cmd == 20) { if (DEBUG) Debug.Print("--> 20 DeviceInfo"); ReceiveDeviceInfo(ba); }
                            else if (cmd == 21) { if (DEBUG && SPAM) Debug.Print("--> 21 Pins"); ReceivePins(ba); }
                            else if (cmd == 22) { if (DEBUG) Debug.Print("--> 22 KeyDown {0}", ba[0]); ReceiveKeyDown(ba[0]); }
                            else if (cmd == 23) { if (DEBUG) Debug.Print("--> 23 KeyUp   {0}", ba[0]); ReceiveKeyUp(ba[0]); }
                            else if (cmd == 24) { if (DEBUG) Debug.Print("--> 24 FingerChanged {0}", ba[0]); ReceiveFinger(ba); }
                            else if (cmd == 26) { if (DEBUG) Debug.Print("--> 26 DeviceTypes"); ReceiveDeviceTypes(ba); }
                            else if (cmd == 30) { if (DEBUG) Debug.Print("--> 30 NVDA Gesture"); ReceiveNVDAGesture(ba); }
                            else if (cmd == 31) { if (DEBUG) Debug.Print("--> 31 TcpRoots"); ReceiveTcpRoots(ba); }
                            else if (cmd == 34) { if (DEBUG) Debug.Print("--> 34 MVBDVersion"); ReceiveMVBDVersion(ba); }
                            else if (cmd == 52) { if (DEBUG) Debug.Print("--> 52 DeviceGraphic"); /*ReceiveDeviceGraphic(ba);*/ }
                            else if (cmd == 57) { if (DEBUG) Debug.Print("--> 57 NotificationsMask"); ReceiveNotificationsMask(ba); }
                            else if (cmd == 58) { if (DEBUG) Debug.Print("--> 58 BrailleDeviceList"); ReceiveBrailleDevices(ba); }
                            else if (cmd == 62) { if (DEBUG) Debug.Print("--> 62 Select (DataTable)"); /*ReceiveGetSelect(ba);*/ }
                            else if (cmd == 63) { if (DEBUG) Debug.Print("--> 63 Reflection"); ReceiveReflection(ba); }
                            else if (cmd == 64) { if (DEBUG) Debug.Print("--> 64 Screenshot");                /*ReceiveScreenshot(ba);*/   }

                            else if (cmd == 66) { if (DEBUG) Debug.Print("--> 66 Keyboard KeyDown", ba[0]); ReceiveKeyboardKeyDown(ba[0]); }
                            else if (cmd == 67) { if (DEBUG) Debug.Print("--> 67 Keyboard KeyUp", ba[0]); ReceiveKeyboardKeyUp(ba[0]); }
                            else if (cmd == 68) { if (DEBUG) Debug.Print("--> 68 MouseMove"); ReceiveMouseMove(ba); }

                            else if (cmd == 69) { if (DEBUG) Debug.Print("--> 69 Keyboard Graphic"); /*ReceiveKeyboardGraphic(ba);*/ }
                            else if (cmd == 70) { if (DEBUG) Debug.Print("--> 70 Screens Graphic"); /*ReceiveScreensGraphic(ba);*/ }
                            else if (cmd == 72) { if (DEBUG) Debug.Print("--> 72 Configurations"); ReceiveConfigurations(ba); }
                            else if (cmd == 74) { if (DEBUG) if (DEBUG) Debug.Print("--> 74 KeyShortcut"); ReceiveKeyShortcut(ba); }
                            else if (cmd == 75) { if (DEBUG) Debug.Print("--> 75 Bitmap"); /*ReceiveBitmap(ba);*/ }
                            else if (cmd == 76) { if (DEBUG) Debug.Print("--> 76 NumCells"); ReceiveNumCells(ba); }
                            else if (cmd == 77) { if (DEBUG) Debug.Print("--> 77 DebugMessage"); ReceiveDebugMessage(ba); }
                            else { if (DEBUG) Debug.Print("--> 00 Unknown Cmd={0} Len={1}", cmd, len); }

                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }

                }
                catch (Exception ex)
                {
                    Debug.Print(ex.Message);
                }


            }

        }

        #region EVENTS

        public event MVBDBrailleBytesEventHandler BrailleBytes;       // 01

        /// <summary>Received when the virtual device has changed</summary>
        public event MVBDDeviceInfoEventHandler DeviceInfoChanged;  // 20

        /// <summary>Received pins from the device (Cmd 21)</summary>
        public event MVBDPinsEventHandler PinsChanged;        // 21

        /// <summary>Received a key down from the device (Cmd 22)</summary>
        public event MVBDKeyEventHandler KeyDown;            // 22

        /// <summary>Received a key up from the device (Cmd 23)</summary>
        public event MVBDKeyEventHandler KeyUp;              // 23

        /// <summary>Received a finger touch from the device (Cmd 24)</summary>
        public event MVBDFingerEventHandler FingerChanged;      // 24

        public event MVBDNVDAGestureEventHandler NVDAGesture;        // 30


        /// <summary>Received a keyboard key down from the pc (Cmd 66)</summary>
        public event MVBDKeyEventHandler KeyboardKeyDown;            // 66

        /// <summary>Received a keyboard key up from the pc (Cmd 67)</summary>
        public event MVBDKeyEventHandler KeyboardKeyUp;              // 67

        /// <summary>Received a mouse move from the pc (Cmd 68)</summary>
        public event MVBDMouseMoveEventHandler MouseMove;              // 68

        /// <summary>Received a list of keys from the device (Cmd 74)</summary>
        public event MVBDKeyListEventHandler KeyShortcut;            // 74

        /// <summary>Received a command</summary>
        public event MVBDCommandEventHandler CommandReceived;        // xxx


        /// <summary>Send the event and check if InvokeRequired!</summary>
        private void RaiseEvent(Delegate d, object sender, EventArgs e)
        {
            if (d == null) return;  // -->

            object[] args = new object[] { sender, e };
            foreach (Delegate method in d.GetInvocationList())
            {
                method.DynamicInvoke(args);
            }

        }

        #endregion

        #region SENDING

        /// <summary>Sends a command without data and waits for a response. Returns false if there is a timeout.</summary>
        protected bool SendAndWait(int cmd)
        {
            return SendAndWait(cmd, null);
        }

        /// <summary>Sends a command with data and waits for a response. Returns false if there is a timeout.</summary>
        protected bool SendAndWait(int cmd, byte[] data)
        {
            _received[cmd] = false;

            Send(cmd, data);

            DateTime dtTimeout = DateTime.Now.AddSeconds(5);

            while (true)
            {
                if (DateTime.Now > dtTimeout) return false;     // --> Timeout
                if (_received[cmd] == true) return true;      // --> OK

                Thread.Sleep(100);
            }

        }


        /// <summary>Sends a command without data (no response). Returns true if it succeeds.</summary>
        protected bool Send(int cmd)
        {
            return Send(cmd, null);
        }


        /// <summary>Sends a command with data (no response). 'data' can be null. Returns true if it succeeds.</summary>
        protected bool Send(int cmd, byte[] data)
        {
            try
            {
                NetworkStream ns = _tcpClient.GetStream();

                ns.WriteByte((byte)cmd);                    // 00

                if (data == null)
                {
                    ns.WriteByte(0);     // 01 no data
                    ns.WriteByte(0);     // 02 no data
                }
                else
                {
                    ns.WriteByte((byte)(data.Length >> 0));     // 01 Length of data (low byte)
                    ns.WriteByte((byte)(data.Length >> 8));     // 02 Length of data (high byte)
                    ns.Write(data, 0, data.Length);           // 03... data
                }

                ns.Flush();
                return true;    // -->
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
                return false;   // -->
            }

        }

        #endregion

        #region COMMANDS (sending and receiving)

        #region #1 [ NVDA Braille Data ]

        /// <summary>Request 1. Send Pins like NVDA. Every byte is a braille character (1="a")</summary>
        public bool SendPinsAsNVDA(byte[] data)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 1 Send PinsAsNVDA {0}", data.Length);
            return Send(1, data);
        }

        /// <summary>Receive cmd=1 NVDA bytes</summary>
        protected void ReceiveBrailleBytes(byte[] ba)
        {
            RaiseEvent(BrailleBytes, this, new MVBDBrailleBytesEventArgs(ba));
        }

        #endregion 

        #region #20 [ Device Info ]

        /// <summary>Request 20. Sends a request to get information (id, resolution and orientation) about the current virtual device.</summary>
        public bool SendGetDeviceInfo()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 20 Send GetDeviceInfo");
            return SendAndWait(20);   // no parameters
        }

        /// <summary>Receive cmd=20. DeviceInfo received</summary>
        protected void ReceiveDeviceInfo(byte[] ba)
        {
            DeviceInfo = new MVBDDeviceInfo(ba);

            WorkingPosition = DeviceInfo.WorkingPosition;


            if ((WorkingPosition == Position.Front) || (WorkingPosition == Position.Rear))
            {
                PinCountX = DeviceInfo.Width;   // 76
                PinCountY = DeviceInfo.Height;  // 48
            }
            else
            {
                PinCountX = DeviceInfo.Height;  // 48
                PinCountY = DeviceInfo.Width;   // 76
            }


            RaiseEvent(DeviceInfoChanged, this, new MVBDDeviceInfoEventArgs(DeviceInfo));
        }

        #endregion

        #region #21 [ Pins ]

        /// <summary>Request 21. Send all pin data</summary>
        public bool SendPins()
        {
            return SendPins(VirtualDevice.Pins.Array, 0, 0, PinCountX, PinCountY);
        }

        /// <summary>Request 21. Send pin data</summary>
        public bool SendPins(bool[,] pins, int x, int y, int width, int height)
        {
            return SendPins(pins, new Rectangle(x, y, width, height));
        }

        /// <summary>Request 21. Send pin data. The boolean array is [x,y] !!!</summary>
        public bool SendPins(bool[,] pins, Rectangle rect)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->


            byte[] ba = new byte[4 + (rect.Width * rect.Height / 8) + 1]; // 4 + (104 * 60 / 8) + 1 = 785 Bytes data

            ba[0] = (byte)rect.Left;
            ba[1] = (byte)rect.Top;
            ba[2] = (byte)rect.Right;
            ba[3] = (byte)rect.Bottom;

            byte[] v2 = new byte[] { 1, 2, 4, 8, 16, 32, 64, 128 };

            int cnt = (4 << 3); // Start with index 4 (3 LSB and 5 MSB)

            for (int y = rect.Top; y < rect.Bottom; y++) // 0...59
            {
                for (int x = rect.Left; x < rect.Right; x++)    // 0...104
                {
                    if (pins[y, x] == true)
                    {
                        int i = (cnt >> 3);     // 4 4 4 4 4 4 4 4  5 5 5 5 5 5 5 5  6 6 6 6 6 6 6 6
                        int b = (cnt & 7);      // 0 1 2 3 4 5 6 7  0 1 2 3 4 5 6 7  0 1 2 3 4 5 6 7

                        ba[i] |= v2[b];
                    }

                    cnt++;
                }
            }

            if (DEBUG && SPAM) Debug.Print("<-- 21 Send Pins Len={0} Rect={1}", ba.Length, rect);
            return Send(21, ba);   // pins data as parameter
        }


        /// <summary>Receive cmd=21 Pindata</summary>
        protected void ReceivePins(byte[] ba)
        {
            Rectangle rect = Rectangle.FromLTRB(ba[0], ba[1], ba[2], ba[3]);

            //bool[,] pins = new bool[rect.Width, rect.Height];

            byte[] v2 = new byte[] { 1, 2, 4, 8, 16, 32, 64, 128 };

            int cnt = (4 << 3); // Start bei Index 4

            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    int i = (cnt >> 3);
                    int b = (cnt & 7);  // 0111

                    VirtualDevice.Pins[x, y] = ((ba[i] & v2[b]) != 0);
                    cnt++;
                }
            }

            RaiseEvent(PinsChanged, this, new MVBDPinsEventArgs(VirtualDevice.Pins.Array));
        }

        #endregion

        #region #22 [ Key Down ]

        /// <summary>Request 22. Send Key Down</summary>
        public bool SendKeyDown(int key)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            byte[] data = new byte[1];
            data[0] = (byte)key;

            if (DEBUG) Debug.Print("<-- 22 Send KeyDown");
            return Send(22, data); // 1 parameter
        }

        /// <summary>Receive cmd=22 Key down</summary>
        protected void ReceiveKeyDown(int key)
        {
            VirtualDevice.Keys[key].IsPressed = true;
            RaiseEvent(KeyDown, this, new MVBDKeyEventArgs(key));
        }

        #endregion

        #region #23 [ Key Up ]

        /// <summary>Request 23. Send Key Up</summary>
        public bool SendKeyUp(int key)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            byte[] data = new byte[1];
            data[0] = (byte)key;

            if (DEBUG) Debug.Print("<-- 23 Send KeyUp");
            return Send(23, data); // 1 parameter
        }

        /// <summary>Receive cmd=23 Key up</summary>
        protected void ReceiveKeyUp(int key)
        {
            VirtualDevice.Keys[key].IsPressed = false;
            RaiseEvent(KeyUp, this, new MVBDKeyEventArgs(key));
        }

        #endregion

        #region #24 [ Touch ]

        /// <summary>Request 24. Send Finger. index is number of finger (0-127)</summary>
        public bool SendFinger(int index, bool isPressed, int px, int py, int x, int y)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->


            byte[] ba = new byte[7];

            ba[0] = (byte)(index & 0x07F);          // Bit 0-6
            if (isPressed == true) ba[0] |= 128;  // Set Bit7

            ba[1] = (byte)(px);
            ba[2] = (byte)(py);

            ba[3] = (byte)(x >> 0);   // low byte
            ba[4] = (byte)(x >> 8);

            ba[5] = (byte)(y >> 0);   // low byte
            ba[6] = (byte)(y >> 8);

            if (DEBUG) Debug.Print("<-- 24 Send Finger");
            return Send(24, ba); // 1 parameter
        }

        /// <summary>Receive cmd=24 Finger</summary>
        protected void ReceiveFinger(byte[] ba)
        {
            int index = (ba[0] & 0x7F);         // Bit 0-6

            MVBDFinger finger = VirtualDevice.Fingers[index];
            finger.IsPressed = (ba[0] & 0x80) != 0;    // Bit 7
            finger.PX = ba[1];
            finger.PY = ba[2];
            finger.X = (ba[3] | ba[4] << 8);
            finger.Y = (ba[5] | ba[6] << 8);

            RaiseEvent(FingerChanged, this, new MVBDFingerEventArgs(finger));
        }

        #endregion

        #region #26 [ GET Device Types ]

        /// <summary>Request 26. Sends a request to get a list of all device types (models).</summary>
        public bool SendGetDeviceTypes()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 26 Send GetDeviceList");
            return SendAndWait(26);    // no parameters
        }

        /// <summary>Receive cmd=26. List of virtual devices received</summary>
        protected void ReceiveDeviceTypes(byte[] ba)
        {
            int pos = 0;
            int count = ba[pos++];         // 41

            DeviceTypes = new MVBDDeviceType[count];

            for (int i = 0; i < count; i++)
            {
                MVBDDeviceType t = new MVBDDeviceType();

                t.ID = (ba[pos++] | ba[pos++] << 8 | ba[pos++] << 16 | ba[pos++] << 24);    // 4106

                int len = ((ba[pos++] << 0) | (ba[pos++] << 8));
                string s = _encoding.GetString(ba, pos, len);
                pos += len;

                t.Name = s;   // "BD3"

                DeviceTypes[i] = t;
            }
        }

        #endregion

        #region #27 [ SET Device Type ]

        /// <summary>Request 27. Sets the type of the virtual devices. (id = 4 bytes)</summary>
        public bool SendSetDeviceType(int id)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            byte[] ba = new byte[4];
            ba[0] = (byte)(id >> 0);   // low byte
            ba[1] = (byte)(id >> 8);
            ba[2] = (byte)(id >> 16);
            ba[3] = (byte)(id >> 24);

            if (DEBUG) Debug.Print("<-- 27 Send SetDevice");
            return Send(27, ba);   // 1 parameter
        }

        #endregion

        #region #28 [ SET PMW Value ]

        /// <summary>Request 28. Sets the PWM Value. Only vor special devices! (value = 1 byte)</summary>
        public bool SendSetPWMValue(int value)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            byte[] ba = new byte[1];
            ba[0] = (byte)(value);

            if (DEBUG) Debug.Print("<-- 28 Send SetPWMValue");
            return Send(28, ba);
        }

        #endregion

        #region #29 [ SET ID ]

        /// <summary>Request 29. Send identifier. Who i am. 0=Unkwnown,1=MVBD,2=NVDA,3=GRANT,... (identifier = 1 byte)</summary>
        public bool SendSetIdentifier(int identifier)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            byte[] ba = new byte[1];
            ba[0] = (byte)identifier;   // 0 = Unkwnown, 1 = MVBD, 2 = NVDA, 3 = GRANT,...

            if (DEBUG) Debug.Print("<-- 29 Send SetIdentifier {0}", identifier);
            return Send(29, ba);
        }

        #endregion

        #region #30 [ NVDA Gesture ]

        /// <summary>Request 30. Send a NVDA Gesture to route the cursor to a position. (0,1,2,...)</summary>
        public bool SendNVDAGestureRouteTo(int routingIndex) { return SendNVDAGesture(0, routingIndex, 255, 255); }

        /// <summary>Request 30. Send a NVDA Gesture to scroll the braille line back</summary>
        public bool SendNVDAGestureScrollBack() { return SendNVDAGesture(1, 255, 255, 255); }

        /// <summary>Request 30. Send a NVDA Gesture to scroll the braille line forward</summary>
        public bool SendNVDAGestureScrollForward() { return SendNVDAGesture(2, 255, 255, 255); }

        /// <summary>Request 30. Send a NVDA Gesture (1=scroll back, 2=scroll forward, 64=say datetime,...)</summary>
        public bool SendNVDAGesture(int id) { return SendNVDAGesture(id, 255, 255, 255); }

        /// <summary>Request 30. Send NVDA Gesture. (4 bytes for the parameters). The Gesture have to be defined in the "MVBD.py" file</summary>
        public bool SendNVDAGesture(int id, int routingIndex, int dots, int space)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            byte[] ba = new byte[4];
            ba[0] = (byte)id;
            ba[1] = (byte)routingIndex;
            ba[2] = (byte)dots;
            ba[3] = (byte)space;

            if (DEBUG) Debug.Print("<-- 30 Send NVDAGesture");
            return Send(30, ba);
        }

        /// <summary>Receive cmd=30 NVDA gesture</summary>
        protected void ReceiveNVDAGesture(byte[] data)
        {
            RaiseEvent(NVDAGesture, this, new MVBDNVDAGestureEventArgs(data));
        }

        #endregion

        #region #32 [ GET TCP Roots ]

        /// <summary>Request 31. Sends a request to get a list (5x16x16 boolean array) of all tcp rootings.</summary>
        public bool SendGetTcpRoots()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 31 Send GetTcpRoots");
            return SendAndWait(31);
        }

        /// <summary>Receive cmd=31. List of TcpRoots received</summary>
        protected void ReceiveTcpRoots(byte[] ba)
        {
            //PrintByteArray(ba);

            int cmdCount = ba[0];     // 5
            int idCount = ba[1];     // 16


            TcpRoots = new bool[cmdCount, idCount, idCount];  // 5 x 16 x 16

            byte[] bv = new byte[] { 1, 2, 4, 8, 16, 32, 64, 128 };     // bitvalues
            int cnt = 0;                                     // counter for every bit

            // 5 x 16 x 16 bytes (cmd,in,out)
            for (int cmd = 0; cmd < cmdCount; cmd++)
            {
                for (int i = 0; i < idCount; i++)
                {
                    for (int o = 0; o < idCount; o++)
                    {

                        int index = cnt >> 3;   // Bit 3...         (0,1,2,3,4,5,6,7,8,...,159)
                        int bit = cnt & 7;    // Bit 0,1,2 in cnt (0,1,2,3,4,5,6,7)


                        if ((ba[2 + index] & bv[bit]) != 0)
                        {
                            TcpRoots[cmd, i, o] = true;
                        }

                        cnt++;
                    }
                }
            }

        }

        #endregion

        #region #32 [ SET TCP Roots ]

        /// <summary>Request 32. Send all values of the tcp rootings. It sends a 3D boolean array</summary>
        public bool SendSetTcpRoots()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->


            int cmdCount = TcpRoots.GetLength(0);      // 5
            int idCount = TcpRoots.GetLength(1);      // 16


            byte[] ba = new byte[2 + (cmdCount * idCount * idCount / 8)];   // 5 * 16 * 16 = 1280 bits / 8 = 160 Bytes + 2 = 162 Bytes



            byte[] bv = new byte[] { 1, 2, 4, 8, 16, 32, 64, 128 };     // bitvalues
            int cnt = 0;                                     // counter for every bit

            ba[0] = (byte)cmdCount;   // 00 (5)
            ba[1] = (byte)idCount;    // 01 (16)


            // 5 x 16 x 16 bytes (cmd,in,out)
            for (int cmd = 0; cmd < cmdCount; cmd++)
            {
                for (int i = 0; i < idCount; i++)
                {
                    for (int o = 0; o < idCount; o++)
                    {

                        int index = cnt >> 3;   // Bit 3...         (0,1,2,3,4,5,6,7,8,...,159)
                        int bit = cnt & 7;    // Bit 0,1,2 in cnt (0,1,2,3,4,5,6,7)


                        if (TcpRoots[cmd, i, o] == true)
                        {
                            ba[2 + index] |= bv[bit];
                        }

                        cnt++;
                    }
                }
            }

            if (DEBUG) Debug.Print("<-- 32 Send SetTcpRootsValue");
            return Send(32, ba);
        }

        #endregion

        #region #33 [ SET TCP Roots ]

        /// <summary>Request 33. Set one value in the tcp rootings to true or false. (4 bytes for the parameters)</summary>
        public bool SendSetTcpRootsValue(int cmd, int i, int o, bool value)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            byte[] ba = new byte[4];
            ba[0] = (byte)cmd;                // 0 = Pins, 1=Keys, 2=Fingers,...
            ba[1] = (byte)i;                  // 0=Unknown, 1=MVBD, 2=NVDA, 3=GRANT (in)
            ba[2] = (byte)o;                  // 0=Unknown, 1=MVBD, 2=NVDA, 3=GRANT (out)
            if (value == true) ba[3] = 1;   // 0 = false, 1 = true

            if (DEBUG) Debug.Print("<-- 33 Send SetTcpRootsValue");
            return Send(33, ba);
        }

        #endregion

        #region #34 [ MVBD Version ]

        /// <summary>Request 34. Sends a request to get the version of the MVBD. The response is a 4 byte number</summary>
        public bool SendGetVersion()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 34 Send GetVersion");
            return SendAndWait(34);
        }

        /// <summary>Receive cmd=34. MVBD Version received</summary>
        protected void ReceiveMVBDVersion(byte[] ba)
        {
            Version = ((ba[0] << 0) | (ba[1] << 8) | (ba[2] << 16) | (ba[3] << 24));
        }

        #endregion

        #region #35 [ Clear Pins ]

        /// <summary>Request 35. Reset/clear all Pins in the MVBD. (Call SendPinsFlush when finished)</summary>
        public bool SendPinsClear()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 35 Send PinsClear");
            return Send(35);
        }

        #endregion

        #region #36 [ Flush Pins ]

        /// <summary>Request 36. Flushes and shows the drawn pins in the MVBD. (First call Draw... then call Flush)</summary>
        public bool SendPinsFlush()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 36 Send PinsFlush");
            return Send(36);
        }

        #endregion

        #region #41 [ TTS ]

        /// <summary>Request 41. MVBD speakes a text (2 Bytes length in Unicode).</summary>
        public bool SendSpeakText(string text)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (text == null) text = "";                      // null = empty



            int count = (text.Length * 2);      // 6
            byte[] ba = new byte[2 + count];  // 6+2 = 8

            ba[0] = (byte)(count >> 0);
            ba[1] = (byte)(count >> 8);

            int written = Encoding.Unicode.GetBytes(text, 0, text.Length, ba, 2);     // byte 2...


            if (DEBUG) Debug.Print("<-- 41 Send SpeakText");
            return Send(41, ba);
        }

        #endregion

        #region #53 [ SET Visibility ]

        /// <summary>Request 53. Set the visibility of MVBD and Notify Icon</summary>
        /// <param name="visibility">0 = Show nothing, 1 = Show Mainwindow, 2 = Use Notify Icon</param>
        public bool SendSetVisibility(Visibility visibility)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            byte[] ba = new byte[1];
            ba[0] = (byte)visibility;   // 0,1,2

            if (DEBUG) Debug.Print("<-- 53 Send SetVisibility");
            return Send(53, ba);
        }

        #endregion

        #region #54 [ GET Pins ]

        /// <summary>Request 54. Request to get pins data</summary>
        public bool SendGetPins()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 54 Send GetPins");
            return Send(54);
        }

        #endregion

        #region #55 [ EXIT ]

        /// <summary>Request 55. Exit the MVBD</summary>
        public bool SendExit()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 55 Send Exit");
            return Send(55);
        }

        #endregion

        #region #56 [ SET Notification Mask ]

        /// <summary>Request 56. Set the notifikation mask. (Add Event or remove event)</summary>
        public bool SendSetNotificationsMask(NotificationsMask notifikationsmask, bool enabled)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->


            byte[] ba = new byte[5];

            uint mask = (uint)notifikationsmask;
            ba[0] = (byte)(mask >> 0);
            ba[1] = (byte)(mask >> 8);
            ba[2] = (byte)(mask >> 16);
            ba[3] = (byte)(mask >> 32);

            if (enabled == true) ba[4] = 1; else ba[4] = 0;

            if (DEBUG) Debug.Print("<-- 56 Send Set Notifications");
            return Send(56, ba);
        }

        #endregion

        #region #57 [ GET Notofication Mask ]

        /// <summary>Request 57. Get the notifikation mask. (Add Event or remove event)</summary>
        public bool SendGetNotificationsMask()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 57 Send Get Notifications");
            return SendAndWait(57);
        }

        /// <summary>Receive cmd=57 NotificationsMask</summary>
        protected void ReceiveNotificationsMask(byte[] ba)
        {
            NotificationsMask = (NotificationsMask)((ba[0] << 0) | (ba[1] << 8) | (ba[2] << 16) | (ba[3] << 24));
        }

        #endregion

        #region #58 [ List of hardware braille devices ]

        /// <summary>Request 58. Sends a request to get a list of all braille devices (real hardware devices)</summary>
        public bool SendGetBrailleDevices()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 58 Send GetBrailleDevices");
            return SendAndWait(58);    // no parameters
        }

        /// <summary>Receive cmd=58. List of hardware braille devices received</summary>
        protected void ReceiveBrailleDevices(byte[] ba)
        {
            int pos = 0;
            int count = ba[pos++];                              // 0

            BrailleDevices = new MVBDBrailleDevice[count];

            for (int i = 0; i < count; i++)
            {
                MVBDBrailleDevice bd = new MVBDBrailleDevice();

                int len = ((ba[pos++] << 0) | (ba[pos++] << 8));
                bd.ID = _encoding.GetString(ba, pos, len);     // 0.1 Unique key/ID
                pos += len;

                len = ((ba[pos++] << 0) | (ba[pos++] << 8));
                bd.Name = _encoding.GetString(ba, pos, len);    // 0.2 A name like Hyperflat
                pos += len;

                bd.Connected = (ba[pos++] != 0);            // 0.3 Is the device connected
                bd.Reconnect = (ba[pos++] != 0);            // 0.4 Connect automatically

                BrailleDevices[i] = bd;
            }
        }

        #endregion

        #region #63 [ Reflection ]

        private MVBDMemberInfo[] _members;

        /// <summary>Request 63. Get a Reflection</summary>
        public MVBDMemberInfo[] SendGetReflection(int ptr)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return null; // -->

            byte[] ba = new byte[2];
            ba[0] = (byte)(ptr >> 0);
            ba[1] = (byte)(ptr >> 8);

            if (DEBUG) Debug.Print("<-- 63 Send GetReflection");

            _members = null;
            SendAndWait(63, ba);

            return _members;
        }

        /// <summary>Receive cmd=63</summary>
        protected void ReceiveReflection(byte[] ba)
        {
            int pos = 0;

            // 1. Objekt das abgefragt wurde
            int ptr = ((ba[pos++] << 0) | (ba[pos++] << 8));

            // 2. Anzahl der Member
            int count = ((ba[pos++] << 0) | (ba[pos++] << 8));

            MVBDMemberInfo[] members = new MVBDMemberInfo[count];

            for (int i = 0; i < count; i++)
            {
                MVBDMemberInfo mi = new MVBDMemberInfo();

                // 2.1 MemberType (4=Field, 16=Property)
                mi.MemberType = (MemberTypes)ba[pos++];

                // 2.2 Name
                int len = ((ba[pos++] << 0) | (ba[pos++] << 8));
                mi.Name = _encoding.GetString(ba, pos, len);
                pos += len;

                // 2.3 IsStatic
                mi.IsStatic = (ba[pos++] != 0);

                // 2.4 ReturnType
                int rt = (ba[pos++]);

                if (rt == 1) mi.ReturnType = typeof(Byte);
                else if (rt == 2) mi.ReturnType = typeof(Int16);
                else if (rt == 3) mi.ReturnType = typeof(Int32);
                else if (rt == 4) mi.ReturnType = typeof(Int64);
                else if (rt == 5) mi.ReturnType = typeof(String);
                else if (rt == 6) mi.ReturnType = typeof(Boolean);
                else if (rt == 7) mi.ReturnType = typeof(DateTime);
                else if (rt == 88) mi.ReturnType = typeof(MVBDPtr);
                else System.Diagnostics.Debugger.Break();



                // 2.5 Value
                int vt = (ba[pos++]);

                if (vt == 66)
                {
                    len = ((ba[pos++] << 0) | (ba[pos++] << 8));
                    mi.ExceptionMessage = _encoding.GetString(ba, pos, len);
                    pos += len;
                }

                else if (vt == 0)
                {
                    mi.Value = null;
                }

                else if (vt == 1)
                {
                    mi.Value = ba[pos++];
                }

                else if (vt == 2)
                {
                    mi.Value = ((ba[pos++] << 0) | (ba[pos++] << 8));
                }

                else if (vt == 3)
                {
                    mi.Value = ((ba[pos++] << 0) | (ba[pos++] << 8) | (ba[pos++] << 16) | (ba[pos++] << 24));
                }

                else if (vt == 4)
                {
                    mi.Value = ((ba[pos++] << 0) | (ba[pos++] << 8) | (ba[pos++] << 16) | (ba[pos++] << 24) | (ba[pos++] << 32) | (ba[pos++] << 40) | (ba[pos++] << 48) | (ba[pos++] << 56));
                }

                else if (vt == 5)
                {
                    len = ((ba[pos++] << 0) | (ba[pos++] << 8));
                    mi.Value = _encoding.GetString(ba, pos, len);
                    pos += len;
                }

                else if (vt == 6)
                {
                    if (ba[pos++] == 0) mi.Value = false; else mi.Value = true;
                }

                else if (vt == 7)
                {
                    long ticks = ((ba[pos++] << 0) | (ba[pos++] << 8) | (ba[pos++] << 16) | (ba[pos++] << 24) | (ba[pos++] << 32) | (ba[pos++] << 40) | (ba[pos++] << 48) | (ba[pos++] << 56));
                    mi.Value = new DateTime(ticks);
                }

                else if (vt == 88)
                {
                    int value = ((ba[pos++] << 0) | (ba[pos++] << 8));
                    mi.Value = new MVBDPtr(value);

                    len = ((ba[pos++] << 0) | (ba[pos++] << 8));
                    mi.TypeName = _encoding.GetString(ba, pos, len);
                    pos += len;
                }
                else
                {
                    System.Diagnostics.Debugger.Break();
                }

                members[i] = mi;
            }

            _members = members;
        }

        #endregion

        #region #65 [ Moude Move ]

        /// <summary>Request 65. Send a mouse move</summary>
        public void SendMouseMove()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return; // -->




            int dx = 10;
            int dy = 10;
            byte[] ba = new byte[5];
            ba[0] = 3;
            ba[1] = (byte)(dx >> 0);
            ba[2] = (byte)(dx >> 8);
            ba[3] = (byte)(dy >> 0);
            ba[4] = (byte)(dy >> 8);


            if (DEBUG) Debug.Print("<-- 65 Send MouseMove");
            Send(65, ba);

        }

        #endregion

        #region #66 [ Keyboard Key Down ]

        /// <summary>Receive cmd=66 Keyboard Key down</summary>
        protected void ReceiveKeyboardKeyDown(int key)
        {
            KeyboardKeys[key] = true;
            RaiseEvent(KeyboardKeyDown, this, new MVBDKeyEventArgs(key));
        }

        #endregion

        #region #67 [ Keyboard Key Up ]

        /// <summary>Receive cmd=67 Keyboard Key up</summary>
        protected void ReceiveKeyboardKeyUp(int key)
        {
            KeyboardKeys[key] = false;
            RaiseEvent(KeyboardKeyUp, this, new MVBDKeyEventArgs(key));
        }

        #endregion

        #region #68 [ Mouse Move ]

        /// <summary>Receive cmd=68 Mouse Move</summary>
        protected void ReceiveMouseMove(byte[] ba)
        {
            int x = ((ba[0] << 0) | (ba[1] << 8));
            int y = ((ba[2] << 0) | (ba[3] << 8));

            MousePosition = new Point(x, y);
            RaiseEvent(MouseMove, this, new MVBDMouseMoveEventArgs(x, y));
        }

        #endregion

        #region #72 [ Configuration ]

        /// <summary>Request 72. Get the configurations (32 bit values)</summary>
        public bool SendGetConfigurations()
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->

            if (DEBUG) Debug.Print("<-- 72 Send Get Configurations");
            return SendAndWait(72);
        }

        /// <summary>Receive cmd=72 Configurations (32 bit values)</summary>
        protected void ReceiveConfigurations(byte[] ba)
        {
            Configurations = (ConfigurationsMask)((ba[0] << 0) | (ba[1] << 8) | (ba[2] << 16) | (ba[3] << 24));
        }

        #endregion

        #region #73 [ Set the Configuration Mask ]

        /// <summary>Request 73. Set the Configurations. The mask can be a combination of some configurations</summary>
        public bool SendSetConfigurations(ConfigurationsMask configurationsmask, bool enabled)
        {
            if ((_tcpClient == null) || (_tcpClient.Connected == false)) return false; // -->


            byte[] ba = new byte[5];

            uint mask = (uint)configurationsmask;
            ba[0] = (byte)(mask >> 0);
            ba[1] = (byte)(mask >> 8);
            ba[2] = (byte)(mask >> 16);
            ba[3] = (byte)(mask >> 32);

            if (enabled == true) ba[4] = 1; else ba[4] = 0;

            if (DEBUG) Debug.Print("<-- 73 Send Set Configurations");
            return Send(73, ba);
        }

        #endregion

        #region #74 [ Key Shortcut ]

        /// <summary>Receive cmd=74 Key Shortcut</summary>
        protected void ReceiveKeyShortcut(byte[] ba)
        {
            RaiseEvent(KeyShortcut, this, new MVBDKeyListEventArgs(ba));
        }

        #endregion

        #region #76 [ Number of Cells ]

        /// <summary>Receive cmd=76 Number of Cells</summary>
        protected void ReceiveNumCells(byte[] ba)
        {
            int numCells = ba[0];

            RaiseEvent(CommandReceived, this, new MVBDCommandEventArgs(76, numCells));
        }

        #endregion

        #region #77 [ DebugMessage ]

        /// <summary>Receive cmd=77 DebugMessage coded as UTF-8</summary>
        protected void ReceiveDebugMessage(byte[] ba)
        {
            string message = System.Text.Encoding.UTF8.GetString(ba);

            RaiseEvent(CommandReceived, this, new MVBDCommandEventArgs(77, message));
        }

        #endregion

        #endregion

    }

    #region DELEGATES & EVENTARGS

    public delegate void MVBDNVDAGestureEventHandler(object sender, MVBDNVDAGestureEventArgs e);
    public delegate void MVBDPinsEventHandler(object sender, MVBDPinsEventArgs e);
    public delegate void MVBDKeyEventHandler(object sender, MVBDKeyEventArgs e);
    public delegate void MVBDKeyListEventHandler(object sender, MVBDKeyListEventArgs e);
    public delegate void MVBDFingerEventHandler(object sender, MVBDFingerEventArgs e);
    public delegate void MVBDDeviceInfoEventHandler(object sender, MVBDDeviceInfoEventArgs e);
    public delegate void MVBDBrailleBytesEventHandler(object sender, MVBDBrailleBytesEventArgs e);
    public delegate void MVBDMouseMoveEventHandler(object sender, MVBDMouseMoveEventArgs e);
    public delegate void MVBDCommandEventHandler(object sender, MVBDCommandEventArgs e);



    public class MVBDNVDAGestureEventArgs : EventArgs
    {
        protected byte[] _data;

        public MVBDNVDAGestureEventArgs(byte[] data)
        {
            _data = data;
        }


        public byte[] Data
        {
            get
            {
                return _data;
            }
        }


        public override string ToString()
        {
            return String.Format("{0} {1} {2} {3}", _data[0], _data[1], _data[2], _data[3]);
        }

    }

    public class MVBDPinsEventArgs : EventArgs
    {
        protected bool[,] _pins;



        public MVBDPinsEventArgs(bool[,] pins)
        {
            _pins = pins;
        }


        public bool[,] Pins
        {
            get
            {
                return _pins;
            }
        }


        public override string ToString()
        {
            return String.Format("Width={0} Height={1}", _pins.GetLength(0), _pins.GetLength(1));
        }

    }

    public class MVBDKeyEventArgs : EventArgs
    {
        protected int _key;



        public MVBDKeyEventArgs(int key)
        {
            _key = key;
        }


        public int Key
        {
            get
            {
                return _key;
            }
        }


        public override string ToString()
        {
            return _key.ToString();
        }

    }

    public class MVBDKeyListEventArgs : EventArgs
    {
        protected byte[] _keys;


        public MVBDKeyListEventArgs(byte[] keys)
        {
            _keys = keys;
        }


        public byte[] Keys
        {
            get
            {
                return _keys;
            }
        }

    }

    public class MVBDFingerEventArgs : EventArgs
    {
        protected MVBDFinger _finger;

        public MVBDFingerEventArgs(MVBDFinger finger)
        {
            _finger = finger;
        }


        public MVBDFinger Finger
        {
            get
            {
                return _finger;
            }
        }


        public override string ToString()
        {
            return _finger.ToString();
        }

    }

    public class MVBDDeviceInfoEventArgs : EventArgs
    {
        protected MVBDDeviceInfo _info;

        public MVBDDeviceInfoEventArgs(MVBDDeviceInfo info)
        {
            _info = info;
        }


        public MVBDDeviceInfo Info
        {
            get
            {
                return _info;
            }
        }


        public override string ToString()
        {
            return _info.ToString();
        }

    }

    public class MVBDBrailleBytesEventArgs : EventArgs
    {
        protected byte[] _data;

        public MVBDBrailleBytesEventArgs(byte[] data)
        {
            _data = data;
        }


        public byte[] Data
        {
            get
            {
                return _data;
            }
        }


        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < _data.Length; i++)
            {
                sb.AppendFormat("{0:X2} ", _data[i]);
            }

            if (sb.Length != 0) sb.Length -= 1;  // Cut last space


            return sb.ToString();
        }

    }

    public class MVBDMouseMoveEventArgs : EventArgs
    {
        protected int _x;
        protected int _y;


        public MVBDMouseMoveEventArgs(int x, int y)
        {
            _x = x;
            _y = y;
        }

        /// <summary>The X position in screen coordinates</summary>
        public int X
        {
            get
            {
                return _x;
            }
        }

        /// <summary>The Y position in screen coordinates</summary>
        public int Y
        {
            get
            {
                return _y;
            }
        }



        public override string ToString()
        {
            return String.Format("{0},{1}", _x, _y);
        }

    }

    public class MVBDCommandEventArgs : EventArgs
    {
        protected int _cmd;
        protected object[] _args;


        public MVBDCommandEventArgs(int cmd, params object[] args)
        {
            _cmd = cmd;
            _args = args;
        }

        /// <summary>The number of the command</summary>
        public int Cmd
        {
            get
            {
                return _cmd;
            }
        }

        /// <summary>The data of the command</summary>
        public object[] Args
        {
            get
            {
                return _args;
            }
        }



        public override string ToString()
        {
            return String.Format("{0}", _cmd);
        }

    }

    #endregion

    #region SUBCLASSES

    public class MVBDDeviceInfo
    {
        public int Width;
        public int Height;
        public Position WorkingPosition;
        public int ID;


        /// <summary>Creates a new DeviceInfo from the command 20</summary>
        public MVBDDeviceInfo(byte[] data)
        {
            Width = data[0];
            Height = data[1];
            WorkingPosition = (Position)data[2];
            ID = (data[3] | data[4] << 8 | data[5] << 16 | data[6] << 24);
        }



        public override string ToString()
        {
            return String.Format("Width={0} Height={1} WorkingPosition={2} ID={3}", Width, Height, WorkingPosition, ID);
        }

    }

    /// <summary>A type of device (model)</summary>
    public class MVBDDeviceType
    {
        public int ID;
        public string Name;


        public override string ToString()
        {
            return String.Format("ID={0} Name={1}", ID, Name);
        }

    }

    /// <summary>A real hardware device</summary>
    public class MVBDBrailleDevice
    {
        public string ID;
        public string Name;
        public bool Connected;
        public bool Reconnect;

        public override string ToString()
        {
            return String.Format("ID={0} Name={1}", ID, Name);
        }
    }


    public class MVBDMemberInfo
    {
        public string Name;
        public object Value;
        public Type ReturnType;
        public string TypeName;
        public bool IsStatic;
        public string ExceptionMessage;

        /// <summary>4 = Field, 16=Property</summary>
        public MemberTypes MemberType;

        public MVBDMemberInfo()
        {
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class MVBDPtr
    {
        public static explicit operator int(MVBDPtr pt)
        {
            return pt.Value;
        }

        public static explicit operator MVBDPtr(int value)
        {
            return new MVBDPtr(value);
        }

        /// <summary>Returns a null pointer (0)</summary>
        public static MVBDPtr Zero = new MVBDPtr(0);

        /// <summary>Returns the point for the static root object Program (1)</summary>
        public static MVBDPtr Program = new MVBDPtr(1);


        public readonly int Value;

        public MVBDPtr(int value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    /// <summary>A pc keyboard</summary>
    public class MVBDComputerKeyboard
    {
        /// <summary>The width of the keyboard in pixel</summary>
        public readonly int Width;

        /// <summary>The height of the keyboard in pixel</summary>
        public readonly int Height;

        /// <summary>The keys on the keyboard</summary>
        public readonly MVBDComputerKey[] Keys;

        public MVBDComputerKeyboard(int width, int height, MVBDComputerKey[] keys)
        {
            Width = width;
            Height = height;
            Keys = keys;
        }

    }

    /// <summary>A key on a pc keyboard</summary>
    public class MVBDComputerKey
    {
        /// <summary>A unique number of the key</summary>
        public readonly int Index;

        /// <summary>The Virtual Key Code</summary>
        public readonly int KeyCode;

        /// <summary>The X position of the key on the keyboard</summary>
        public readonly int X;

        /// <summary>The Y position of the key on the keyboard</summary>
        public readonly int Y;

        /// <summary>The width of the key on the keyboard</summary>
        public readonly int Width;

        /// <summary>The height of the key on the keyboard</summary>
        public readonly int Height;

        /// <summary>The text on the key</summary>
        public readonly string Text;


        /// <summary>Is the key pressed (down)</summary>
        public bool IsPressed;



        public MVBDComputerKey(int index, int keyCode, int x, int y, int width, int height, string text)
        {
            Index = index;
            KeyCode = keyCode;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Text = text;
        }

        public override string ToString()
        {
            return String.Format("Index={0} KeyCode={1} Text={2}", Index, KeyCode, Text);
        }
    }

    /// <summary>A virtual device in the MVBD</summary>
    public class MVBDVirtualDevice
    {
        /// <summary>The width in pixel</summary>
        public int Width;

        /// <summary>The height in pixel</summary>
        public int Height;

        /// <summary>The depth in pixel for furuter use</summary>
        public int Depth;

        /// <summary>The color of the housing</summary>
        public Color Color;

        /// <summary>The color of the keys</summary>
        public Color KeysColor;

        /// <summary>The pins inside the device</summary>
        public MVBDPins Pins;

        /// <summary>The keys inside the device</summary>
        public MVBDDeviceKey[] Keys;

        /// <summary>The fingers on the device</summary>
        public MVBDFinger[] Fingers;

        /// <summary>Create a new unknown virtual device with pins, 256 keys and 10 fingers</summary>
        public MVBDVirtualDevice()
        {
            Pins = new MVBDPins();
            Keys = new MVBDDeviceKey[256];

            for (int i = 0; i < Keys.Length; i++)
            {
                Keys[i] = new MVBDDeviceKey(i);
            }



            Fingers = new MVBDFinger[10];
            for (int i = 0; i < Fingers.Length; i++)
            {
                Fingers[i] = new MVBDFinger(i);
            }
        }

    }

    /// <summary>The pins inside a virtual device</summary>
    public class MVBDPins
    {
        /// <summary>The x position of the pinarea on the device</summary>
        public int X;

        /// <summary>The y position of the pinarea on the device</summary>
        public int Y;

        /// <summary>The drawing color of the pins</summary>
        public Color Color;

        /// <summary>The distance of the pins for drawing</summary>
        public int Step;

        /// <summary>If it is a braille line draw a space betreen the cells</summary>
        public bool IsBrailleLine;

        /// <summary>The count of pins in x direction. The used pins in the PinArray</summary>
        public int CountX;

        /// <summary>The count of pins in x direction. The used pins in the PinArray</summary>
        public int CountY;


        /// <summary>Intern data array to store the values</summary>
        public bool[,] Array;




        /// <summary>Creates a new pin array</summary>
        public MVBDPins()
        {
            Array = new bool[256, 256];
        }

        /// <summary>Gets or sets the value of a pin</summary>
        public bool this[int x, int y]
        {
            get
            {
                return Array[x, y];
            }
            set
            {
                Array[x, y] = value;
            }
        }


        public override string ToString()
        {
            return String.Format("{0} x {1} pins", CountX, CountY);
        }
    }

    /// <summary>A key inside a virtual device</summary>
    public class MVBDDeviceKey
    {
        /// <summary>A unique number of the key</summary>
        public readonly int Index;

        /// <summary>Is it a circle or a rectange button</summary>
        public bool IsCircle;

        /// <summary>The X position of the key on the keyboard</summary>
        public int X;

        /// <summary>The Y position of the key on the keyboard</summary>
        public int Y;

        /// <summary>The width of the key on the keyboard</summary>
        public int Width;

        /// <summary>The height of the key on the keyboard</summary>
        public int Height;

        /// <summary>Is the key used in this device</summary>
        public bool Visible;


        /// <summary>Is the key pressed (down)</summary>
        public bool IsPressed;


        public MVBDDeviceKey(int index)
        {
            Index = index;
        }

        public override string ToString()
        {
            return String.Format("Index={0}", Index);
        }
    }

    /// <summary>A finger on the virtual device</summary>
    public class MVBDFinger
    {
        /// <summary>Number of the fingers (0-9)</summary>
        public readonly int Index;

        /// <summary>X Position in Pins ( 0 to Width - 1 )</summary>
        public int PX;

        /// <summary>The Y position in pins units ( 0 to Width - 1 )</summary>
        public int PY;


        /// <summary>The high resolution X-position</summary>
        public int X;

        /// <summary>The high resolution Y-position</summary>
        public int Y;



        /// <summary>Has this finger contact or is it gone</summary>
        public bool IsPressed;




        /// <summary>Creates a new finger</summary>
        public MVBDFinger(int index)
        {
            this.Index = index;
        }

        /// <summary>Informationen about this finger</summary>
        public override string ToString()
        {
            return String.Format("Index={0,-2} IsPressed={1,-5} PX={2,-4} PY={3,-5}   X={4,-4} Y={5,-5}", Index, IsPressed, PX, PY, X, Y);
        }
    }

    #endregion

    #region ENUMS

    /// <summary>Position for working and viewing. Where is the user at the device.</summary>
    public enum Position
    {
        /// <summary>0 = The user works at the front of the device.</summary>
        Front = 0,

        /// <summary>1 = The user works at the right site of the device.</summary>
        Right = 1,

        /// <summary>2 = The user works at the backsite of the device. (USB connector)</summary>
        Rear = 2,

        /// <summary>3 = The user works at the left site of the device.</summary>
        Left = 3
    }

    /// <summary>The visibility variants of the MVBD (Used in command 53)</summary>
    public enum Visibility
    {
        /// <summary>0 = The MVBD is invisible</summary>
        Hidden = 0,

        /// <summary>1 = The MVBD Main Window is visible</summary>
        Window = 1,

        /// <summary>2 = The MVBD is used with the Notify Icon</summary>
        NotifyIcon = 2,
    }

    /// <summary>32 Bit-Mask for EventIDs to get notifications from the MVBD. Combinations are possible (Used in command 56)</summary>
    public enum NotificationsMask : uint
    {
        /// <summary>0x01 (1) Bit 0 = Receive NVDA braille coded pin data by command 1</summary>
        NVDAPins = 0x0001,

        /// <summary>0x02 (2) Bit 1 = Receive device changes by command 20</summary>
        DeviceInfo = 0x0002,

        /// <summary>0x04 (4) Bit2 = Receive pin data by command 21</summary>
        Pins = 0x0004,

        /// <summary>0x08 (8) Bit 3 = Receive Key-Down notifications  by command 22</summary>
        KeyDown = 0x0008,

        /// <summary>0x10 (16) Bit 4 = Receive Key-Up notifications by command 23</summary>
        KeyUp = 0x0010,

        /// <summary>0x20 (32) Bit 5 = Receive touch notifications of the fingers by command 24</summary>
        Fingers = 0x0020,

        /// <summary>0x40 (64) Bit 6 = Receive NVDA Gestures by command 30</summary>
        NVDAGestures = 0x0040,

        /// <summary>0x80 (128) Bit 7 = Receive Keyboard Key-Down notifications  by command 66</summary>
        KeyboardKeyDown = 0x0080,

        /// <summary>0x100 (256) Bit 8 = Receive Keyboard Key-Up notifications by command 67</summary>
        KeyboardKeyUp = 0x0100,

        /// <summary>0x200 (512) Bit 9 = Receive MouseMove notifications by command 68</summary>
        MouseMove = 0x0200,

        /// <summary>0x400 (1024) Bit 10 = Receive Key-Shortcut notifications by command XXX</summary>
        KeyShortcut = 0x0400,

        /// <summary>0x800 (2048) Bit 11 = Receive Bitmap (OCR Service) notifications by command XXX</summary>
        Bitmap = 0x0800,

        /// <summary>0x1000 (4096) Bit 12 = Receive Debug Messages</summary>
        DebugMessages = 0x1000,
    }

    /// <summary>32 Bit-Mask for configurations in the MVBD. Combinations are possible (Used in command 72 and 73)</summary>
    public enum ConfigurationsMask : uint
    {
        /// <summary>
        ///  Default configuration - nothing is activated
        /// </summary>
        None = 0x0000,

        /// <summary>0x01 (1) Bit 0 = The device key shortcuts inside the MVBD are active. To create your own shortcuts set this to false.</summary>
        DeviceKeyShortcutsActive = 0x0001,

        /// <summary>0x02 (2) Bit 1 = The MVBD intern screencapture (copy pixel) is active</summary>
        ScreenCaptureActive = 0x0002,

        /// <summary>0x04 (4) Bit2 = The screen capture follows the mouse</summary>
        ScreenCaptureFollowMousepointer = 0x0004,

        /// <summary>0x08 (8) Bit 3 = The screen capture follows the focus element</summary>
        ScreenCaptureFollowFocus = 0x0008,



        /// <summary>0x10 (16) Bit 4 = Speak the element under the first finger</summary>
        ScreenCaptureSpeakingFinger = 0x0010,

        /// <summary>0x20 (32) Bit 5 = The pins in the screen capture are shown inverted</summary>
        ScreenCaptureInvert = 0x0020,

        /// <summary>0x40 (64) Bit 6 = The screen capture shows a blinking mousepointer</summary>
        ScreenCaptureShowMousepointer = 0x0040,

        /// <summary>0x80 (128) Bit 7 = The screen capture will be controlled by hardcoded device key commands. (The cursor keys controlling the mouse)</summary>
        ScreenCaptureControlWithKeys = 0x0080,



        /// <summary>0x100 (256) Bit 8 = Zoom with 2 fingers</summary>
        ScreenCaptureFingersZoom = 0x0100,

        /// <summary>0x200 (512) Bit 9 = Speak computer keyboard keys (H...A...L...L...O...ENTER)</summary>
        SpeakKeys = 0x0200,

        /// <summary>0x400 (1024) Bit 10 = Speak the UIA elements when follow the mouse or the focus</summary>
        SpeakElements = 0x0400,

        /// <summary>0x800 (2048) Bit 11 = Show braille text at the bottom of the pins</summary>
        ShowBrailleline = 0x0800,



        /// <summary>0x1000 Bit 12 = Show a simple text of the active UIA in the brailleline</summary>
        ShowElementInBrailleline = 0x1000,

        /// <summary>0x2000 Bit 13 = Activates the PinOscilators. PinOscilators are made for MRT/MEG customers to control single braille cells.</summary>
        PinOscilatorsActive = 0x2000,

        /// <summary>0x4000 Bit 14 = Is the pin player active in the MVBD. Set this to false when you want to send your own pin patterns. New in version 130</summary>
        PinPlayerActive = 0x4000,

        /// <summary>0x8000 Bit 15 = Are the Tcp-IP client rootings active. When true the events are controlled by the MVBD. When false every client has to set the events by itself! New in version 130</summary>
        TcpRootingsActive = 0x8000,


        /// <summary>0x010000 Bit 16 = Move with 2 fingers</summary>
        ScreenCaptureFingersMove = 0x010000,

        /// <summary>0x020000 Bit 17 = 1 finger can move the mousepointer</summary>
        ScreenCaptureFingerMovesMousepointer = 0x020000,

        /// <summary>0x040000 Bit 18 = 1 finger on the edge scrolls the view</summary>
        ScreenCaptureFingerOnEdgeScrollsTheView = 0x040000,

        /// <summary>0x080000 Bit 19 = 1 finger can click when double tap</summary>
        ScreenCaptureFingerCanClick = 0x080000,

    }

    #endregion

}