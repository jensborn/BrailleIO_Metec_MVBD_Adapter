using Metec.MVBDClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVBDAdapter.DeviceMapper
{
    class MVBD_DeviceMapper_HyperBrailleF : MVBD_DeviceMapper_HyperBrailleS
    {

        public MVBD_DeviceMapper_HyperBrailleF(): base()
        {
            maxTouchHeight = 299.0;
            maxTouchWidth = 519.0;
            Width = 104;
            Height = 60;
        }



        /// <summary>
        /// Converts a finger to a touch.
        /// </summary>
        /// <param name="finger">The finger to convert.</param>
        /// <returns>
        /// The Touch definition for this finger.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override BrailleIO.Structs.Touch ConvertFingerToTouch(MVBDFinger finger)
        {
            if(finger.Y > maxTouchHeight)
            {
                maxTouchHeight = finger.Y;
                Height = 60;
                System.Diagnostics.Debug.WriteLine( "New Touch Width Max Value: " + maxTouchHeight);
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


    }
}
