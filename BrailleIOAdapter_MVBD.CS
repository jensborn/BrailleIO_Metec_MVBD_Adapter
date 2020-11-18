using BrailleIO;
using BrailleIO.Interface;
using BrailleIO.Structs;
using Metec.MVBDClient;
using MVBDAdapter.DeviceMapper;
using MVBDAdapter.MVBD;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;


namespace MVBDAdapter
{
    /// <summary>
    /// An Adapter using the metec MVBD (pin device visualizer) to wrap pin devices and 
    /// control them via TCP/IP connection.
    /// </summary>
    /// <seealso cref="BrailleIO.AbstractBrailleIOAdapterBase" />
    /// <seealso cref="System.IDisposable" />
    public class BrailleIOAdapter_MVBD : AbstractBrailleIOAdapterBase, IDisposable, IRotatable
    {
        #region Members

        /// <summary>
        /// Handles the MVBD server application.
        /// </summary>
        /// <value>
        /// The handler for MVBD.
        /// </value>
        internal MVBDHandler MVBD
        {
            get { return MVBDHandler.Instance; }
        }

        /// <summary>
        /// Gets or sets the mapper for mapping MVBD data to a special type of device.
        /// </summary>
        /// <value>
        /// The MVBD data mapper.
        /// </value>
        internal IMVBD_DeviceMapper Mapper { get; set; }

        #endregion

        #region Constructor / Destructor

