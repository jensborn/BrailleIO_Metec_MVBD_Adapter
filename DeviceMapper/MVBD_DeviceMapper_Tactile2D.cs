﻿using BrailleIO;
using BrailleIO.Interface;
using Metec.MVBDClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVBDAdapter.DeviceMapper
{
    /// <summary>
    /// Device mapper for the Tactile2D device (48 by 39)
    /// </summary>
    /// <seealso cref="MVBDAdapter.DeviceMapper.IMVBD_DeviceMapper" />
    class MVBD_DeviceMapper_Tactile2D : IMVBD_DeviceMapper
    {
        #region Members

        int _width = 0;
        /// <summary>
        /// Gets or sets the horizontal display size.
        /// </summary>
        /// <value>
        /// The width.
        /// </value>
        internal int Width
        {
            get { return _width; }
            set
            {
                _width = value;
                widthTouchFactor = 48 / _maxX;
            }
        }
        double widthTouchFactor = 0.0;


        int _height = 0;
        /// <summary>
        /// Gets or sets vertical display size.
        /// </summary>
        /// <value>
        /// The height.
        /// </value>
        internal int Height
        {
            get { return _height; }
            set
            {
                _height = value;
                heightTouchFactor = 39 / _maxY;

            }
        }
        double heightTouchFactor = 0.0;

        #endregion

        #region IMVBD_DeviceMapper

        /// <summary>
        /// Gets the device Object for this wrapped pin device.
        /// Call <see cref="BuildDevice" /> first.
        /// </summary>
        /// <value>
        /// The device property set.
        /// </value>
        public BrailleIODevice Device { get; private set; }

        /// <summary>
        /// Gets the device information.
        /// Call <see cref="BuildDevice" /> first.
        /// </summary>
        /// <value>
        /// The device information.
        /// </value>
        public MVBDDeviceInfo DeviceInfo { get; private set; }

        /// <summary>
        /// Converts the MVBD key code to a BrailleIO button.
        /// </summary>
        /// <param name="key">The MVBD key code.</param>
        /// <param name="general">The general button or NONE.</param>
        /// <param name="keyboard">The keyboard button or NONE.</param>
        /// <param name="additional">The array of additional keys or NULL.</param>
        /// <returns>
        ///   <c>true</c> if the key could be translated; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public bool ConvertToBioButton(int key,
            out BrailleIO_DeviceButton general,
            out BrailleIO_BrailleKeyboardButton keyboard,
            out BrailleIO_AdditionalButton[] additional,
            out String genericButtonId
            )
        {
            general = BrailleIO_DeviceButton.None;
            keyboard = BrailleIO_BrailleKeyboardButton.None;
            additional = null;
            genericButtonId = key.ToString();

            switch (this.DeviceInfo.WorkingPosition)
            {
                default:
                    return convertToBioButtonFront(key, out general, out keyboard, out additional, out genericButtonId);
            }
        }

        private bool convertToBioButtonFront(int key, 
            out BrailleIO_DeviceButton general, 
            out BrailleIO_BrailleKeyboardButton keyboard, 
            out BrailleIO_AdditionalButton[] additional, 
            out string genericButtonId)
        {
            general = BrailleIO_DeviceButton.None;
            keyboard = BrailleIO_BrailleKeyboardButton.None;
            additional = null;
            genericButtonId = key.ToString();

            if (key > 0)
            {
                switch (key)
                {
                    case 208:
                        general = BrailleIO_DeviceButton.Up;
                        break;
                    case 209:
                        general = BrailleIO_DeviceButton.Left;
                        break;
                    case 210:
                        general = BrailleIO_DeviceButton.Down;
                        break;
                    case 211:
                        general = BrailleIO_DeviceButton.Right;
                        break;
                    case 212:
                        general = BrailleIO_DeviceButton.Enter;
                        break;

                    case 223:
                        additional = new BrailleIO_AdditionalButton[1]{BrailleIO_AdditionalButton.fn1};
                        break;
                    case 215:
                        additional = new BrailleIO_AdditionalButton[1]{BrailleIO_AdditionalButton.fn2};
                        break;


                    case 224:
                        general = BrailleIO_DeviceButton.ZoomIn;
                        break;
                    case 225:
                        general = BrailleIO_DeviceButton.ZoomOut;
                        break;

                    case 238:
                        general = BrailleIO_DeviceButton.Gesture;
                        break;
                    case 239:
                        general = BrailleIO_DeviceButton.Abort;
                        break;

                    case 200:
                        keyboard = BrailleIO_BrailleKeyboardButton.k1;
                        break;
                    case 201:
                        keyboard = BrailleIO_BrailleKeyboardButton.k2;
                        break;
                    case 202:
                        keyboard = BrailleIO_BrailleKeyboardButton.k3;
                        break;
                    case 203:
                        keyboard = BrailleIO_BrailleKeyboardButton.k4;
                        break;
                    case 204:
                        keyboard = BrailleIO_BrailleKeyboardButton.k5;
                        break;
                    case 205:
                        keyboard = BrailleIO_BrailleKeyboardButton.k6;
                        break;
                    case 206:
                        keyboard = BrailleIO_BrailleKeyboardButton.k7;
                        break;
                    case 207:
                        keyboard = BrailleIO_BrailleKeyboardButton.k8;
                        break;

                    case 214:
                        keyboard = BrailleIO_BrailleKeyboardButton.F1;
                        break;
                    case 222:
                        keyboard = BrailleIO_BrailleKeyboardButton.F2;
                        break;
                    case 236:
                        keyboard = BrailleIO_BrailleKeyboardButton.F11;
                        break;
                    case 237:
                        keyboard = BrailleIO_BrailleKeyboardButton.F22;
                        break;


                    default:
                        return false;
                    // break;
                }
            }


            return true;
        }

        /// <summary>
        /// Builds the device data struct for an MVBD wrapped device.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>
        /// The device identifying struct
        /// </returns>
        public BrailleIODevice BuildDevice(MVBDDeviceInfo deviceInfo)
        {
            DeviceInfo = deviceInfo;
            Device = new BrailleIODevice(deviceInfo.Width, deviceInfo.Height, "MVBD_Tactile2D_" + deviceInfo.WorkingPosition.ToString(), true, true, 20);


            Width = deviceInfo.Width;
            Height = deviceInfo.Height;            
            
            
            return Device;
        }


        const double _maxX = 1600.0; // 1485.0;
        const double _maxY = 774.0; // 1597.0;

        //double maxX, maxY;


        /// <summary>
        /// Converts a finger to a touch.
        /// </summary>
        /// <param name="finger">The finger to convert.</param>
        /// <returns>
        /// The Touch definition for this finger.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public BrailleIO.Structs.Touch ConvertFingerToTouch(MVBDFinger finger)
        {
            BrailleIO.Structs.Touch t = new BrailleIO.Structs.Touch(-1, -1, 0);
            if (finger != null)
            {

                //maxX = Math.Max(maxX, finger.X);
                //maxY = Math.Max(maxY, finger.Y);

                double x = finger.X * widthTouchFactor;
                double y = finger.Y * heightTouchFactor;

                switch (DeviceInfo.WorkingPosition)
                {
                        // TODO: calculate the rotated ones
                    case Position.Right:
                    case Position.Rear:
                    case Position.Left:
                    case Position.Front:
                    default:
                        break;
                }
                
                t = new BrailleIO.Structs.Touch(x, y, 1.0);
            }
            return t;
        }

        #endregion

    }
}
