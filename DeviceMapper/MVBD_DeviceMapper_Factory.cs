using Metec.MVBDClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVBDAdapter.DeviceMapper
{
    static class MVBD_DeviceMapper_Factory
    {

        /// <summary>
        /// Gets the corresponding MVBD device mapper for the requested device info.
        /// </summary>
        /// <param name="deviceInfo">The device information.</param>
        /// <returns>The corresponding device mapper if defined; otherwise the factory returns <c>null</c>.</returns>
        internal static IMVBD_DeviceMapper GetMVBD_DeviceMapper(MVBDDeviceInfo deviceInfo)
        {
            IMVBD_DeviceMapper mapper = null;

            switch (deviceInfo.ID)
            {
                case 24577: // HyperBrailleS
                    mapper = new MVBD_DeviceMapper_HyperBrailleS();
                    break;
                case 28673: // HyperFlat
                    mapper = new MVBD_DeviceMapper_HyperFlat();
                    break;
                case 36865: // Tactile2D
                    mapper = new MVBD_DeviceMapper_Tactile2D();
                    break;
                case 28674: // HGyperBrailleF
                    mapper = new MVBD_DeviceMapper_HyperBrailleF();
                    break;

                #region undefined yet
                case 4097: // BD1
                    mapper = null;
                    break;
                case 4098: // BD2
                    mapper = null;
                    break;
                case 4099: // BD3
                    mapper = null;
                    break;
                case 4100: // BD4
                    mapper = null;
                    break;
                case 4101: // BD5
                    mapper = null;
                    break;
                case 4102: // BD6
                    mapper = null;
                    break;
                case 4110: // BD14
                    mapper = null;
                    break;
                case 4121: // BD25
                    mapper = null;
                    break;
                case 4136: // BD40
                    mapper = null;
                    break;
                case 4138: // BD42
                    mapper = null;
                    break;
                case 4139: // BD43
                    mapper = null;
                    break;
                case 4144: // BD48
                    mapper = null;
                    break;
                case 4176: // BD80
                    mapper = null;
                    break;
                case 4356: // BD4 PWM
                    mapper = null;
                    break;
                case 6147: // BD40_P16
                    mapper = null;
                    break;
                case 6148: // BD3_P16
                    mapper = null;
                    break;
                case 6149: // BD8_P16
                    mapper = null;
                    break;
                case 5122: // Flat20
                    mapper = null;
                    break;
                case 5124: // Flat40
                    mapper = null;
                    break;
                case 5126: // Flat60
                    mapper = null;
                    break;
                case 5128: // Flat80
                    mapper = null;
                    break;
                case 16385: // BD20II
                    mapper = null;
                    break;
                case 16386: // BD25II
                    mapper = null;
                    break;
                case 16387: // BD32II
                    mapper = null;
                    break;
                case 16388: // BD40II
                    mapper = null;
                    break;
                case 20489: // BD3IIBlue
                    mapper = null;
                    break;
                case 20485: // BD10IIBlue
                    mapper = null;
                    break;
                case 20481: // BD20IIBlue
                    mapper = null;
                    break;
                case 20482: // BD25IIBlue
                    mapper = null;
                    break;
                case 20483: // BD32IIBlue
                    mapper = null;
                    break;
                case 20484: // BD40IIBlue
                    mapper = null;
                    break;
                case 20488: // BD80IIBlue
                    mapper = null;
                    break;
                case 8193: // BrailleDis120x60
                    mapper = null;
                    break;
                case 8196: // BrailleDis32x30
                    mapper = null;
                    break;
                case 8199: // BrailleDis32x20
                    mapper = null;
                    break;
                case 12289: // HyperBraille120x60
                    mapper = null;
                    break;
                case 12290: // HyperBraille60x60
                    mapper = null;
                    break;
                case 12291: // HyperBraille24x15
                    mapper = null;
                    break;
                case 12292: // HyperBraille32x30
                    mapper = null;
                    break;
                case 12293: // HyperBraille64x30
                    mapper = null;
                    break;
                case 12294: // HyperBraille32x5
                    mapper = null;
                    break;
                case 12295: // HyperBraille32x20
                    mapper = null;
                    break;
                case 12296: // HyperBraille20x5
                    mapper = null;
                    break;
                case 32769: // Virtual128x64
                    mapper = null;
                    break;
                case 32770: // Virtual120x100
                    mapper = null;
                    break;
                case 32771: // Virtual160x128
                    mapper = null;
                    break;
                #endregion


                default:
                    break;
            }

            if (mapper != null)
            {
                mapper.BuildDevice(deviceInfo);
            }
            return mapper;
        } 
    }
}