        /// <summary>
        /// Initializes a new instance of the <see cref="BrailleIOAdapter_MVBD"/> class.
        /// </summary>
        /// <param name="manager">The adapter manager.</param>
        public BrailleIOAdapter_MVBD(IBrailleIOAdapterManager manager) : this(manager, null) { }
        /// <summary>
        /// Initializes a new instance of the <see cref="BrailleIOAdapter_MVBD"/> class.
        /// </summary>
        /// <param name="manager">The adapter manager.</param>
        /// <param name="mapper">The MVBD device mapper if exist or <c>null</c> for the first an initial one.</param>
        internal BrailleIOAdapter_MVBD(IBrailleIOAdapterManager manager, IMVBD_DeviceMapper mapper = null)
            : base(manager)
        {

            if (MVBD != null)
            {
                // only connect the first one to the MVBD events --> forward the others
                if (mapper == null)
                {
                    MVBD.NewDeviceConnected += MVBD_NewDeviceConnected;
                    MVBD.KeyDown += MVBD_KeyDown;
                    MVBD.KeyUp += MVBD_KeyUp;
                    MVBD.FingerChanged += MVBD_FingerChanged;
                }

                if (mapper != null)
                {
                    Mapper = mapper;
                    Device = Mapper.Device;
                }

                MVBD.StartMVBD();
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="BrailleIOAdapter_MVBD"/> class.
        /// </summary>
        ~BrailleIOAdapter_MVBD()
        {
            Dispose();
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        new public void Dispose()
        {
            if (MVBD != null)
            {
                MVBD.CloseMVBD();
            }
        }

        #endregion

        #region MVBD event forwarding

        /// <summary>
        /// List of devices and their related key mappers
        /// </summary>
        readonly Dictionary<IMVBD_DeviceMapper, BrailleIOAdapter_MVBD> devices = new Dictionary<IMVBD_DeviceMapper, BrailleIOAdapter_MVBD>();

        void MVBD_NewDeviceConnected(object sender, MVBDAdapter.MVBD.MVBDHandler.DeviceEventArgs e)
        {
            if (e != null && e.Mapper != null)
            {
                if (devices.ContainsKey(e.Mapper))
                {
                    // set the device as the main one (?)
                    manager.ActiveAdapter = devices[e.Mapper];
                }
                else
                {
                    BrailleIOAdapter_MVBD adapter = this;
                    if (this.Mapper != null) // create a new adapter if this one is already used for a device
                    {
                        adapter = new BrailleIOAdapter_MVBD(manager, e.Mapper);
                        // ((AbstractBrailleIOAdapterBase)adapter).Synch = true;
                        manager.AddAdapter(adapter);
                    }
                    else
                    {
                        this.Mapper = e.Mapper;
                        Device = Mapper.Device;
                    }
                    devices.Add(e.Mapper, adapter); // add an adapter?!

                    // set the device as the main one (?)
                    manager.ActiveAdapter = adapter;
                }
            }
        }

        /// <summary>
        /// Handles the KeyUp event of the MVBD control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MVBDHandler.KeyEventArgs"/> instance containing the event data.</param>
        internal virtual void MVBD_KeyUp(object sender, MVBDHandler.KeyEventArgs e)
        {
            if (e != null)
            {
                if (e.Mapper == this.Mapper)
                {
                    handleKeyUp(e);
                }
                else
                {
                    if (devices.ContainsKey(e.Mapper))
                    {
                        devices[e.Mapper].MVBD_KeyUp(this, e);
                    }
                    else
                    {
                        // create/register calling device
                        MVBD_NewDeviceConnected(this, new MVBDHandler.DeviceEventArgs(e.Mapper, e.Mapper.DeviceInfo));
                        Thread.Sleep(100);
                        // send the key event again for handling
                        if (sender != this) MVBD_KeyUp(this, e); // prevent stack overflow
                    }
                }
            }
        }

        /// <summary>
        /// Handles the KeyDown event of the MVBD control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MVBDHandler.KeyEventArgs"/> instance containing the event data.</param>
        internal virtual void MVBD_KeyDown(object sender, MVBDHandler.KeyEventArgs e)
        {
            if (e != null)
            {
                if (e.Mapper == this.Mapper)
                {
                    handleKeyDown(e);
                }
                else
                {
                    if (devices.ContainsKey(e.Mapper))
                    {
                        devices[e.Mapper].MVBD_KeyDown(this, e);
                    }
                    else
                    {
                        // create/register calling device
                        MVBD_NewDeviceConnected(this, new MVBDHandler.DeviceEventArgs(e.Mapper, e.Mapper.DeviceInfo));
                        Thread.Sleep(100);
                        // send the key event again for handling
                        if (sender != this) MVBD_KeyDown(this, e); // prevent stack overflow
                    }
                }
            }
        }


        /// <summary>
        /// Handles the FingerChanged event of the MVBD control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MVBDHandler.FingerEventArgs"/> instance containing the event data.</param>
        void MVBD_FingerChanged(object sender, MVBDHandler.FingerEventArgs e)
        {
            if (e != null)
            {
                if (e.Mapper == this.Mapper)
                {
                    handleFingerTouch(e.Finger);
                }
                else
                {
                    if (devices.ContainsKey(e.Mapper))
                    {
                        devices[e.Mapper].MVBD_FingerChanged(this, e);
                    }
                    else
                    {
                        // create/register calling device
                        MVBD_NewDeviceConnected(this, new MVBDHandler.DeviceEventArgs(e.Mapper, e.Mapper.DeviceInfo));
                        Thread.Sleep(100);
                        // send the key event again for handling
                        if (sender != this) MVBD_FingerChanged(this, e); // prevent stack overflow
                    }
                }
            }

        }

        #endregion

        #region Key Handling

        /// <summary>
        /// Handles the key up, builds device button states and fire the event to the framework.
        /// </summary>
        /// <param name="e">The <see cref="MVBDHandler.KeyEventArgs"/> instance containing the event data.</param>
        internal virtual void handleKeyUp(MVBDHandler.KeyEventArgs e)
        {
            if (e != null)
            {
                // get the button related state
                BrailleIO_DeviceButtonStates states = Utils.GetUpStateForDeviceButton(e.GeneralButton);
                OrderedDictionary raw = new OrderedDictionary();

                BrailleIO_BrailleKeyboardButtonStates kbStates = Utils.GetUpStateForDeviceButton(e.KeyboardButton);
                BrailleIO_AdditionalButtonStates[] addStates = null;
                if (e.AdditionalButtons != null && e.AdditionalButtons.Length > 0)
                {
                    addStates = new BrailleIO_AdditionalButtonStates[e.AdditionalButtons.Length];
                    for (int i = 0; i < addStates.Length; i++)
                    {
                        if (e.AdditionalButtons[i] != BrailleIO_AdditionalButton.None)
                        {
                            addStates[i] = Utils.GetUpStateForDeviceButton(e.AdditionalButtons[i]);
                        }
                    }
                }

                fireKeyStateChanged(states, ref raw, kbStates, addStates);
            }
        }

        /// <summary>
        /// Handles the key down, builds device button states and fire the event to the framework.
        /// </summary>
        /// <param name="e">The <see cref="MVBDHandler.KeyEventArgs"/> instance containing the event data.</param>
        internal virtual void handleKeyDown(MVBDHandler.KeyEventArgs e)
        {
            if (e != null)
            {
                // get the button related state
                BrailleIO_DeviceButtonStates states = Utils.GetDownStateForDeviceButton(e.GeneralButton);
                OrderedDictionary raw = new OrderedDictionary();

                BrailleIO_BrailleKeyboardButtonStates kbStates = Utils.GetDownStateForDeviceButton(e.KeyboardButton);
                BrailleIO_AdditionalButtonStates[] addStates = null;
                if (e.AdditionalButtons != null && e.AdditionalButtons.Length > 0)
                {
                    addStates = new BrailleIO_AdditionalButtonStates[e.AdditionalButtons.Length];
                    for (int i = 0; i < addStates.Length; i++)
                    {
                        if (e.AdditionalButtons[i] != BrailleIO_AdditionalButton.None)
                        {
                            addStates[i] = Utils.GetDownStateForDeviceButton(e.AdditionalButtons[i]);
                        }
                    }
                }

                fireKeyStateChanged(states, ref raw, kbStates, addStates);
            }
        }

        #endregion

        #region Touch

        #region TouchResetTimer
        /// <summary>
        /// The touch reset timeout in Milliseconds.
        /// After this time period, the touch values are rested to do not keep ghost touches.
        /// </summary>
        internal int TouchResetTimeoutMs = 10000;
        internal int fingerListCapacity = 30;
        Timer t = null;
        readonly object _tLock = new object();


        void resetTouchTimeoutTimer()
        {
            lock (_tLock)
            {
                if (t == null) startTouchResetTimer();
                else t.Change(0, Timeout.Infinite);
            }
        }

        private void startTouchResetTimer()
        {
            lock (_tLock)
            {
                if (t == null) t = new Timer(touchResetTimerElapsed, null, TouchResetTimeoutMs, Timeout.Infinite);
                else resetTouchTimeoutTimer();
            }
        }

        private void stopTouchResetTimer()
        {
            lock (_tLock)
            {
                if (t != null)
                {
                    t.Dispose();
                    t = null;
                }
            }
        }
        #endregion

        /// <summary>
        /// An empty dummy dictionary for sending events
        /// </summary>
        OrderedDictionary emptyRaw = new OrderedDictionary();
        /// <summary>
        /// The cache for the serializes sent finger/touch data
        /// </summary>
        Metec.MVBDClient.MVBDFinger[] fingerlist = new Metec.MVBDClient.MVBDFinger[30];

        /// <summary>
        /// Handles the finger touch changes. Builds th event data and firs the touch events to the framework.
        /// </summary>
        /// <param name="finger">The finger.</param>
        private void handleFingerTouch(Metec.MVBDClient.MVBDFinger finger)
        {
            stopTouchResetTimer();

            if (finger != null && finger.Index < fingerlist.Length)
            {
                if (finger.IsPressed)
                {
                    fingerlist[finger.Index] = finger;
                }
                else
                {
                    fingerlist[finger.Index] = null;
                }

                List<Touch> detailedTouches;
                double[,] touchMatrix = buildTouchEventData(fingerlist, out detailedTouches);
                fireTouchValuesChanged(touchMatrix, (int)DateTime.Now.Ticks, ref emptyRaw, detailedTouches);
            }
            startTouchResetTimer();
        }

        /// <summary>
        /// Builds the touch event data.
        /// </summary>
        /// <param name="fingerlist">The finger list.</param>
        /// <param name="detailedTouches">The detailed touches.</param>
        /// <returns></returns>
        private double[,] buildTouchEventData(Metec.MVBDClient.MVBDFinger[] fingerlist, out List<Touch> detailedTouches)
        {
            double[,] touchMatrix = new double[0, 0];
            detailedTouches = new List<Touch>();

            if (Mapper != null && Mapper.Device != null)
            {
                touchMatrix = new double[Mapper.Device.DeviceSizeY, Mapper.Device.DeviceSizeX];
                foreach (var finger in fingerlist)
                {
                    try
                    {
                        if (finger != null)
                        {
                            Touch t = Mapper.ConvertFingerToTouch(finger);
                            // Touch t = new Touch(finger.X, finger.Y, 1.0);
                            touchMatrix[
                                Math.Max(0, Math.Min(t.PinY, Mapper.Device.DeviceSizeY - 1)),
                                Math.Max(0, Math.Min(t.PinX, Mapper.Device.DeviceSizeX - 1))
                                ] = 1.0;
                            detailedTouches.Add(t);
                        }
                    }
                    catch { }
                }
            }
            return touchMatrix;
        }

        /// <summary>
        /// Callback if the reset timer elapsed - clears the touch list if some ghost touches already kept.
        /// </summary>
        /// <param name="state">The state.</param>
        private void touchResetTimerElapsed(Object state)
        {
            fingerlist = new Metec.MVBDClient.MVBDFinger[fingerListCapacity];

            // send empty touches for reset
            fireTouchValuesChanged(
                new double[this.Mapper.Device.DeviceSizeY, this.Mapper.Device.DeviceSizeX],
                DateTime.Now.Millisecond,
                ref emptyRaw,
                new List<Touch>());
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Connects this instance.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if connected successfully; otherwise, <c>false</c>.
        /// </returns>
        public override bool Connect()
        {
            return base.Connect();
        }

        /// <summary>
        /// Disconnects this instance.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if disconnected successfully; otherwise, <c>false</c>.
        /// </returns>
        public override bool Disconnect()
        {
            Dispose();

            return base.Disconnect();
        }

        DateTime _lastMVBDCheck = DateTime.Now;
        TimeSpan _mvbdCheckTime = new TimeSpan(0, 0, 30);

        /// <summary>
        /// Synchronizes the specified matrix.
        /// That means the Adapter try to sent the given Matrix to the real hardware
        /// device as an output.
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        public override void Synchronize(bool[,] matrix)
        {
            try
            {
                if (LockPins == false && MVBD != null)
                    if (MVBD.Connection != null && MVBD.Connection.IsConnected())
                    {
                        MVBD.Connection.SendPins(matrix, 0, 0, MVBD.Connection.PinCountX, MVBD.Connection.PinCountY);
                        if (DateTime.Now - _lastMVBDCheck > _mvbdCheckTime)
                        {
                            if (!MVBD.IsMVBDRunning()) { MVBD.StartMVBD(); }
                            _lastMVBDCheck = DateTime.Now;
                        }
                    }
                    else
                    {
                        MVBD.StartMVBD();
                    }
            }
            catch (Exception) { }
        }

        #endregion

        #region IRotatable

        /// <summary>
        /// Rotate the display to the right.
        /// </summary>
        /// <returns>
        /// The new position identifier.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Metec.MVBDClient.Position TurnRight()
        {
            if (Mapper != null && Mapper.DeviceInfo != null)
            {
                return Mapper.DeviceInfo.WorkingPosition = Mapper.DeviceInfo.WorkingPosition.Previous();
            }
            return Position.Front;
        }

        /// <summary>
        /// Rotate the display to the left.
        /// </summary>
        /// <returns>
        /// The new position identifier.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Metec.MVBDClient.Position TurnLeft()
        {
            if (Mapper != null && Mapper.DeviceInfo != null)
            {
                return Mapper.DeviceInfo.WorkingPosition = Mapper.DeviceInfo.WorkingPosition.Next();
            }
            return Position.Front;
        }

        /// <summary>
        /// Reset the rotate of the display to default.
        /// </summary>
        /// <returns>
        /// The new position identifier.
        /// </returns>
        public virtual Metec.MVBDClient.Position ResetTurn()
        {
            if (Mapper != null && Mapper.DeviceInfo != null)
            {
                return Mapper.DeviceInfo.WorkingPosition = Position.Front;
            }
            return Position.Front;
        }

        /// <summary>
        /// Rotate the display to a specialized position.
        /// </summary>
        /// <param name="p"></param>
        /// <returns>
        /// The new position identifier.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Metec.MVBDClient.Position SetPosition(Metec.MVBDClient.Position p)
        {
            if (Mapper != null && Mapper.DeviceInfo != null)
            {
                return Mapper.DeviceInfo.WorkingPosition = p;
            }
            return Position.Front;
        }

        /// <summary>
        /// Gets the current position.
        /// </summary>
        /// <returns>
        /// The current used position.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Position GetPosition()
        {
            if (Mapper != null && Mapper.DeviceInfo != null)
            {
                return Mapper.DeviceInfo.WorkingPosition;
            }
            return Position.Front;
        }

        #endregion
    }
}
