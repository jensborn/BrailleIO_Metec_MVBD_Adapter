using BrailleIO;
using BrailleIO.Interface;
using Metec.MVBDClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVBDAdapter.DeviceMapper
{
    class MVBD_DeviceMapper_HyperBrailleS : IMVBD_DeviceMapper
    {
        #region Members

        //protected double maxTouchWidth = 2047.0;
        protected double maxTouchWidth = 299.0;
        //protected double maxTouchHeight = 2047.0;
        protected double maxTouchHeight = 519.0;


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
                widthTouchFactor = 104 / maxTouchWidth;  // do this with max values because MVBD can change width and height and the mapping does not work anymore.
            }
        }
        protected double widthTouchFactor = 0.0;


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
                heightTouchFactor = 60 / maxTouchHeight; // do this with max values because MVBD can change width and height and the mapping does not work anymore.

            }
        }
        protected double heightTouchFactor = 0.0;

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
        public virtual bool ConvertToBioButton(int key,
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

            if (key > 0)
            {
                switch (key)
                {
                    case 200:
                        keyboard = BrailleIO_BrailleKeyboardButton.k1;
                        genericButtonId = "k1";
                        break;
                    case 201:
                        keyboard = BrailleIO_BrailleKeyboardButton.k2;
                        genericButtonId = "k2";
                        break;
                    case 202:
                        keyboard = BrailleIO_BrailleKeyboardButton.k3;
                        genericButtonId = "k3";
                        break;
                    case 203:
                        keyboard = BrailleIO_BrailleKeyboardButton.k4;
                        genericButtonId = "k4";
                        break;
                    case 204:
                        keyboard = BrailleIO_BrailleKeyboardButton.k5;
                        genericButtonId = "k5";
                        break;
                    case 205:
                        keyboard = BrailleIO_BrailleKeyboardButton.k6;
                        genericButtonId = "k6";
                        break;
                    case 206:
                        keyboard = BrailleIO_BrailleKeyboardButton.k7;
                        genericButtonId = "k7";
                        break;
                    case 207:
                        keyboard = BrailleIO_BrailleKeyboardButton.k8;
                        genericButtonId = "k8";
                        break;


                    case 208:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn4 };
                        genericButtonId = "clu";
                        break;
                    case 209:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn6 };
                        genericButtonId = "cll";
                        break;
                    case 210:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn5 };
                        genericButtonId = "cld";
                        break;
                    case 211:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn7 };
                        genericButtonId = "clr";
                        break;
                    case 212:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn1 };
                        genericButtonId = "clc";
                        break;


                    case 214:
                        keyboard = BrailleIO_BrailleKeyboardButton.F1;
                        genericButtonId = "l";
                        break;
                    case 215:
                        keyboard = BrailleIO_BrailleKeyboardButton.F11;
                        genericButtonId = "lr";
                        break;


                    case 216:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn12 };
                        genericButtonId = "cru";
                        break;
                    case 217:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn14 };
                        genericButtonId = "crl";
                        break;
                    case 218:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn13 };
                        genericButtonId = "crd";
                        break;
                    case 219:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn15 };
                        genericButtonId = "crr";
                        break;
                    case 220:
                        general = BrailleIO_DeviceButton.Enter;
                        genericButtonId = "crc";
                        break;

                    case 222:
                        keyboard = BrailleIO_BrailleKeyboardButton.F2;
                        genericButtonId = "r";
                        break;
                    case 223:
                        keyboard = BrailleIO_BrailleKeyboardButton.F22;
                        genericButtonId = "rl";
                        break;

                    case 224:
                        general = BrailleIO_DeviceButton.ZoomIn;
                        genericButtonId = "rslu";
                        break;
                    case 225:
                        general = BrailleIO_DeviceButton.ZoomOut;
                        genericButtonId = "rsld";
                        break;

                    case 226:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn2 };
                        genericButtonId = "rsru";
                        break;
                    case 227:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn3 };
                        genericButtonId = "rsrd";
                        break;

                    case 229:
                        general = BrailleIO_DeviceButton.Left;
                        genericButtonId = "nsl";
                        break;
                    case 228:
                        general = BrailleIO_DeviceButton.Up;
                        genericButtonId = "nsu";
                        break;
                    case 230:
                        general = BrailleIO_DeviceButton.Down;
                        genericButtonId = "nsd";
                        break;
                    case 231:
                        general = BrailleIO_DeviceButton.Right;
                        genericButtonId = "nsr";
                        break;
                    case 232:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn8 };
                        genericButtonId = "nsuu";
                        break;
                    case 233:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn10 };
                        genericButtonId = "nsll";
                        break;
                    case 234:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn9 };
                        genericButtonId = "nsdd";
                        break;
                    case 235:
                        additional = new BrailleIO_AdditionalButton[1] { BrailleIO_AdditionalButton.fn11 };
                        genericButtonId = "nsrr";
                        break;



                    case 239:
                        general = BrailleIO_DeviceButton.Gesture;
                        genericButtonId = "hbl";
                        break;
                    case 240:
                        general = BrailleIO_DeviceButton.Abort;
                        genericButtonId = "hbr";
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
        public virtual BrailleIODevice BuildDevice(MVBDDeviceInfo deviceInfo)
        {
            DeviceInfo = deviceInfo;
            Device = new BrailleIODevice(deviceInfo.Width, deviceInfo.Height, "MVBD_HyperBarilleS" , true, true, 20);

            Width = deviceInfo.Width;
            Height = deviceInfo.Height;            
            
            return Device;
        }

        /// <summary>
        /// Converts a finger to a touch.
        /// </summary>
        /// <param name="finger">The finger to convert.</param>
        /// <returns>
        /// The Touch definition for this finger.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual BrailleIO.Structs.Touch ConvertFingerToTouch(MVBDFinger finger)
        {
            //BrailleIO.Structs.Touch t = new BrailleIO.Structs.Touch(-1,-1,0);
            //if(finger != null){
            //    t = new BrailleIO.Structs.Touch(finger.X * widthTouchFactor, (maxTouchHeight - finger.Y) * heightTouchFactor, 1.0, 0.5, 0.5);
            //}
            //return t;

            if (finger.Y > maxTouchHeight)
            {
                maxTouchHeight = finger.Y;
                Height = 60;
                System.Diagnostics.Debug.WriteLine("New Touch Width Max Value: " + maxTouchHeight);
            }

            if (finger.X > maxTouchWidth)
            {
                maxTouchWidth = finger.X;
                Width = 104;
                System.Diagnostics.Debug.WriteLine("New Touch Height Max Value: " + maxTouchWidth);
            }

            BrailleIO.Structs.Touch t = new BrailleIO.Structs.Touch(-1, -1, 0);
            if (finger != null)
            {
                t = new BrailleIO.Structs.Touch(finger.X * widthTouchFactor, finger.Y * heightTouchFactor, 1.0, 0.5, 0.5);
            }
            return t;

        }
        #endregion
    }
}
